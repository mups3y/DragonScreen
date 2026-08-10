#!/usr/bin/env python
"""
Rasterise a Figma-exported Dragon page SVG to a PNG the plugin can load.

    python rasterise.py                       # default page (Frame 58) at 2x panel width
    python rasterise.py "Frame 59" 1920       # a different page, explicit width

---- THE PIPELINE IS SVG -> PDF -> PNG, AND THAT IS NOT AN ACCIDENT ----
Three routes were tried on 2026-08-04. Do not re-derive this:

  1. resvg           - the obvious tool. v0.48.1 ships NO WINDOWS BINARY, only Linux and macOS.
  2. svglib renderPM - parses our SVGs correctly, but its raster backend needs rlPyCairo AND then
                       crashes anyway: `_shape_to_pdf_path` assumes a PDF canvas and dies on our
                       clip paths with "'NoneType' object has no attribute 'moveTo'".
  3. svglib -> PDF -> pypdfium2 -> PNG   <-- THIS ONE. svglib's NATIVE path, so the clip handling is
                       the code that was actually tested by its authors.

Dependencies are BUILD-TIME ONLY - nothing here ships to the game:
    pip install svglib reportlab pypdfium2

---- CAVEAT WORTH REMEMBERING ----
svglib is a SUBSET renderer. If a page looks wrong in game, compare it against the source SVG opened
in a browser before assuming the plugin is at fault - the loss is more likely here.
"""
import os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
SRC_DIR = os.path.join(ROOT, 'assets', 'figma', 'dashboard_ui')
# Reference renders, NOT game assets - these are for LOOKING at the source art, which is
# the only way to find out what is in a 26-id Figma export with no semantic names.
OUT_DIR = os.path.join(HERE, 'build', 'refart')
TMP_DIR = os.path.join(HERE, 'build')

# Frame 58 is the richest instrument page in the set - 493 paths, 26 circles, 51 lines, and ZERO
# embedded rasters, which is why it is the default. Frame 59 is the next cleanest.
DEFAULT_PAGE = 'Frame 58'

# 2x the 960 px panel, so the texture still holds up if the panel grows or the user runs at 4K.
DEFAULT_WIDTH = 1920.0


def rasterise(page_name, target_width):
    from svglib.svglib import svg2rlg
    from reportlab.graphics import renderPDF
    import pypdfium2 as pdfium

    src = os.path.join(SRC_DIR, page_name + '.svg')
    if not os.path.isfile(src):
        sys.exit('no such page: %s' % src)

    slug = page_name.lower().replace(' ', '_')
    pdf = os.path.join(TMP_DIR, 'dragon_%s.pdf' % slug)
    out = os.path.join(OUT_DIR, 'dragon_page_%s.png' % slug.replace('frame_', ''))
    os.makedirs(TMP_DIR, exist_ok=True)
    os.makedirs(OUT_DIR, exist_ok=True)

    d = svg2rlg(src)
    if d is None:
        sys.exit('svglib could not parse %s' % src)
    print('%s: drawing %.0f x %.0f' % (page_name, d.width, d.height))

    renderPDF.drawToFile(d, pdf)

    doc = pdfium.PdfDocument(pdf)
    bitmap = doc[0].render(scale=target_width / d.width)
    img = bitmap.to_pil().convert('RGB')
    img.save(out)

    print('  -> %s  %dx%d  %.1f KB' % (out, img.width, img.height,
                                       os.path.getsize(out) / 1024.0))
    print('  run `python build.py install` to copy it into the game.')


if __name__ == '__main__':
    page = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_PAGE
    width = float(sys.argv[2]) if len(sys.argv) > 2 else DEFAULT_WIDTH
    rasterise(page, width)
