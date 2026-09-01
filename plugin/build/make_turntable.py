#!/usr/bin/env python
"""
Generate the PLACEHOLDER capsule turntable sequence:

    GameData/DragonScreen/art/cover/dragon_turn_000.png ... dragon_turn_035.png

    python build/make_turntable.py

---- THIS IS A STAND-IN, AND IT SAYS SO ON EVERY FRAME ----
BUILD_PLAN §5 wants a pre-rendered sprite turntable of the real vehicle, rendered from the MaTte0
"Crew Dragon Falcon 9" model (Sketchfab, CC-BY). That model is NOT in the repo, and C7 puts external
URLs off-limits as build inputs, so T11 was SPLIT (owner decision via the overseer, 2026-09-02):

    T11a   the model-independent half - src/pure/Turntable.cs (sequence naming, frame picker,
           drag maths) - driven against THIS sequence.
    T11b   held: place the licence-clean model in the repo, render the real trunk-inclusive frames
           over these, clear Turntable.Placeholder, and verify the drag on glass.

So every frame here is a deliberately SCHEMATIC wireframe carrying the words PLACEHOLDER and NOT A
RENDER, plus its own frame number and azimuth. §1.4's rule is that invented material is never passed
off as sourced material; a stand-in that does not look like one breaks that rule quietly, which is
the worst way to break it. Nothing here is claimed to be Crew Dragon geometry - it is a body of
revolution of roughly the right proportions whose only job is to make rotation legible.

---- WHAT MAKES THE ROTATION READABLE ----
A body of revolution has the same silhouette from every angle, so a silhouette alone would give 36
identical frames and prove nothing about the frame picker or the drag. Three cues turn instead:

    the ribs        a wireframe cylinder: near-side generatrices only, brightness by facing angle
    the index rib   ONE rib in accent cyan - absolute azimuth, unmistakable
    the hatch       an off-axis disc that orbits and foreshortens, and hides on the far side
    the compass     a top-down dial with a fixed FRONT mark and a tick at this frame's azimuth

---- DETERMINISTIC ----
Fixed geometry, no randomness: running this twice produces byte-identical files, so the sequence can
be regenerated without a spurious diff. (The text is drawn with Pillow's OWN bundled default font,
not a system font, for the same reason - a machine-dependent font would make the output machine
dependent. Pillow's bundled face can change between Pillow major versions; that is the one thing
that would move these bytes, and it is recorded here rather than left to be rediscovered.)

Written with Pillow 12.3.
"""
import math
import os

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
OUT_DIR = os.path.join(HERE, '..', 'GameData', 'DragonScreen', 'art', 'cover')

# ---- the sequence. MUST match src/pure/Turntable.cs: Count, FrameW, FrameH, KeyPrefix. ----
COUNT = 36
W, H = 512, 1024
PREFIX = 'dragon_turn_'

# ---- palette, straight from src/pure/DragonPalette.cs so the stand-in sits on the page's own
# colours rather than introducing a thirty-seventh blue. ----
OUTLINE = (132, 137, 163, 255)      # Text6
STRUCT = (17, 27, 82, 150)          # Panel, translucent - the body reads as a shell, not a slab
RIB_NEAR = (49, 61, 123, 255)       # Hairline
ACCENT = (32, 251, 253, 255)        # Accent
DIM = (88, 93, 124, 255)            # Text7
MARK = (255, 183, 75, 255)          # Caution - the PLACEHOLDER wording
SUB = (166, 171, 201, 255)          # Text5

# ---- vehicle proportions, in metres, then one scale. Roughly Crew Dragon + trunk (~4.0 m across,
# ~8.1 m tall) so the sprite occupies the slot the real render will; not a claim about geometry. ----
CAPSULE_M, TRUNK_M = 4.4, 3.7
RADIUS_M = 2.0

TOP = 60                            # px of headroom
TEXT_TOP = 838                      # everything below this is the marking block
PPM = (TEXT_TOP - 40 - TOP) / (CAPSULE_M + TRUNK_M)

CX = W * 0.5
R = RADIUS_M * PPM                  # body radius, px
TILT = 0.18                         # vertical semi-axis of a circular section, as a fraction of R

CAP_TOP = TOP
CAP_BOT = TOP + CAPSULE_M * PPM
TRUNK_BOT = CAP_BOT + TRUNK_M * PPM

NOSE_H = 70.0                       # dome
BARREL_H = 50.0                     # short cylindrical nose barrel
NOSE_R = 0.7 * PPM                  # barrel radius


def half_width(y):
    """Body half-width at height y - the silhouette, which is the same from every azimuth."""
    if y <= CAP_TOP + NOSE_H:
        # elliptical dome, 0 at the very top
        t = (y - CAP_TOP) / NOSE_H
        return NOSE_R * math.sqrt(max(0.0, 1.0 - (1.0 - t) ** 2))
    if y <= CAP_TOP + NOSE_H + BARREL_H:
        return NOSE_R
    if y <= CAP_BOT:
        t = (y - CAP_TOP - NOSE_H - BARREL_H) / (CAP_BOT - CAP_TOP - NOSE_H - BARREL_H)
        return NOSE_R + t * (R - NOSE_R)
    return R


