#!/usr/bin/env python3
"""
Script: generate_vexflow_svg_glyphs.py

Reads VexFlow's Bravura font outline table (TypeScript) and generates:
- one SVG per glyph
- glyphs.json with metrics and filenames

This is deterministic and does not require network access.

Usage:
  python scripts/generate_vexflow_svg_glyphs.py \
    --vexflow-font path/to/vexflow/src/fonts/bravura_glyphs.ts \
    --out assets/svg/vexflow-bravura
"""

import argparse
import json
import re
from pathlib import Path


def outline_to_svg_path(outline: str) -> str:
    tokens = outline.strip().split()
    i = 0
    parts = []
    while i < len(tokens):
        t = tokens[i]
        i += 1
        if t == "m":
            x = float(tokens[i])
            y = float(tokens[i + 1])
            i += 2
            parts.append(f"M {x:g} {-y:g}")
        elif t == "l":
            x = float(tokens[i])
            y = float(tokens[i + 1])
            i += 2
            parts.append(f"L {x:g} {-y:g}")
        elif t == "q":
            cpx = float(tokens[i])
            cpy = float(tokens[i + 1])
            x = float(tokens[i + 2])
            y = float(tokens[i + 3])
            i += 4
            parts.append(f"Q {cpx:g} {-cpy:g} {x:g} {-y:g}")
        elif t == "b":
            cp1x = float(tokens[i])
            cp1y = float(tokens[i + 1])
            cp2x = float(tokens[i + 2])
            cp2y = float(tokens[i + 3])
            x = float(tokens[i + 4])
            y = float(tokens[i + 5])
            i += 6
            parts.append(f"C {cp1x:g} {-cp1y:g} {cp2x:g} {-cp2y:g} {x:g} {-y:g}")
        elif t.lower() == "z":
            parts.append("Z")
        else:
            # Unknown token. VexFlow's Bravura outlines usually only use m/l/q/b/z.
            # We silently ignore to keep the script robust.
            pass

    return " ".join(parts)


def parse_vexflow_bravura_glyphs(ts_text: str):
    """
    Parse the TypeScript object literal entries like:
      gClef: { x_min: 0, ... o: 'm 0 0 ...' },
    into a dict { name -> { x_min, x_max, y_min, y_max, ha, o } }.
    """
    pattern = re.compile(
        r"^\\s{4}([A-Za-z0-9_]+|'[^']+'):\\s*\\{\\s*(.*?)^\\s{4}\\},\\s*$",
        re.M | re.S,
    )
    matches = list(pattern.finditer(ts_text))
    glyphs = {}
    for m in matches:
        name = m.group(1)
        if name.startswith("'") and name.endswith("'"):
            name = name[1:-1]
        body = m.group(2)

        def get_num(field):
            mm = re.search(rf"{field}:\\s*(-?\\d+)", body)
            return int(mm.group(1)) if mm else None

        om = re.search(r"o:\\s*'([^']*)'", body)
        glyphs[name] = {
            "x_min": get_num("x_min"),
            "x_max": get_num("x_max"),
            "y_min": get_num("y_min"),
            "y_max": get_num("y_max"),
            "ha": get_num("ha"),
            "o": om.group(1) if om else None,
        }
    return glyphs


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--vexflow-font", required=True, help="Path to VexFlow bravura_glyphs.ts")
    ap.add_argument("--out", required=True, help="Output directory for svg files")
    args = ap.parse_args()

    font_path = Path(args.vexflow_font)
    out_dir = Path(args.out)
    out_dir.mkdir(parents=True, exist_ok=True)

    text = font_path.read_text(encoding="utf-8")
    glyphs = parse_vexflow_bravura_glyphs(text)

    meta = {}
    for name, g in glyphs.items():
        if not g.get("o"):
            continue

        d = outline_to_svg_path(g["o"])

        x_min, x_max, y_min, y_max = g["x_min"], g["x_max"], g["y_min"], g["y_max"]
        view_x = x_min
        view_y = -y_max
        view_w = x_max - x_min
        view_h = y_max - y_min

        filename = re.sub(r"[^A-Za-z0-9_.-]+", "_", name) + ".svg"
        svg = (
            f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="{view_x} {view_y} {view_w} {view_h}">\\n'
            f'  <path d="{d}" fill="currentColor" />\\n'
            f"</svg>\\n"
        )

        (out_dir / filename).write_text(svg, encoding="utf-8")
        meta[name] = {
            "file": str((out_dir / filename).as_posix()),
            "x_min": x_min,
            "x_max": x_max,
            "y_min": y_min,
            "y_max": y_max,
            "ha": g["ha"],
            "outline_source": "vexflow BravuraFont glyph outline (bravura_glyphs.ts)",
        }

    (out_dir / "glyphs.json").write_text(json.dumps(meta, indent=2, ensure_ascii=False), encoding="utf-8")
    print(f"Generated {len(meta)} glyphs into {out_dir}")


if __name__ == "__main__":
    main()
