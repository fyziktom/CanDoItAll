"""Strict UTF-8 audit for an explicit Git-backed text scope."""

import argparse
import codecs
import json
import re
from pathlib import Path

from validate_bundle_delivery import Inventory, git


BINARY_SUFFIXES = frozenset(".png .jpg .jpeg .gif .webp .ico .pdf .zip .gz .7z .nupkg .snupkg .dll .exe .pdb .woff .woff2 .ttf .eot .mp3 .mp4 .wav .xlsx .docx .pptx .bin .db .sqlite".split())
CP1252_PUNCTUATION = r"\u20ac\u201a\u0192\u201e\u2026\u2020\u2021\u02c6\u2030\u0160\u2039\u0152\u017d\u2018\u2019\u201c\u201d\u2022\u2013\u2014\u02dc\u2122\u0161\u203a\u0153\u017e\u0178"
MOJIBAKE = re.compile(
    rf"[\u00c2\u00c3][\u0080-\u00bf{CP1252_PUNCTUATION}]|"
    r"\u00e2[\u0080\u20ac\u201a]|\u0102[\u02d8\u02db\u02dd\u02c7\u013d\u015f]|"
    r"\u02d8\u00e2|\u201a\u00ac")


def inspect_bytes(path, data, previous=None):
    if Path(path).suffix.lower() in BINARY_SUFFIXES:
        return []
    try:
        text = data.decode("utf-8-sig", errors="strict")
    except UnicodeDecodeError as error:
        return [{"kind": "invalid-utf8", "offset": error.start}]
    findings = []
    if previous is not None and data.startswith(codecs.BOM_UTF8) != previous.startswith(codecs.BOM_UTF8):
        findings.append({"kind": "bom-change", "line": 1})
    for number, line in enumerate(text.split("\n"), 1):
        if "\ufffd" in line:
            findings.append({"kind": "replacement-character", "line": number})
        if any("\u0080" <= c <= "\u009f" for c in line):
            findings.append({"kind": "c1-control", "line": number})
        if "\0" in line:
            findings.append({"kind": "nul-in-text", "line": number})
        if MOJIBAKE.search(line):
            findings.append({"kind": "mojibake", "line": number})
    return findings


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=Path, default=Path.cwd())
    parser.add_argument("--scope", action="append", default=[])
    parser.add_argument("--changed-since")
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    inventory = Inventory(args.repo_root)
    paths = {p for p in inventory.paths if any(p == scope or p.startswith(scope.rstrip("/") + "/") for scope in args.scope)}
    if args.changed_since:
        paths.update(p.decode() for p in git(args.repo_root, "diff", "--name-only", "-z", args.changed_since).split(b"\0") if p)
    head = Inventory(args.repo_root, "HEAD")
    changed = {p.decode() for p in git(args.repo_root, "diff", "HEAD", "--name-only", "-z").split(b"\0") if p}
    findings = []
    scanned = 0
    for path in sorted(paths & inventory.paths):
        data = inventory.read(path)
        previous = head.read(path) if path in changed and path in head.paths else b"" if path not in head.paths else None
        scanned += Path(path).suffix.lower() not in BINARY_SUFFIXES
        findings.extend({"path": path, **finding} for finding in inspect_bytes(path, data, previous))
    report = {"scannedTextFiles": scanned, "findings": findings, "passed": not findings}
    result = json.dumps(report, ensure_ascii=True, indent=2) + "\n"
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(result, encoding="utf-8", newline="\n")
    print(result)
    return 0 if not findings else 1


if __name__ == "__main__":
    raise SystemExit(main())
