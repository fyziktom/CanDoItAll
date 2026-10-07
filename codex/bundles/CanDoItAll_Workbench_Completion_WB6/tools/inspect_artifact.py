#!/usr/bin/env python3
"""Read-only inspection of exported demo artifacts. Does not evaluate formulas or prove provenance."""
from __future__ import annotations

import argparse
import hashlib
import json
import posixpath
import re
import sys
import warnings
import zipfile
from pathlib import Path
from xml.etree import ElementTree as ET

MAX_FILE = 32 * 1024 * 1024
MAX_EXPANDED = 64 * 1024 * 1024
MAX_ENTRIES = 2048
SVG = "http://www.w3.org/2000/svg"
SS = "http://schemas.openxmlformats.org/spreadsheetml/2006/main"
REL = "http://schemas.openxmlformats.org/officeDocument/2006/relationships"
PKG = "http://schemas.openxmlformats.org/package/2006/relationships"


def parse_xml(data: bytes) -> ET.Element:
    if len(data) > MAX_EXPANDED:
        raise ValueError("XML input is too large")
    # Remove NUL bytes only for declaration detection in UTF-16/32 XML.
    declaration_probe = data.replace(b"\x00", b"").upper()
    if b"<!DOCTYPE" in declaration_probe or b"<!ENTITY" in declaration_probe:
        raise ValueError("DTD and entity declarations are not permitted")
    try:
        return ET.fromstring(data)
    except ET.ParseError as exc:
        raise ValueError(f"Invalid XML: {exc}") from exc


def inspect_svg(data: bytes) -> dict:
    root = parse_xml(data)
    if root.tag != f"{{{SVG}}}svg":
        raise ValueError("Expected one SVG root in the SVG namespace")
    identifiers: set[str] = set()
    references: set[str] = set()
    counts: dict[str, int] = {}
    text: list[str] = []
    for element in root.iter():
        local = element.tag.rsplit("}", 1)[-1]
        if local.lower() in {"script", "foreignobject", "iframe", "object", "embed"}:
            raise ValueError(f"Unsafe SVG element: {local}")
        counts[local] = counts.get(local, 0) + 1
        if local in {"text", "title", "desc"}:
            value = "".join(element.itertext()).strip()
            if value:
                text.append(value)
        if local == "style":
            css = "".join(element.itertext())
            # The demo oracle rejects CSS escapes and imports rather than interpreting CSS.
            if "\\" in css or "@import" in css.lower() or "/*" in css:
                raise ValueError("Unsupported CSS escape/import/comment in SVG")
            for target in re.findall(r"url\(\s*['\"]?([^)'\"\s]+)", css, re.I):
                if not target.startswith("#"):
                    raise ValueError("External SVG CSS reference")
                references.add(target[1:])
        for key, value in element.attrib.items():
            attribute = key.rsplit("}", 1)[-1].lower()
            if attribute.startswith("on") or (key == "{http://www.w3.org/XML/1998/namespace}base"):
                raise ValueError(f"Unsafe SVG attribute: {attribute}")
            if attribute == "id":
                if not value or value in identifiers:
                    raise ValueError("Empty or duplicate SVG ID")
                identifiers.add(value)
            if attribute in {"href", "src"}:
                if not value.startswith("#"):
                    raise ValueError("External SVG resource reference")
                references.add(value[1:])
            if attribute == "style" and ("\\" in value or "@import" in value.lower() or "/*" in value):
                raise ValueError("Unsupported inline CSS in SVG")
            for target in re.findall(r"url\(\s*['\"]?([^)'\"\s]+)", value, re.I):
                if not target.startswith("#"):
                    raise ValueError("External SVG resource URL")
                references.add(target[1:])
    missing = sorted(references - identifiers)
    if missing:
        raise ValueError("Broken SVG references: " + ", ".join(missing))
    return {"kind": "svg", "view_box": root.get("viewBox"), "elements": counts,
            "text": text[:200], "ids": sorted(identifiers), "safe_demo_subset": True,
            "limitations": "Strict demo inspection, not a general SVG sanitizer or rendering proof"}


