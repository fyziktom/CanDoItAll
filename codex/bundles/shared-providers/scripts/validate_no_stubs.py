#!/usr/bin/env python3
"""Reject implementation placeholders in explicitly selected text files."""

from __future__ import annotations

import argparse
import re
from pathlib import Path


PATTERNS = {
    "TODO marker": re.compile(r"\bTODO\b", re.IGNORECASE),
    "FIXME marker": re.compile(r"\bFIXME\b", re.IGNORECASE),
    "NotImplementedException": re.compile(r"\bNotImplementedException\b"),
    "placeholder exception": re.compile(r"throw\s+new\s+NotSupportedException\s*\(\s*\)"),
}


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("files", nargs="+")
    args = parser.parse_args()

    failures: list[str] = []
    for raw_path in args.files:
        path = Path(raw_path).resolve()
        if not path.is_file():
            failures.append(f"missing file: {path}")
            continue

        text = path.read_text(encoding="utf-8")
        for label, pattern in PATTERNS.items():
            for match in pattern.finditer(text):
                line = text.count("\n", 0, match.start()) + 1
                failures.append(f"{path}:{line}: {label}")

    if failures:
        print("Stub audit failed:")
        print("\n".join(failures))
        return 1

    print(f"Stub audit passed for {len(args.files)} selected files.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
