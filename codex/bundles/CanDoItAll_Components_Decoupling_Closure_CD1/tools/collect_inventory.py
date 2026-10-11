#!/usr/bin/env python3
"""Collect a read-only lexical inventory. This is not a semantic boundary validator."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import subprocess
import sys
from typing import Iterable
import xml.etree.ElementTree as ET

MAX_TEXT_BYTES = 4 * 1024 * 1024
RENDER_SIGNALS = ("BuildRenderTree", "RenderFragment", "ComponentBase", "DynamicComponent", "OpenComponent<")
ACTIVATION_SIGNALS = ("DynamicComponent", "OpenAsync<", "OpenDialog", "AddRazorComponents", "AddAdditionalAssemblies", "RenderFragment")
TEXT_EXTENSIONS = {".razor", ".cs", ".csproj", ".props", ".targets", ".css", ".js"}


def safe_path(root: Path, value: str) -> Path:
    """Reject unsafe paths and symlinks instead of following external source."""
    logical = PurePosixPath(value)
    if not value or logical.is_absolute() or ".." in logical.parts or "\\" in value or ":" in value:
        raise ValueError(f"Unsafe repository path: {value!r}")
    candidate = root.joinpath(*logical.parts)
    probe = root
    for part in logical.parts:
        probe /= part
        if probe.is_symlink():
            raise ValueError(f"Symlink requires explicit owner review: {value}")
    if not candidate.resolve().is_relative_to(root.resolve()):
        raise ValueError(f"Path escapes repository: {value}")
    return candidate


def source_role(path: str) -> str:
    if path.startswith("src/Sandboxes/"):
        return "SCENARIO_SOURCE"
    if "/Examples/" in path or "/Samples/" in path:
        return "EXAMPLE_OR_SAMPLE_REQUIRES_CLASSIFICATION"
    return "PRODUCTION_CANDIDATE"


def raw_project_references(text: str) -> list[dict[str, str | None]]:
    """Report unevaluated declarations without treating them as resolved graph edges."""
    element = ET.fromstring(text)
    references: list[dict[str, str | None]] = []
    for node in element.iter():
        tag = node.tag.rsplit("}", 1)[-1]
        if tag in {"ProjectReference", "PackageReference", "Import", "Compile", "RazorComponent"}:
            references.append({"kind": tag, "include": node.get("Include"), "update": node.get("Update"),
                               "remove": node.get("Remove"), "project": node.get("Project"),
                               "condition": node.get("Condition"), "status": "UNEVALUATED"})
    return references


def inspect_text(path: str, text: str) -> dict | None:
    suffix = PurePosixPath(path).suffix.lower()
    if suffix in {".csproj", ".props", ".targets"}:
        return {"kind": "BUILD_INPUT", "raw_declarations": raw_project_references(text), "graph_evaluated": False}
    if suffix == ".razor":
        return {"kind": "RAZOR_IMPORT" if PurePosixPath(path).name == "_Imports.razor" else "RAZOR_CANDIDATE",
                "route_literals": re.findall(r'^\s*@page\s+"([^"\r\n]+)"', text, flags=re.MULTILINE),
                "has_injection_directive": bool(re.search(r"^\s*@inject\s", text, flags=re.MULTILINE)),
                "activation_signals": [term for term in ACTIVATION_SIGNALS if term in text],
                "semantic_status": "UNCLASSIFIED"}
    if suffix == ".cs":
        signals = [term for term in RENDER_SIGNALS + ACTIVATION_SIGNALS if term in text]
        if signals or path.endswith(".razor.cs"):
            return {"kind": "CS_RENDER_OR_ACTIVATION_CANDIDATE", "lexical_signals": sorted(set(signals)),
                    "semantic_status": "UNCLASSIFIED"}
    if suffix in {".css", ".js"}:
        return {"kind": "TEXT_ASSET_INPUT", "served_or_consumed": "NOT_VERIFIED"}
    return None


def collect_from_paths(root: Path, names: Iterable[str], commit: str | None) -> dict:
    entries: list[dict] = []
    issues: list[dict] = []
    seen: set[str] = set()
    for name in sorted(set(names)):
        if name in seen:
            continue
        seen.add(name)
        suffix = PurePosixPath(name).suffix.lower()
        if suffix not in TEXT_EXTENSIONS:
            continue
        if not name.startswith("src/") and suffix not in {".props", ".targets"}:
            continue
        try:
            candidate = safe_path(root, name)
            if not candidate.is_file():
                raise ValueError("Indexed source is missing or not a regular file")
            size = candidate.stat().st_size
            if size > MAX_TEXT_BYTES:
                raise ValueError(f"Text exceeds bounded scan size ({size} bytes)")
            data = candidate.read_bytes()
            text = data.decode("utf-8-sig")
            detail = inspect_text(name, text)
            if detail is not None:
                entries.append({"path": name, "source_role": source_role(name), "bytes": len(data),
                                "sha256": hashlib.sha256(data).hexdigest(), **detail})
        except (OSError, ValueError, UnicodeError, ET.ParseError) as error:
            issues.append({"path": name, "issue": str(error), "disposition": "REQUIRES_REVIEW"})
    return {"schema_version": 1, "tool": "CD1 lexical seed collector", "reviewed_commit": commit,
            "semantic_certification": False, "evaluated_graph": False,
            "coverage": "Git-indexed plus untracked nonignored textual src files and build props/targets; no MSBuild or runtime activation evaluation",
            "required_supplement": ["Evaluated linked/generated items", "Semantic and public type closure", "Dynamic runtime/plugin activation", "Actual package assets and consumers"],
            "entries": entries, "issues": issues}


def git_bytes(root: Path, *args: str) -> bytes:
    result = subprocess.run(["git", "-C", str(root), *args], capture_output=True, timeout=30, check=False)
    if result.returncode:
        raise RuntimeError(result.stderr.decode("utf-8", errors="replace").strip() or "Git command failed")
    return result.stdout


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True, help="A small explicitly selected inventory JSON file")
    args = parser.parse_args(argv)
    try:
        root = Path(git_bytes(args.repo.resolve(), "rev-parse", "--show-toplevel").decode().strip()).resolve()
        try:
            commit = git_bytes(root, "rev-parse", "--verify", "HEAD").decode().strip()
        except RuntimeError:
            commit = None
        raw = git_bytes(root, "ls-files", "--cached", "--others", "--exclude-standard", "-z")
        names = [name.decode("utf-8", errors="strict") for name in raw.split(b"\0") if name]
        result = collect_from_paths(root, names, commit)
        output = args.output.resolve()
        if output.exists():
            raise ValueError("Output already exists; choose a new small proof file or review its replacement explicitly")
        output.parent.mkdir(parents=True, exist_ok=True)
        with output.open("x", encoding="utf-8") as stream:
            json.dump(result, stream, indent=2, ensure_ascii=False)
            stream.write("\n")
        print(f"Collected {len(result['entries'])} lexical entries and {len(result['issues'])} explicit issues. Not a semantic pass.")
        return 2 if result["issues"] else 0
    except (OSError, RuntimeError, ValueError, subprocess.TimeoutExpired) as error:
        print(f"Inventory failed: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
