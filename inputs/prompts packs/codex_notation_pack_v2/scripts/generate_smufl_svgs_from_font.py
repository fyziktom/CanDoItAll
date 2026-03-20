#!/usr/bin/env python3
"""
Script: generate_smufl_svgs_from_font.py

Generates SVG files for SMuFL codepoints in the Private Use Area (U+E000..U+F8FF)
from a Bravura font file (e.g. Bravura.woff2).

Output:
- one SVG per codepoint: uE050.svg, ...
- glyphs.json mapping U+XXXX -> file + bbox + glyphName

This is useful when you want *complete* symbol coverage without relying on SMuFL name->codepoint metadata.
The codepoint is the stable identifier.

Usage:
  python scripts/generate_smufl_svgs_from_font.py \
    --font path/to/Bravura.woff2 \
    --out assets/svg/bravura-smufl
"""

import argparse
import json
from pathlib import Path

from fontTools.ttLib import TTFont
from fontTools.pens.svgPathPen import SVGPathPen
from fontTools.pens.boundsPen import BoundsPen


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--font", required=True, help="Path to a Bravura font (woff2/ttf/otf)")
    ap.add_argument("--out", required=True, help="Output directory for svg files")
    args = ap.parse_args()

    font_path = Path(args.font)
    out_dir = Path(args.out)
    out_dir.mkdir(parents=True, exist_ok=True)

    font = TTFont(str(font_path))
    cmap = font.getBestCmap()
    glyph_set = font.getGlyphSet()

    pua = [cp for cp in cmap.keys() if 0xE000 <= cp <= 0xF8FF]
    pua.sort()

    meta = {}
    for cp in pua:
        gname = cmap[cp]
        if gname == ".notdef":
            continue

        # Compute bounding box in font units.
        bpen = BoundsPen(glyph_set)
        glyph_set[gname].draw(bpen)
        bounds = bpen.bounds
        if bounds is None:
            continue

        xMin, yMin, xMax, yMax = bounds
        width = xMax - xMin
        height = yMax - yMin
        if width <= 0 or height <= 0:
            continue

        # Get SVG path commands (still in font coordinate system: y+ is up).
        pen = SVGPathPen(glyph_set)
        glyph_set[gname].draw(pen)
        d = pen.getCommands()

        # Flip y axis for SVG by applying transform scale(1,-1).
        # Use viewBox y=-yMax so bounds map correctly after the flip.
        view_x = xMin
        view_y = -yMax
        view_w = width
        view_h = height

        hexcode = f"{cp:04X}"
        filename = f"u{hexcode}.svg"
        svg = (
            f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="{view_x:g} {view_y:g} {view_w:g} {view_h:g}">\\n'
            f'  <path d="{d}" fill="currentColor" transform="scale(1,-1)" />\\n'
            f"</svg>\\n"
        )
        (out_dir / filename).write_text(svg, encoding="utf-8")
        meta[f"U+{hexcode}"] = {
            "file": str((out_dir / filename).as_posix()),
            "glyphName": gname,
            "xMin": xMin,
            "yMin": yMin,
            "xMax": xMax,
            "yMax": yMax,
        }

    (out_dir / "glyphs.json").write_text(json.dumps(meta, indent=2, ensure_ascii=False), encoding="utf-8")
    print(f"Generated {len(meta)} SMuFL SVGs into {out_dir}")


if __name__ == "__main__":
    main()
