#!/usr/bin/env python3
"""Materialize portable repository references for an exact CanDoItAll checkout."""

from __future__ import annotations

import argparse
import json
import shutil
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path

PORTABLE_TOKEN = "{{REPO_ROOT}}"
TEXT_SUFFIXES = {
    ".md",
    ".json",
    ".csv",
    ".txt",
    ".py",
    ".ps1",
    ".sh",
    ".yml",
    ".yaml",
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--bundle-root", required=True, type=Path)
    parser.add_argument("--repo-root", required=True, type=Path)
    output = parser.add_mutually_exclusive_group(required=True)
    output.add_argument("--output-root", type=Path)
    output.add_argument("--in-place", action="store_true")
    parser.add_argument("--allow-different-commit", action="store_true")
    parser.add_argument("--overwrite-output", action="store_true")
    parser.add_argument("--skip-validation", action="store_true")
    return parser.parse_args()


def run_git(repo_root: Path, *arguments: str) -> str:
    completed = subprocess.run(
        ["git", "-C", str(repo_root), *arguments],
        check=True,
        capture_output=True,
        text=True,
    )
    return completed.stdout.strip()


def load_json(path: Path) -> dict:
    value = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ValueError(f"Expected a JSON object in {path}")
    return value


def copy_bundle(source: Path, destination: Path, overwrite: bool) -> None:
    if destination.exists():
        if not overwrite:
            raise FileExistsError(
                f"Output root already exists: {destination}. Use --overwrite-output only for a disposable materialized copy."
            )
        if destination.is_symlink():
            raise ValueError("Refusing to remove a symlinked output root.")
        shutil.rmtree(destination)
    shutil.copytree(source, destination, symlinks=True)


def materialize_source_manifests(root: Path, repo_reference: str) -> list[str]:
    changed: list[str] = []
    for path in sorted(root.rglob("source-reference-manifest.json")):
        document = load_json(path)
        references = document.get("references")
        if not isinstance(references, list):
            raise ValueError(f"Source-reference manifest has no references array: {path}")
        for item in references:
            if not isinstance(item, dict):
                continue
            relative_path = str(item.get("relative_path", "")).lstrip("/")
            item["portable_reference"] = f"{repo_reference}/{relative_path}"
        path.write_text(
            json.dumps(document, indent=2, ensure_ascii=False) + "\n",
            encoding="utf-8",
            newline="\n",
        )
        changed.append(path.relative_to(root).as_posix())
    return changed


def materialize_text_files(root: Path, repo_reference: str) -> list[str]:
    changed = materialize_source_manifests(root, repo_reference)
    for path in sorted(root.rglob("*")):
        if not path.is_file() or path.suffix.lower() not in TEXT_SUFFIXES:
            continue
        relative = path.relative_to(root)
        if relative.parts and relative.parts[0] == "scripts":
            continue
        if path.name in {
            "CHECKSUMS.sha256",
            "bundle-index.json",
            "manifest.json",
            "materialization-report.json",
            "source-reference-manifest.json",
        }:
            continue
        try:
            text = path.read_text(encoding="utf-8")
        except UnicodeDecodeError:
            continue
        if PORTABLE_TOKEN not in text:
            continue
        path.write_text(text.replace(PORTABLE_TOKEN, repo_reference), encoding="utf-8", newline="\n")
        changed.append(relative.as_posix())
    return sorted(set(changed))


def main() -> int:
    args = parse_args()
    source = args.bundle_root.expanduser().resolve()
    repo_root = args.repo_root.expanduser().resolve()
    if not source.is_dir():
        print(f"ERROR: bundle root does not exist: {source}", file=sys.stderr)
        return 2
    if not (source / "manifest.json").is_file():
        print("ERROR: bundle root has no manifest.json", file=sys.stderr)
        return 2
    if not (repo_root / "CanDoItAll.slnx").is_file() or not (repo_root / "global.json").is_file():
        print("ERROR: repo root is not a CanDoItAll checkout", file=sys.stderr)
        return 2

    try:
        head = run_git(repo_root, "rev-parse", "HEAD")
        branch = run_git(repo_root, "branch", "--show-current") or "(detached)"
        status = run_git(repo_root, "status", "--short")
    except (subprocess.CalledProcessError, FileNotFoundError) as exc:
        print(f"ERROR: unable to inspect Git checkout: {exc}", file=sys.stderr)
        return 2

    manifest = load_json(source / "manifest.json")
    source_anchor = manifest.get("source_anchor")
    prepared_commit = str(source_anchor.get("commit", "")) if isinstance(source_anchor, dict) else ""
    rebase_required = bool(prepared_commit and head != prepared_commit)
    if rebase_required and not args.allow_different_commit:
        print(
            f"ERROR: repository HEAD {head} differs from prepared anchor {prepared_commit}. "
            "Use the rebase protocol or pass --allow-different-commit to materialize only for re-analysis.",
            file=sys.stderr,
        )
        return 3

    destination = source if args.in_place else args.output_root.expanduser().resolve()
    if not args.in_place:
        try:
            destination.relative_to(source)
        except ValueError:
            pass
        else:
            print("ERROR: output root must not be inside the source bundle root", file=sys.stderr)
            return 2
        try:
            copy_bundle(source, destination, args.overwrite_output)
        except (FileExistsError, OSError, ValueError) as exc:
            print(f"ERROR: unable to create materialized copy: {exc}", file=sys.stderr)
            return 2

    repo_reference = repo_root.as_posix()
    changed_files = materialize_text_files(destination, repo_reference)
    report = {
        "schema_version": 1,
        "materialized_utc": datetime.now(timezone.utc).isoformat(),
        "portable_bundle_source": source.as_posix(),
        "materialized_bundle_root": destination.as_posix(),
        "repository_root": repo_reference,
        "repository_branch": branch,
        "repository_head": head,
        "prepared_anchor": prepared_commit,
        "rebase_required": rebase_required,
        "working_tree_dirty": bool(status),
        "working_tree_status_paths": [
            line[3:] if len(line) >= 4 else line
            for line in status.splitlines()
            if line.strip()
        ],
        "changed_bundle_files": changed_files,
        "next_action": (
            "Run B00/A00 rebase protocol before implementation."
            if rebase_required
            else "Run prepared validation, then start only the first eligible subbundle."
        ),
    }
    (destination / "materialization-report.json").write_text(
        json.dumps(report, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
        newline="\n",
    )

    scripts_root = destination / "scripts"
    checksum_command = [
        sys.executable,
        str(scripts_root / "calculate_checksums.py"),
        "--bundle-root",
        str(destination),
    ]
    checksum_result = subprocess.run(checksum_command, check=False)
    if checksum_result.returncode != 0:
        print("ERROR: failed to regenerate bundle integrity files", file=sys.stderr)
        return checksum_result.returncode

    if not args.skip_validation:
        validation_command = [
            sys.executable,
            str(scripts_root / "validate_bundle.py"),
            "--bundle-root",
            str(destination),
            "--repo-root",
            str(repo_root),
            "--stage",
            "prepared",
        ]
        if args.allow_different_commit:
            validation_command.append("--allow-different-commit")
        validation_result = subprocess.run(validation_command, check=False)
        if validation_result.returncode != 0:
            print("ERROR: materialized bundle validation failed", file=sys.stderr)
            return validation_result.returncode

    print(f"Materialized bundle: {destination}")
    print(f"Repository HEAD: {head}")
    print(f"References replaced in {len(changed_files)} files")
    if rebase_required:
        print("WARNING: prepared source anchor differs; implementation remains blocked pending rebase review")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
