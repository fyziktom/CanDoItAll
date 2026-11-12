#!/usr/bin/env python3
"""Produce a read-only source census for a local CanDoItAll Git checkout.

This heuristic census is not a call graph, a concurrency proof, or test execution.
Source contents and configuration values are never emitted. Reports contain paths,
line numbers, signal names and qualified source-reference checks only.
"""
from __future__ import annotations

import argparse
from bisect import bisect_left
import json
import re
import subprocess
import sys
import tempfile
from pathlib import Path
from typing import Any

SCAN_ROOTS = ("src/App/", "src/Modules/", "src/UI/", "src/Foundation/", "src/MAF/", "src/Processes/", "src/Memory/")
EXTENSIONS = {".cs", ".razor", ".cshtml"}
MAX_BYTES = 2 * 1024 * 1024
SIGNALS = {
    "route": re.compile(r"^\s*@page\s+", re.MULTILINE),
    "form": re.compile(r"<\s*(?:EditForm|form)\b|\bEditContext\b"),
    "input": re.compile(r"<\s*(?:Input\w*|TextArea|TextBox|DropDown|Numeric|DatePicker|RichText\w*)\b"),
    "overlay": re.compile(r"<\s*(?:\w*(?:Dialog|Modal|FloatingWindow|OverlayWindow)\w*)\b|\bDialogService\b"),
    "mutation_candidate": re.compile(r"\b(?:Save|Update|Replace|Delete|Upsert|Archive|Publish|Import|Set)[A-Z]\w*Async\s*\("),
    "dynamic_renderer": re.compile(r"\bDynamicComponent\b|\bISettingsRendererSource\b|\bRenderFragment\b"),
    "concurrency_hint": re.compile(r"\b(?:Expected\w+|ConcurrencyToken|DbUpdateConcurrencyException|Revision|Observation)\b"),
    "change_observation": re.compile(r"\b(?:\w*ChangeFeed\w*|\w*Subscription\w*|\w*Invalidation\w*)\b"),
    "test_symbol": re.compile(r"\b(?:Fact|Theory|TestMethod)\b"),
}


def git(repo: Path, *args: str, check: bool = True) -> subprocess.CompletedProcess[bytes]:
    result = subprocess.run(["git", "-C", str(repo), *args], capture_output=True, check=False)
    if check and result.returncode:
        # Do not echo configuration or arbitrary command output in an exception.
        raise RuntimeError(f"Git command failed: {' '.join(args[:2])}; exit={result.returncode}")
    return result


def path_matches(candidate: str, entry: str) -> bool:
    entry = entry.replace("\\", "/").rstrip("/")
    if "*" in entry or "…" in entry or ";" in entry:
        return False
    return candidate == entry or candidate.startswith(entry + "/")


def collect(repo: Path, package: Path, components_repo: Path | None = None) -> dict[str, Any]:
    head = git(repo, "rev-parse", "HEAD").stdout.decode().strip()
    tracked = git(repo, "ls-files", "-z").stdout.decode("utf-8", errors="replace").split("\0")
    untracked = git(repo, "ls-files", "--others", "--exclude-standard", "-z").stdout.decode("utf-8", errors="replace").split("\0")
    names = sorted(set(filter(None, tracked + untracked)))
    source_manifest = json.loads((package / "evidence/sources.json").read_text(encoding="utf-8"))
    inventory = json.loads((package / "inventory/surfaces.json").read_text(encoding="utf-8"))["rows"]
    records: list[dict[str, Any]] = []
    skipped: list[dict[str, str]] = []
    candidates: list[dict[str, Any]] = []
    for name in names:
        if not name.startswith(SCAN_ROOTS) or Path(name).suffix not in EXTENSIONS:
            continue
        path = repo / name
        if path.is_symlink() or not path.resolve().is_relative_to(repo):
            skipped.append({"path": name, "reason": "symlink_or_outside_root"})
            continue
        if not path.is_file():
            skipped.append({"path": name, "reason": "missing_worktree_file"})
            continue
        if path.stat().st_size > MAX_BYTES:
            skipped.append({"path": name, "reason": "source_exceeds_scan_bound"})
            continue
        try:
            text = path.read_text(encoding="utf-8-sig")
        except (OSError, UnicodeError):
            skipped.append({"path": name, "reason": "unreadable_or_not_utf8"})
            continue
        # Only signal names and line locations are retained, never matched text.
        newline_offsets = [index for index, character in enumerate(text) if character == "\n"]
        signals = {
            label: sorted({bisect_left(newline_offsets, match.start()) + 1 for match in pattern.finditer(text)})
            for label, pattern in SIGNALS.items()
        }
        signals = {label: locations for label, locations in signals.items() if locations}
        hints = [row["id"] for row in inventory
                 if any(path_matches(name, entry) for entry in [row["ui_entry"], *row["owner_entries"]])]
        record = {"path": name, "signals": signals, "inventory_path_hints": hints,
                  "review_status": "UNREVIEWED", "classification_is_proof": False}
        records.append(record)
        if signals:
            candidates.append(record)

    source_paths: list[dict[str, Any]] = []
    for source in source_manifest["sources"]:
        owner = source["repository"]
        source_repo = repo if owner == "fyziktom/CanDoItAll" else components_repo if owner == "fyziktom/CanDoItAll.Components" else None
        status: dict[str, Any] = {"source_id": source["id"], "repository": owner, "path": source["path"]}
        if source_repo is None:
            status["status"] = "SIBLING_NOT_PROVIDED"
        else:
            candidate = source_repo / source["path"]
            status["status"] = "EXISTS_IN_WORKTREE" if candidate.is_file() else "MISSING_OR_MOVED_IN_WORKTREE"
            probe = git(source_repo, "cat-file", "-e", f"{source['commit']}:{source['path']}", check=False)
            status["baseline_object_available_locally"] = probe.returncode == 0
            status["note"] = "Existence is not content review; absent baseline objects may reflect a shallow clone."
        source_paths.append(status)
    return {
        "scope": "Heuristic read-only local source census. No build, tests, network, or complete writer closure.",
        "head": head,
        "briefing_baseline": source_manifest["baseline"]["commit"],
        "worktree_has_changes": bool(git(repo, "status", "--porcelain", "-z").stdout),
        "scanned_source_count": len(records),
        "candidate_count": len(candidates),
        "skipped": skipped,
        "source_path_checks": source_paths,
        "candidates": candidates,
        "all_scanned_paths": [row["path"] for row in records],
        "limitations": [
            "Regex signals include false positives and cannot prove absence of editors or writes.",
            "Dynamic slots, generated code, aliases, indirect effects and external writers need CodeAnalytics/manual review.",
            "Concurrency-related symbols do not prove original-state conditions at the authoritative write boundary.",
            "Match each candidate to an inventory row or an explicit reviewed exclusion; do not auto-close coverage.",
            "Untracked nonignored source is included; configuration and secret files are not scanned or printed.",
        ],
    }


