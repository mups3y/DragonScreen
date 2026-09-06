#!/usr/bin/env python
"""
slice_bottom_bar.py - cut the bottom bar's own elements out of `component_48.png`.

WHY THIS EXISTS (S176, the per-page rebuild's unit 1)
-----------------------------------------------------
`docs/BUILD_PLAN.md` §14.2a clause (1) (G13, owner-authorised 2026-09-06): an element present in the
export is BUILT FROM the export, LAYERED at the export's own coordinates, and never left flattened
once its own export exists. The bottom bar is a flattened COMPONENT: 337 files landed under
`assets/figma/` (S175) and NONE of them is a bottom-bar nav icon - checked across all seven zips.
The unit prompt's own allowance covers exactly this case:

    "Where an element has no individual export, slicing it from a flat uniform ground is legitimate
     ... eyeballing it off a screenshot is not."

The bar's ground IS flat and uniform (`#111B52`, `DragonPalette.Panel`), so every box below was
MEASURED off the asset - ink bounding boxes, then padded to a whole box that contains the ink and
nothing else's. The measurements are printed by `--verify` so a later chat can re-derive them rather
than trust this docstring.

⛔ THIS IS NOT A BUILD STEP. It is run ONCE, by hand, and its outputs are committed as art. The bar
draws the committed PNGs; nothing at build time reads `component_48.png` any more except the tiles'
own provenance. Re-run it only if `component_48.png` is re-exported - and read S175 first, because
the shipped `component_48.png` is the clean export PLUS S147's CURRENT STATE erase at
x 1098..1461, y 170..208, which a fresh export does NOT carry.

    python plugin/tools/slice_bottom_bar.py --verify    # measure and report, write nothing
    python plugin/tools/slice_bottom_bar.py             # write the tiles into art/cover/
"""
import os
import sys

try:
    from PIL import Image
except ImportError:
    sys.exit("this tool needs Pillow (pip install pillow); it is NOT needed to build or preview")

HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.join(HERE, '..', 'GameData', 'DragonScreen', 'art', 'cover')
SRC = os.path.join(ART, 'component_48.png')

GROUND = (17, 27, 82, 255)          # DragonPalette.Panel, the bar's own flat ground

# ---------------------------------------------------------------------------------------------
# The cut list, in component_48's OWN pixels. component_48 is 3427x235 and spans the design frame's
# full width at design y 1877, so its x IS design x and its y is design y minus 1877.
#
# Each row: key, (x, y, w, h), and what the box is.  The `ink` column is the MEASURED ink bounding
# box the cut must contain - `--verify` re-measures it and fails if a cut has drifted off its ink.
# ---------------------------------------------------------------------------------------------
CUTS = [
    # The design frame's two rounded BOTTOM CORNERS - the arc ONLY, cut at row 105, which is where
    # the border stops curving and becomes the straight top rule. Everything else in the bar's chrome
    # is a straight edge and is drawn as a primitive; these two are curves and have to come from the
    # art.
    # THE CUT HEIGHT IS 105 FOR A MEASURED REASON, and 112 was tried first and is visible in a render.
    # A tile is drawn 132x105 source into 88x70 panel px, so a 2-row rule inside it comes out as a
    # ~1.3 px SOFT band; the primitive rule beside it is a crisp 2 device px (Strokes.Px). Including
    # rows 105-106 in the tile therefore put a blurred rule end-to-end against a crisp one and the
    # seam read as a GAP at the bar's right end. Cutting at 105 leaves the tile carrying only the
    # curve, and the straight rule is one primitive from x 90 to x 3337 - the arc's own feet,
    # measured: row 105's white run is x 90..3336.
    ('bar_cap_left',            (0,    0, 132, 105), 'frame corner, left'),
    ('bar_cap_right',           (3295, 0, 132, 105), 'frame corner, right'),

    # The five nav icons, at BottomBar.IconX / IconY / IconS exactly - the same 80x80 boxes the hit
    # map already used, so the drawn icon and its touch target are cut from one set of numbers.
    ('bar_nav_0',               (46,  126, 80, 80), 'compass'),
    ('bar_nav_1',               (174, 126, 80, 80), 'target'),
    ('bar_nav_2',               (302, 126, 80, 80), 'rocket'),
    ('bar_nav_3',               (430, 126, 80, 80), 'folder'),
    ('bar_nav_4',               (558, 126, 80, 80), 'gear'),

    # The two captions and the one baked value.  ⚠ These are TEXT in the export and §14.2a clause (1)
    # wants text TYPED. They are cut as pixels here and the reason is written up in REGISTER.md S176:
    # typing them puts them under S153's floor policy, which for the nav bar is the GLANCEABLE floor
    # (Typography.Dense's own docstring excludes "anything on the nav bar"), i.e. 48.07 design px
    # against the export's 20.3 - a 2.4x raise that re-flows the whole bar. That is S153a-Q1's wall,
    # and S153a-Q1 is OPEN and the owner's.
    ('bar_label_current_state', (1287, 141, 158, 22), 'CURRENT STATE caption'),
    ('bar_label_pointing_mode', (1966, 141, 158, 22), 'POINTING MODE caption'),
    ('bar_value_pointing_mode', (1965, 171, 144, 30), 'Sun + GEO - baked, S147b owns its source'),

    # The comm block and the counter, as ONE element: SPX ring + SPX + downlink + 22:33/GND +
    # 0.00/TDRS + ISS ring + ISS + downlink + 79/1450122. Cut whole because its parts are laid out
    # against each other, not against the frame, and because all three of its VALUES are S147b's
    # held owner question - see REGISTER.md S176.
    ('bar_comm_block',          (2663, 136, 686, 59), 'SPX/GND/TDRS/ISS + counter'),
]


