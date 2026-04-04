#!/usr/bin/env python3
"""
Create a timestamped architecture review folder from a template.
"""

from __future__ import annotations

import argparse
import datetime as dt
from pathlib import Path
import re
import shutil
import sys


def slugify(value: str) -> str:
    value = value.strip().lower()
    value = re.sub(r"[^a-z0-9]+", "-", value)
    value = re.sub(r"-{2,}", "-", value).strip("-")
    return value or "review"


def main() -> int:
    parser = argparse.ArgumentParser(description="Create a timestamped architecture review folder.")
    parser.add_argument("review_kind", help="Review kind, for example canonical-model-review.")
    parser.add_argument("--scope", default="", help="Optional human-readable scope.")
    parser.add_argument("--root", default="architecture/reviews", help="Target review root folder.")
    parser.add_argument("--template", default="", help="Optional path to a Markdown template.")
    args = parser.parse_args()

    stamp = dt.datetime.now().strftime("%Y-%m-%d")
    scope_slug = f"-{slugify(args.scope)}" if args.scope else ""
    folder_name = f"{stamp}-{slugify(args.review_kind)}{scope_slug}"

    target_root = Path(args.root)
    target_dir = target_root / folder_name
    target_dir.mkdir(parents=True, exist_ok=False)

    template_path = Path(args.template) if args.template else None
    report_path = target_dir / "report.md"
    findings_path = target_dir / "findings.md"
    notes_path = target_dir / "notes.md"

    if template_path and template_path.exists():
        shutil.copyfile(template_path, report_path)
    else:
        report_path.write_text(
            "# Architecture Review Report\\n\\n"
            f"- Review kind: `{args.review_kind}`\\n"
            f"- Scope: `{args.scope or 'unspecified'}`\\n"
            f"- Date: `{stamp}`\\n\\n"
            "## Executive summary\\n\\n"
            "## Findings\\n\\n"
            "## Stabilization plan\\n",
            encoding="utf-8",
        )

    findings_path.write_text(
        "# Findings Scratchpad\\n\\n"
        "Use this file to collect raw evidence before synthesizing the final report.\\n",
        encoding="utf-8",
    )
    notes_path.write_text(
        "# Notes\\n\\n"
        "Temporary notes, commands, and observations.\\n",
        encoding="utf-8",
    )

    print(target_dir)
    return 0


if __name__ == "__main__":
    sys.exit(main())
