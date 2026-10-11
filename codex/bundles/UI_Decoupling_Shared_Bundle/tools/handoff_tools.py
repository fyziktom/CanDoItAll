#!/usr/bin/env python3
"""Read-only package verification and Git source/candidate inspection.

These checks do not build, run, authenticate, or certify the C# application.
An explicitly requested report file is created exclusively; existing files are not overwritten.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import re
import subprocess
import sys
from pathlib import Path, PurePosixPath
from typing import Any

MANIFEST_NAME = "MANIFEST.json"
MAX_SOURCE_BYTES = 2 * 1024 * 1024


def canonical_bytes(path: Path) -> bytes:
    """Allow CRLF text checkouts; do not normalize other meaningful whitespace."""
    return path.read_bytes().decode("utf-8").replace("\r\n", "\n").encode("utf-8")


def digest(path: Path) -> str:
    return hashlib.sha256(canonical_bytes(path)).hexdigest()


def safe_relative(value: str) -> PurePosixPath:
    if not isinstance(value, str) or not value or "\\" in value or ":" in value or "\x00" in value:
        raise ValueError("Invalid relative path in input inventory.")
    p = PurePosixPath(value)
    if p.is_absolute() or ".." in p.parts or value != p.as_posix() or value == ".":
        raise ValueError("Unsafe or non-canonical relative path: " + value)
    return p


def load_json(path: Path) -> dict[str, Any]:
    obj = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(obj, dict):
        raise ValueError("Expected a JSON object: " + str(path))
    return obj


def verify_package(root: Path, shared: Path | None = None) -> list[str]:
    """Return integrity issues. Hashes prove consistency, not origin or execution."""
    if root.is_symlink():
        return ["Package root is a symbolic link."]
    root = root.resolve(strict=True)
    manifest = load_json(root / MANIFEST_NAME)
    entries = manifest.get("files")
    if not isinstance(entries, dict) or not entries:
        raise ValueError("Manifest must contain a non-empty files object.")
    expected: set[str] = set()
    errors: list[str] = []
    for name, expected_digest in entries.items():
        rel = safe_relative(name)
        if name == MANIFEST_NAME:
            raise ValueError("Manifest cannot include its own digest.")
        if not isinstance(expected_digest, str) or not re.fullmatch(r"[0-9a-f]{64}", expected_digest):
            raise ValueError("Invalid digest for " + name)
        expected.add(name)
        file = root.joinpath(*rel.parts)
        if file.is_symlink() or any(parent.is_symlink() for parent in file.parents if parent != root and root in parent.parents):
            errors.append("Symbolic link is not allowed: " + name)
            continue
        if not file.is_file():
            errors.append("Missing file: " + name)
            continue
        if not file.resolve().is_relative_to(root):
            errors.append("File escapes package root: " + name)
            continue
        if digest(file) != expected_digest:
            errors.append("Digest mismatch: " + name)
    actual: set[str] = set()
    for file in root.rglob("*"):
        rel = file.relative_to(root).as_posix()
        if file.is_symlink():
            errors.append("Symbolic link is not allowed: " + rel)
        elif file.is_file() and rel != MANIFEST_NAME:
            actual.add(rel)
    errors.extend("Unlisted file: " + path for path in sorted(actual - expected))
    metadata = load_json(root / "bundle.json")
    requirement = metadata.get("shared_requirement")
    if requirement:
        if not isinstance(requirement, dict):
            raise ValueError("Invalid shared_requirement.")
        if shared is None:
            errors.append("This child requires --shared pointing to the delivered companion.")
        else:
            if shared.resolve() == root:
                errors.append("The child cannot be its own shared companion.")
            else:
                shared_errors = verify_package(shared)
                errors.extend("Shared: " + error for error in shared_errors)
                shared_metadata = load_json(shared / "bundle.json")
                if shared_metadata.get("id") != requirement.get("id"):
                    errors.append("Shared companion ID mismatch.")
                if digest(shared / MANIFEST_NAME) != requirement.get("manifest_sha256"):
                    errors.append("Shared companion manifest digest mismatch.")
    return sorted(set(errors))


def git(repo: Path, *args: str, allow_failure: bool = False) -> str:
    result = subprocess.run(["git", "-C", str(repo), *args], capture_output=True, timeout=30, check=False)
    if result.returncode and not allow_failure:
        # Do not echo stderr or command arguments that might contain private paths/configuration.
        raise RuntimeError("Git inspection failed with exit code " + str(result.returncode))
    return result.stdout.decode("utf-8", errors="replace") if result.returncode == 0 else ""


def git_names(repo: Path, *args: str) -> list[str]:
    return [name for name in git(repo, *args).split("\x00") if name]


def inspect_repository(repo: Path, register: dict[str, Any]) -> dict[str, Any]:
    """Inspect tracked/untracked paths and selected blobs without changing repository state."""
    root = Path(git(repo, "rev-parse", "--show-toplevel").strip()).resolve(strict=True)
    head = git(root, "rev-parse", "HEAD").strip()
    tree = git(root, "rev-parse", "HEAD^{tree}").strip()
    branch = git(root, "symbolic-ref", "--short", "-q", "HEAD", allow_failure=True).strip() or "(detached)"
    changed = set(git_names(root, "diff", "--name-only", "-z", "HEAD", "--"))
    untracked = set(git_names(root, "ls-files", "--others", "--exclude-standard", "-z"))
    tracked = set(git_names(root, "ls-files", "-z"))
    rows: list[dict[str, Any]] = []
    seen: set[str] = set()
    entries = register.get("sources")
    if not isinstance(entries, list):
        raise ValueError("Source register must contain a sources array.")
    for entry in entries:
        if not isinstance(entry, dict):
            raise ValueError("Invalid source entry.")
        path = safe_relative(entry.get("path", "")).as_posix()
        if path in seen:
            continue
        seen.add(path)
        expected = entry.get("blob_sha")
        if not isinstance(expected, str) or not re.fullmatch(r"[0-9a-f]{40}", expected):
            raise ValueError("Invalid reviewed Git blob for " + path)
        current = git(root, "rev-parse", "--verify", head + ":" + path, allow_failure=True).strip() or None
        rows.append({"path": path, "review_blob": expected, "head_blob": current,
                     "head_differs": current != expected, "working_tree_changed": path in changed,
                     "untracked": path in untracked, "coverage": entry.get("coverage")})
    inventory: list[dict[str, Any]] = []
    for path in sorted(tracked | untracked):
        if not path.startswith("src/") or not path.endswith((".razor", ".razor.cs", ".razor.css")):
            continue
        relative = safe_relative(path)
        file = root.joinpath(*relative.parts)
        item: dict[str, Any] = {"path": path, "tracked": path in tracked,
                                "in_review_register": path in seen, "working_tree_changed": path in changed}
        if file.is_symlink() or not file.is_file() or not file.resolve().is_relative_to(root):
            item["inspection"] = "not-read-missing-or-symlink"
            inventory.append(item)
            continue
        with file.open("rb") as stream:
            raw = stream.read(MAX_SOURCE_BYTES + 1)
        item["scan_truncated"] = len(raw) > MAX_SOURCE_BYTES
        text = raw[:MAX_SOURCE_BYTES].decode("utf-8", errors="replace")
        item["inspection"] = "mechanical-candidate-only"
        item["routes"] = re.findall(r'^\s*@page\s+"([^"\r\n]+)"', text, re.MULTILINE)
        item["injection_directives"] = len(re.findall(r"^\s*@inject\b", text, re.MULTILINE))
        item["review_markers"] = [marker for marker in ("IServiceProvider", "GetRequiredService", "DbContext", "IQueryable", "ProcessStartInfo", "RenderFragment", "IJSRuntime") if marker in text]
        inventory.append(item)
    review_commit = register.get("review_commit")
    delta = None
    if isinstance(review_commit, str) and re.fullmatch(r"[0-9a-f]{40}", review_commit):
        exists = git(root, "rev-parse", "--verify", review_commit + "^{commit}", allow_failure=True).strip()
        if exists:
            delta = git(root, "diff", "--name-status", review_commit, head, "--", "src", "tests", "Directory.Build.props", "Directory.Build.targets", ".github", "package.json")
    return {"schema_version": 1, "repository_root": str(root), "branch": branch, "head": head,
            "tree": tree, "review_commit": review_commit, "review_to_head_name_delta": delta,
            "registered_sources": rows, "working_tree_changed_paths": sorted(changed),
            "untracked_paths": sorted(untracked), "surface_candidates": inventory,
            "drift_detected": any(row["head_differs"] for row in rows) or bool(changed) or bool(untracked),
            "limits": ["Mechanical candidate inventory only, not semantic reachability or an evaluated dependency graph.",
                       "Unregistered does not mean newly introduced or architecturally wrong; the review register is partial.",
                       "Ignored/generated source and sibling repositories require separate current inspection.",
                       "No CodeAnalytics invocation, build, test, browser, database or runtime verification is performed.",
                       "Drift is a review input, never a checkout/reset instruction."]}


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    verify = commands.add_parser("verify", help="Check LF-normalized text integrity and companion compatibility.")
    verify.add_argument("root", type=Path)
    verify.add_argument("--shared", type=Path)
    inspect = commands.add_parser("inspect", help="Read current Git drift and renderer candidate metadata.")
    inspect.add_argument("--repo", type=Path, required=True)
    inspect.add_argument("--sources", type=Path, required=True)
    inspect.add_argument("--output", type=Path)
    args = parser.parse_args(argv)
    try:
        if args.command == "verify":
            errors = verify_package(args.root, args.shared)
            print(json.dumps({"package_integrity": "FAIL" if errors else "PASS", "errors": errors,
                              "product_validation": "NOT_RUN"}, indent=2))
            return 1 if errors else 0
        report = inspect_repository(args.repo, load_json(args.sources))
        serialized = json.dumps(report, indent=2, ensure_ascii=False) + "\n"
        if args.output is None:
            print(serialized, end="")
        else:
            # The caller must choose an owned directory; never overwrite an existing report.
            with args.output.open("x", encoding="utf-8") as stream:
                stream.write(serialized)
            print(json.dumps({"report_created": str(args.output), "drift_detected": report["drift_detected"],
                              "product_validation": "NOT_RUN"}, indent=2))
        return 3 if report["drift_detected"] else 0
    except (OSError, ValueError, RuntimeError, subprocess.TimeoutExpired) as error:
        print(json.dumps({"tool_error": type(error).__name__, "message": str(error),
                          "product_validation": "NOT_RUN"}, indent=2), file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