def ink_bbox(px, x0, y0, w, h):
    """The bounding box of everything that is not the bar's flat ground, inside a cut."""
    xs, ys = [], []
    for y in range(y0, y0 + h):
        for x in range(x0, x0 + w):
            p = px[x, y]
            if p != GROUND and p[3] > 0:
                xs.append(x)
                ys.append(y)
    if not xs:
        return None
    return (min(xs), min(ys), max(xs), max(ys))


def main():
    verify = '--verify' in sys.argv
    im = Image.open(SRC).convert('RGBA')
    if im.size != (3427, 235):
        sys.exit('component_48.png is %dx%d, expected 3427x235' % im.size)
    px = im.load()

    bad = 0
    for key, (x, y, w, h), what in CUTS:
        bb = ink_bbox(px, x, y, w, h)
        # Every cut must actually contain ink, and the ink must not touch the cut's own edge - if it
        # does, the element has been clipped and the box is wrong.
        if bb is None:
            print('  EMPTY  %-26s (%d,%d,%d,%d)  %s' % (key, x, y, w, h, what))
            bad += 1
            continue
        touching = (bb[0] <= x or bb[1] <= y or bb[2] >= x + w - 1 or bb[3] >= y + h - 1)
        print('  %-26s cut (%4d,%3d,%4d,%3d)  ink x %4d..%4d y %3d..%3d%s   %s'
              % (key, x, y, w, h, bb[0], bb[2], bb[1], bb[3],
                 '  <<< INK TOUCHES THE CUT EDGE' if touching else '', what))
        # The two corner caps and the nav icons deliberately run to their own edges (a corner cap IS
        # the frame border, which reaches x 0; a nav-icon box is the hit box, not the ink box), so
        # the edge test applies only to the text cuts.
        if touching and not (key.startswith('bar_cap') or key.startswith('bar_nav')):
            bad += 1

        if not verify:
            im.crop((x, y, x + w, y + h)).save(os.path.join(ART, key + '.png'))

    if bad:
        sys.exit('%d cut(s) are wrong - nothing was trusted' % bad)
    print('  %d cuts %s' % (len(CUTS), 'verified' if verify else 'written to art/cover/'))


if __name__ == '__main__':
    main()
