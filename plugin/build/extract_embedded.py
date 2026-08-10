#!/usr/bin/env python
"""
Pull the EMBEDDED RASTER IMAGES out of the Figma SVGs.

These files are mostly base64 payloads, not vectors - ASSET_PROVENANCE.md already noted that the
docking export is "20.9 MB of which almost all is a single embedded Mars photograph". svglib renders
the vector layers and DROPS the embedded images silently, so the rasterised pages came out with holes
where the artwork is: the Dragon render on the VEHICLE OVERVIEW page is one of them.

Writes every embedded image to build/refart/embedded/.
"""
import os, re, base64, glob, sys
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
ART  = os.path.join(ROOT, 'assets', 'figma')
OUT  = os.path.join(HERE, 'refart', 'embedded')

PAT = re.compile(rb'data:image/(png|jpeg|jpg|gif);base64,([A-Za-z0-9+/=\s]+?)["\')]')

def main():
    os.makedirs(OUT, exist_ok=True)
    files = sorted(glob.glob(os.path.join(ART, '*', '*.svg')))
    if not files:
        sys.exit('no SVGs under %s' % ART)
    total = 0
    for svg in files:
        name = os.path.splitext(os.path.basename(svg))[0].lower().replace(' ', '_')
        data = open(svg, 'rb').read()
        n = 0
        for ext, blob in PAT.findall(data):
            raw = base64.b64decode(re.sub(rb'\s', b'', blob))
            # Skip tiny blobs - gradients and masks get embedded too and are not artwork.
            if len(raw) < 20000:
                continue
            n += 1
            out = os.path.join(OUT, '%s_%d.%s' % (name, n, ext.decode()))
            open(out, 'wb').write(raw)
            print('  %-34s %8.1f KB  -> %s' % (name, len(raw)/1024.0, os.path.basename(out)))
            total += 1
        if n == 0:
            print('  %-34s (no embedded artwork)' % name)
    print('%d embedded images extracted' % total)

main()
