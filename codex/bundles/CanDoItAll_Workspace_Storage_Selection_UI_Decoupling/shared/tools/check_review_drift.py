#!/usr/bin/env python3
"""Report drift in reviewed files; never fetch, checkout, stage, build, or edit Git configuration."""
from __future__ import annotations

import argparse
import json
import os
from pathlib import Path, PurePosixPath
import re
import subprocess
import sys
from typing import Any

OID = re.compile(r"^[0-9a-f]{40}$")


def git(repo: Path, *args: str, required: bool = True) -> str | None:
    env = dict(os.environ, GIT_OPTIONAL_LOCKS="0")
    try:
        result = subprocess.run(
            ["git", "-C", str(repo), *args], capture_output=True, text=True,
            encoding="utf-8", errors="surrogateescape", timeout=30, env=env, check=False,
        )
    except (OSError, subprocess.TimeoutExpired) as exc:
        raise RuntimeError(f"Git command could not run: {exc}") from exc
    if result.returncode != 0:
        if not required:
            return None
        raise RuntimeError(result.stderr.strip() or f"Git exited {result.returncode}")
    return result.stdout


def object_id(repo: Path, revision: str, path: str) -> str | None:
    value = git(repo, "rev-parse", "--verify", f"{revision}:{path}", required=False)
    value = value.strip() if value else None
    return value if value and OID.fullmatch(value) else None


def status_entries(text: str) -> list[dict[str, str]]:
    parts = text.split("\0")
    entries: list[dict[str, str]] = []
    index = 0
    while index < len(parts):
        entry = parts[index]
        index += 1
        if not entry:
            continue
        if len(entry) < 4 or entry[2] != " ":
            raise ValueError("Unexpected Git porcelain status format")
        value = {"status": entry[:2], "path": entry[3:]}
        if "R" in entry[:2] or "C" in entry[:2]:
            if index >= len(parts) or not parts[index]:
                raise ValueError("Incomplete Git rename/copy status")
            value["original_path"] = parts[index]
            index += 1
        entries.append(value)
    return entries


def checked_path(value: Any) -> str:
    if not isinstance(value, str) or not value or "\\" in value or ":" in value:
        raise ValueError("Invalid repository path in source register")
    path = PurePosixPath(value)
    if path.is_absolute() or ".." in path.parts or ".git" in path.parts:
        raise ValueError(f"Unsafe repository path: {value!r}")
    return value


def report(repo: Path, register: dict[str, Any]) -> dict[str, Any]:
    repo = repo.resolve()
    top = git(repo, "rev-parse", "--show-toplevel")
    if not top:
        raise ValueError("No Git repository root found")
    repo = Path(top.strip()).resolve()
    head = (git(repo, "rev-parse", "--verify", "HEAD") or "").strip()
    branch = git(repo, "symbolic-ref", "--quiet", "--short", "HEAD", required=False)
    statuses = status_entries(git(repo, "status", "--porcelain=v1", "-z", "--untracked-files=normal") or "")
    changed_paths = {e["path"] for e in statuses} | {e["original_path"] for e in statuses if "original_path" in e}
    baseline = register.get("review_commit")
    if not isinstance(baseline, str) or not OID.fullmatch(baseline):
        raise ValueError("Source register has no valid review commit")
    rows: list[dict[str, Any]] = []
    source_list = register.get("sources")
    if not isinstance(source_list, list):
        raise ValueError("Source register sources must be a list")
    for source in source_list:
        if source.get("kind") != "repository-file":
            continue
        path = checked_path(source.get("path"))
        expected = source.get("observed_blob_sha")
        if expected is not None and (not isinstance(expected, str) or not OID.fullmatch(expected)):
            raise ValueError(f"Invalid observed blob for {path}")
        # A shallow checkout need not contain the old commit. Its absence is not a checkout request.
        expected_origin = "register" if expected else "local-review-commit"
        if expected is None:
            expected = object_id(repo, baseline, path)
        actual = object_id(repo, "HEAD", path)
        if actual is None:
            state = "missing-at-head"
        elif expected is None:
            state = "baseline-unavailable"
        elif actual == expected:
            state = "same-blob"
        else:
            state = "changed-blob"
        rows.append({
            "source_id": source.get("id"), "path": path, "review_blob": expected,
            "review_blob_origin": expected_origin if expected else None,
            "head_blob": actual, "state": state, "index_or_worktree_dirty": path in changed_paths,
        })
    return {
        "repository_root": str(repo), "branch": branch.strip() if branch else None,
        "head": head, "review_commit": baseline, "head_equals_review": head == baseline,
        "execution_pin_required": False, "dirty": bool(statuses), "git_status": statuses,
        "reviewed_files": rows,
        "files_requiring_attention": sum(r["state"] != "same-blob" or r["index_or_worktree_dirty"] for r in rows),
        "limitations": [
            "Only registered repository files are compared; new modules and unreviewed consumers need fresh discovery.",
            "Matching Git blobs do not prove a build graph, runtime behavior, sibling compatibility or test success.",
            "Dirty files must be read in their current working-tree form; HEAD comparison does not certify local edits.",
            "Unchanged baseline history is not required. No network access or Git write was attempted.",
        ],
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, required=True, help="CanDoItAll checkout or a directory inside it")
    parser.add_argument("--register", type=Path, default=Path(__file__).resolve().parents[1] / "audit/source-register.json")
    args = parser.parse_args()
    try:
        register = json.loads(args.register.read_text(encoding="utf-8"))
        result = report(args.repo, register)
        print(json.dumps(result, indent=2, ensure_ascii=True))
    except (OSError, ValueError, RuntimeError, TypeError, AttributeError) as exc:
        print(f"Review drift check failed: {exc}", file=sys.stderr)
        return 2
    return 0  # Drift is an informational report, never a demand to revert to the review SHA.


if __name__ == "__main__":
    raise SystemExit(main())
