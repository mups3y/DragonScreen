#!/usr/bin/env python
"""
Render EVERY reference SVG to build/refart, once, so we can SEE what we already own.

The Figma export has no semantic layer names (ASSET_PROVENANCE.md), so the only way to find out
what a frame contains is to look at it. Rendering all of them costs minutes and has already
prevented one from-scratch rebuild of art that was sitting on disk.

Reference renders only - none of this ships.
"""
import os, sys, glob
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))          # .../DragonScreen
ART  = os.path.join(ROOT, 'assets', 'figma')
OUT  = os.path.join(HERE, 'refart')
TMP  = os.path.join(HERE, 'tmp')

SKIP = ('Cover', 'README', 'Interface', 'Read ')   # authors' annotation, not interface

def main():
    from svglib.svglib import svg2rlg
    from reportlab.graphics import renderPDF
    import pypdfium2 as pdfium
    os.makedirs(OUT, exist_ok=True); os.makedirs(TMP, exist_ok=True)
    found = sorted(glob.glob(os.path.join(ART, '*', '*.svg')))
    # A glob that matches nothing must SAY SO. The first version silently rendered zero files and
    # exited 0, which looks exactly like success.
    if not found:
        sys.exit('no SVGs under %s - check the path' % ART)
    print('%d SVGs under %s' % (len(found), ART))
    for svg in found:
        name = os.path.splitext(os.path.basename(svg))[0]
        if any(name.startswith(s) for s in SKIP):
            print('skip  %s (annotation)' % name); continue
        folder = os.path.basename(os.path.dirname(svg))
        slug = (folder + '_' + name).lower().replace(' ', '_')
        out = os.path.join(OUT, slug + '.png')
        if os.path.isfile(out):
            print('have  %s' % slug); continue
        try:
            d = svg2rlg(svg)
            if d is None: print('FAIL  %s (unparsed)' % slug); continue
            pdf = os.path.join(TMP, slug + '.pdf')
            renderPDF.drawToFile(d, pdf)
            doc = pdfium.PdfDocument(pdf)
            img = doc[0].render(scale=1100.0 / d.width).to_pil().convert('RGB')
            img.save(out)
            print('ok    %s  %dx%d' % (slug, img.width, img.height))
        except Exception as e:
            print('FAIL  %s: %s' % (slug, e))

main()