def inspect_xlsx(path: Path) -> dict:
    try:
        archive = zipfile.ZipFile(path)
    except zipfile.BadZipFile as exc:
        raise ValueError("Not an OpenXML ZIP workbook") from exc
    with archive:
        infos = archive.infolist()
        if len(infos) > MAX_ENTRIES or sum(x.file_size for x in infos) > MAX_EXPANDED:
            raise ValueError("Workbook expansion budget exceeded")
        names = [x.filename for x in infos]
        if len(names) != len(set(names)):
            raise ValueError("Duplicate workbook ZIP entries")
        for info in infos:
            normalized = posixpath.normpath(info.filename)
            if info.flag_bits & 1 or info.filename.startswith("/") or "\\" in info.filename or normalized.startswith("../"):
                raise ValueError("Encrypted or unsafe workbook package entry")
        nameset = set(names)
        required = {"[Content_Types].xml", "_rels/.rels", "xl/workbook.xml", "xl/_rels/workbook.xml.rels"}
        if not required <= nameset:
            raise ValueError("Required OpenXML workbook parts are missing")
        if any("vbaproject" in name.lower() or name.lower().startswith("xl/externallinks/") or name.lower() == "xl/connections.xml" for name in names):
            raise ValueError("Macros or external workbook connections are not allowed in demo files")
        for name in names:
            if name.endswith(".rels"):
                relations = parse_xml(archive.read(name))
                for relation in relations:
                    if relation.get("TargetMode", "").lower() == "external":
                        raise ValueError("External relationship in workbook")
        workbook = parse_xml(archive.read("xl/workbook.xml"))
        if workbook.tag != f"{{{SS}}}workbook":
            raise ValueError("Unsupported or missing SpreadsheetML workbook root")
        rels = parse_xml(archive.read("xl/_rels/workbook.xml.rels"))
        targets: dict[str, str] = {}
        for relation in rels:
            target = relation.get("Target", "")
            joined = posixpath.normpath(target.lstrip("/") if target.startswith("/") else posixpath.join("xl", target))
            if joined not in nameset:
                raise ValueError("Workbook relationship points to a missing part")
            targets[relation.get("Id", "")] = joined
        strings: list[str] = []
        if "xl/sharedStrings.xml" in nameset:
            for entry in parse_xml(archive.read("xl/sharedStrings.xml")):
                strings.append("".join(x.text or "" for x in entry.iter(f"{{{SS}}}t")))
        sheets: list[dict] = []
        seen_names: set[str] = set()
        formula_total = 0
        no_cache = 0
        for sheet in workbook.findall(f"{{{SS}}}sheets/{{{SS}}}sheet"):
            name = sheet.get("name", "")
            if not name or name in seen_names:
                raise ValueError("Invalid or duplicate worksheet name")
            seen_names.add(name)
            rid = sheet.get(f"{{{REL}}}id", "")
            if rid not in targets:
                raise ValueError("Worksheet has no relationship")
            root = parse_xml(archive.read(targets[rid]))
            if root.tag != f"{{{SS}}}worksheet":
                raise ValueError("Unsupported worksheet type in demo workbook")
            cells: list[dict] = []
            addresses: set[str] = set()
            count = 0
            for cell in root.iter(f"{{{SS}}}c"):
                count += 1
                if count > 10000:
                    raise ValueError("Demo worksheet cell budget exceeded")
                address = cell.get("r", "")
                if not re.fullmatch(r"[A-Z]{1,3}[1-9][0-9]{0,6}", address) or address in addresses:
                    raise ValueError("Invalid or duplicate cell address")
                addresses.add(address)
                kind = cell.get("t", "n")
                value = cell.findtext(f"{{{SS}}}v")
                formula_element = cell.find(f"{{{SS}}}f")
                formula = None if formula_element is None else formula_element.text or ""
                if kind == "e":
                    raise ValueError(f"Formula/error cell at {name}!{address}: {value}")
                if kind == "s":
                    if value is None or not value.isdigit() or int(value) >= len(strings):
                        raise ValueError("Invalid shared string index")
                    value = strings[int(value)]
                elif kind == "inlineStr":
                    value = "".join(x.text or "" for x in cell.iter(f"{{{SS}}}t"))
                if formula is not None:
                    formula_total += 1
                    no_cache += int(value in (None, ""))
                cells.append({"address": address, "type": kind, "value": value, "formula": formula,
                              "formula_attributes": dict(formula_element.attrib) if formula_element is not None else None})
            dimension = root.find(f"{{{SS}}}dimension")
            sheets.append({"name": name, "dimension": None if dimension is None else dimension.get("ref"), "cell_count": count, "cells": cells})
        if not sheets:
            raise ValueError("Workbook contains no worksheets")
        return {"kind": "xlsx", "sheets": sheets, "formula_count": formula_total,
                "formulas_without_cached_values": no_cache, "formulas_evaluated_by_this_tool": False,
                "limitations": "Reads stored formulas and caches; cache correctness and actual rendering require separate proof"}


def inspect_raster(path: Path) -> dict:
    try:
        from PIL import Image
    except ImportError as exc:
        raise ValueError("Pillow is required for actual raster decoding") from exc
    with warnings.catch_warnings():
        warnings.simplefilter("error", Image.DecompressionBombWarning)
        with Image.open(path) as image:
            image.verify()
        with Image.open(path) as image:
            if image.width <= 0 or image.height <= 0 or image.width * image.height > 25_000_000:
                raise ValueError("Raster dimensions exceed demo bounds")
            image.load()
            if image.format not in {"PNG", "JPEG", "WEBP", "GIF"}:
                raise ValueError("Unsupported raster format")
            return {"kind": "raster", "format": image.format, "width": image.width,
                    "height": image.height, "mode": image.mode, "decoded": True}


def inspect(path: Path) -> dict:
    path = path.resolve(strict=True)
    if not path.is_file() or path.stat().st_size > MAX_FILE:
        raise ValueError("File missing or too large for bounded demo inspection")
    data = path.read_bytes()
    if path.suffix.lower() == ".svg":
        result = inspect_svg(data)
    elif path.suffix.lower() == ".xlsx":
        result = inspect_xlsx(path)
    elif path.suffix.lower() in {".png", ".jpg", ".jpeg", ".webp", ".gif"}:
        result = inspect_raster(path)
    else:
        raise ValueError("Supported extensions: SVG, XLSX, PNG, JPEG, WebP and GIF")
    return {"file_name": path.name, "length": len(data), "sha256": hashlib.sha256(data).hexdigest(),
            "producer_proven": False, **result}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("path", type=Path)
    parser.add_argument("--output", type=Path, help="Private JSON report; never overwrite the input")
    args = parser.parse_args()
    try:
        if args.output and args.output.resolve() == args.path.resolve():
            raise ValueError("The report cannot overwrite the artifact")
        result = inspect(args.path)
        text = json.dumps(result, ensure_ascii=False, indent=2) + "\n"
        if args.output:
            args.output.write_text(text, encoding="utf-8")
        else:
            print(text, end="")
        return 0
    except Exception as exc:
        print(f"Artifact inspection failed: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