def self_test(package: Path) -> dict[str, Any]:
    with tempfile.TemporaryDirectory(prefix="candoitall-census-test-") as temporary:
        repo = Path(temporary).resolve()
        git(repo, "init", "-q")
        files = {
            "src/Modules/Fixture/Editor.razor": '@page "/fixture"\n<EditForm Model="draft"><InputText /></EditForm>\n<FixtureDialog />\n',
            "src/Modules/Fixture/Owner.cs": "class Owner { Task SaveRecordAsync() { return default!; } }\n",
            "src/UI/Fixture/Dynamic.razor": "<DynamicComponent Type=\"type\" />\n",
            "src/App/Fixture/appsettings.json": '{"secret": "SYNTHETIC_VALUE_MUST_NOT_APPEAR"}',
            "README.md": "Temporary synthetic fixture only.\n",
        }
        for name, text in files.items():
            path = repo / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(text, encoding="utf-8")
        git(repo, "add", ".")
        git(repo, "-c", "user.name=Census Test", "-c", "user.email=census@example.invalid", "-c", "commit.gpgsign=false", "commit", "-qm", "Synthetic fixture")
        before = {name: (repo / name).read_bytes() for name in files}
        report = collect(repo, package)
        by_path = {row["path"]: row for row in report["candidates"]}
        assert report["scanned_source_count"] == 3, "Only source files should be scanned"
        assert {"route", "form", "input", "overlay"} <= set(by_path["src/Modules/Fixture/Editor.razor"]["signals"])
        assert "mutation_candidate" in by_path["src/Modules/Fixture/Owner.cs"]["signals"]
        assert "dynamic_renderer" in by_path["src/UI/Fixture/Dynamic.razor"]["signals"]
        assert "SYNTHETIC_VALUE_MUST_NOT_APPEAR" not in json.dumps(report)
        assert all((repo / name).read_bytes() == content for name, content in before.items())
        assert not git(repo, "status", "--porcelain").stdout, "Census must not mutate source"
        return {"status": "PASS", "scope": "Synthetic census-tool fixture only; not the real repository", "assertion_groups": 7}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("repo", nargs="?", type=Path, help="Local CanDoItAll checkout")
    parser.add_argument("--package", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--components-repo", type=Path)
    parser.add_argument("--out", type=Path, help="New report JSON file; existing files are not overwritten")
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()
    try:
        if args.self_test:
            result = self_test(args.package.resolve())
        else:
            if args.repo is None:
                parser.error("repo is required unless --self-test is selected")
            repo = args.repo.resolve()
            if not repo.is_dir():
                parser.error("Repository directory does not exist")
            result = collect(repo, args.package.resolve(), args.components_repo.resolve() if args.components_repo else None)
        output = json.dumps(result, ensure_ascii=False, indent=2) + "\n"
        if args.out:
            target = args.out.resolve()
            target.parent.mkdir(parents=True, exist_ok=True)
            # Exclusive creation avoids replacing repository source or prior evidence.
            with target.open("x", encoding="utf-8") as stream:
                stream.write(output)
            print(f"Report created: {target}")
        else:
            print(output, end="")
        return 0
    except (OSError, ValueError, RuntimeError, AssertionError) as exc:
        print(f"Census failed: {type(exc).__name__}: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