def ellipse(d, cy, rx, colour, width=2):
    """A circular cross-section seen from the side: an ellipse of vertical semi-axis TILT*rx."""
    ry = max(1.0, TILT * rx)
    d.ellipse([CX - rx, cy - ry, CX + rx, cy + ry], outline=colour, width=width)


def frame(i):
    az = math.radians(i * 360.0 / COUNT)
    img = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    # ---- the shell: silhouette as a filled polygon, so the ribs read as INSIDE it ----
    left, right = [], []
    y = CAP_TOP
    while y <= TRUNK_BOT:
        hw = half_width(y)
        left.append((CX - hw, y))
        right.append((CX + hw, y))
        y += 4.0
    d.polygon(left + list(reversed(right)), fill=STRUCT)

    # ---- the ribs: a wireframe cylinder on the TRUNK. Only the near half is drawn (cos > 0), and
    # each rib dims towards the limb, which is what makes the spin readable at a glance. ----
    ribs = 24
    for j in range(ribs):
        a = az + j * 2.0 * math.pi / ribs
        c = math.cos(a)
        if c <= 0.02:
            continue
        x = CX + R * math.sin(a)
        k = min(1.0, c)
        col = (int(RIB_NEAR[0] + (OUTLINE[0] - RIB_NEAR[0]) * k),
               int(RIB_NEAR[1] + (OUTLINE[1] - RIB_NEAR[1]) * k),
               int(RIB_NEAR[2] + (OUTLINE[2] - RIB_NEAR[2]) * k), 255)
        d.line([(x, CAP_BOT), (x, TRUNK_BOT)], fill=col, width=2)

    # THE INDEX RIB - azimuth 0, in accent cyan, drawn last so it wins where ribs coincide. Absolute
    # orientation with no counting: whichever way it leans is which way the frame faces.
    c0 = math.cos(az)
    x0 = CX + R * math.sin(az)
    if c0 > 0.0:
        d.line([(x0, CAP_BOT), (x0, TRUNK_BOT)], fill=ACCENT, width=4)
    else:
        # far side: a dashed ghost, so the index is trackable all the way round rather than
        # vanishing for half the sequence
        yy = CAP_BOT
        while yy < TRUNK_BOT:
            d.line([(x0, yy), (x0, min(yy + 10, TRUNK_BOT))], fill=DIM, width=2)
            yy += 22

    # ---- the outline + the section rings ----
    d.line(left, fill=OUTLINE, width=3)
    d.line(right, fill=OUTLINE, width=3)
    ellipse(d, CAP_BOT, R, OUTLINE, 3)                       # capsule/trunk joint
    ellipse(d, TRUNK_BOT, R, OUTLINE, 3)                     # trunk base
    ellipse(d, CAP_TOP + NOSE_H + BARREL_H, NOSE_R, OUTLINE, 2)

    # ---- the side hatch: an off-axis disc on the capsule cone. It orbits (x = r sin a),
    # foreshortens (width scales with |cos a|) and is hidden on the far side, which is the cue a
    # silhouette cannot give. ----
    hy = CAP_TOP + NOSE_H + BARREL_H + 0.45 * (CAP_BOT - CAP_TOP - NOSE_H - BARREL_H)
    hr_local = half_width(hy)
    hx = CX + hr_local * math.sin(az)
    if math.cos(az) > 0.0:
        rr = 30.0
        rw = max(2.0, rr * abs(math.cos(az)))
        d.ellipse([hx - rw, hy - rr, hx + rw, hy + rr], outline=ACCENT, width=3)

    # ---- the compass: a top-down dial, FRONT fixed at the top, a tick at this frame's azimuth.
    # Always readable, including through the half-revolution where the hatch is hidden. ----
    ccx, ccy, cr = 76.0, TEXT_TOP + 62.0, 46.0
    d.ellipse([ccx - cr, ccy - cr, ccx + cr, ccy + cr], outline=DIM, width=2)
    d.line([(ccx, ccy - cr - 8), (ccx, ccy - cr + 8)], fill=SUB, width=3)     # FRONT mark
    # screen x = sin(az), screen y = -cos(az): the dial is the view from ABOVE, front at the top.
    d.line([(ccx, ccy),
            (ccx + cr * 0.82 * math.sin(az), ccy - cr * 0.82 * math.cos(az))],
           fill=ACCENT, width=4)

    # ---- the marking. The frame number is INSIDE the sprite on purpose: the page prints one too,
    # and the pair together prove the picker asked for a frame AND that that file is what loaded. ----
    big = ImageFont.load_default(size=30)
    small = ImageFont.load_default(size=22)
    d.text((150, TEXT_TOP + 18), 'PLACEHOLDER', font=big, fill=MARK)
    d.text((150, TEXT_TOP + 54), 'NOT A RENDER - T11b', font=small, fill=MARK)
    d.text((150, TEXT_TOP + 82), 'FRAME %02d / %d   AZ %03d' % (i, COUNT, round(i * 360.0 / COUNT)),
           font=small, fill=SUB)
    return img


def main():
    out = os.path.abspath(OUT_DIR)
    os.makedirs(out, exist_ok=True)
    for i in range(COUNT):
        path = os.path.join(out, '%s%03d.png' % (PREFIX, i))
        frame(i).save(path, 'PNG', optimize=True)
    print('wrote %d placeholder turntable frames (%dx%d) to %s' % (COUNT, W, H, out))


if __name__ == '__main__':
    main()
