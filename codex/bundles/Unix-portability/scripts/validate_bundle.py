#!/usr/bin/env python3
"""Validate the portable or materialized CanDoItAll portability bundle."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable

PORTABLE_TOKEN = "{{REPO_ROOT}}"
EXPECTED_SUBBUNDLE_FILES = {
    "README.md",
    "agent-prompt.md",
    "exit-criteria.md",
    "tasks.json",
    "tasks.md",
    "validation.md",
}
ROOT_REQUIRED_FILES = {
    "README.md",
    "EXECUTIVE-SUMMARY-CS.md",
    "PROGRAM-SEQUENCING.md",
    "CURRENT-DEVELOPMENT-DELTA.md",
    "ARCHITECTURE-BOUNDARIES.md",
    "CODEX-EXECUTION-CONTRACT.md",
    "LEGACY-BUNDLE-DELTA.md",
    "ORIGINAL-REQUEST.md",
    "manifest.json",
    "shared/source-anchor.json",
    "shared/source-reference-manifest.json",
    "shared/findings-register.json",
    "shared/findings-register.csv",
    "shared/support-matrix.json",
    "shared/support-matrix.csv",
    "shared/migration-matrix.csv",
    "shared/test-matrix.csv",
    "shared/external-dependency-ledger.csv",
    "shared/platform-sensitive-patterns.txt",
    "shared/rebase-protocol.md",
    "shared/architecture-invariants.md",
    "templates/gate-summary-template.md",
    "templates/rebase-report-template.md",
    "scripts/calculate_checksums.py",
    "scripts/materialize_bundle.py",
    "scripts/scan_artifacts_for_secrets.py",
    "scripts/scan_portability.py",
    "scripts/validate_bundle.py",
    "scripts/run-baseline.sh",
    "scripts/run-baseline.ps1",
}
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


def is_integrity_file(root: Path, path: Path) -> bool:
    relative_parts = path.relative_to(root).parts
    return (
        path.is_file()
        and "evidence" not in relative_parts
        and "__pycache__" not in relative_parts
        and path.suffix != ".pyc"
    )


@dataclass(frozen=True)
class Issue:
    severity: str
    path: str
    message: str


class ValidationContext:
    def __init__(self, root: Path) -> None:
        self.root = root
        self.issues: list[Issue] = []

    def error(self, path: Path | str, message: str) -> None:
        self.issues.append(Issue("ERROR", self._relative(path), message))

    def warning(self, path: Path | str, message: str) -> None:
        self.issues.append(Issue("WARNING", self._relative(path), message))

    def _relative(self, path: Path | str) -> str:
        candidate = Path(path)
        try:
            return candidate.resolve().relative_to(self.root.resolve()).as_posix()
        except (OSError, ValueError):
            return str(path)

    @property
    def errors(self) -> list[Issue]:
        return [issue for issue in self.issues if issue.severity == "ERROR"]


class BundleValidationError(RuntimeError):
    pass


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--bundle-root", required=True, type=Path)
    parser.add_argument("--repo-root", type=Path)
    parser.add_argument(
        "--stage",
        choices=("portable", "prepared", "completed"),
        default="portable",
        help="Portable validates the distributable bundle; prepared also validates repository references; completed requires finished requirements and gates.",
    )
    parser.add_argument(
        "--bundle",
        choices=("all", "core", "runtime"),
        default="all",
    )
    parser.add_argument(
        "--allow-different-commit",
        action="store_true",
        help="Allow prepared validation against a checkout other than the prepared anchor. A rebase warning is still emitted.",
    )
    parser.add_argument(
        "--skip-checksums",
        action="store_true",
        help="Skip index and checksum verification while constructing a new bundle.",
    )
    return parser.parse_args()


def load_json(path: Path, context: ValidationContext) -> Any | None:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except FileNotFoundError:
        context.error(path, "Required JSON file is missing.")
    except json.JSONDecodeError as exc:
        context.error(path, f"Invalid JSON at line {exc.lineno}, column {exc.colno}: {exc.msg}")
    except UnicodeDecodeError as exc:
        context.error(path, f"JSON file is not valid UTF-8: {exc}")
    return None


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def run_git(repo_root: Path, *arguments: str) -> str:
    completed = subprocess.run(
        ["git", "-C", str(repo_root), *arguments],
        check=True,
        capture_output=True,
        text=True,
    )
    return completed.stdout.strip()


def validate_root_files(context: ValidationContext) -> None:
    for relative_path in sorted(ROOT_REQUIRED_FILES):
        path = context.root / relative_path
        if not path.is_file():
            context.error(path, "Required bundle file is missing.")

    expected_directories = {
        "bundles/01-core-portability-foundation",
        "bundles/02-runtime-tools-process-drivers",
        "shared",
        "templates",
        "scripts",
    }
    for relative_path in sorted(expected_directories):
        path = context.root / relative_path
        if not path.is_dir():
            context.error(path, "Required bundle directory is missing.")


def validate_all_json(context: ValidationContext) -> None:
    for path in sorted(context.root.rglob("*.json")):
        load_json(path, context)


def validate_root_manifest(context: ValidationContext) -> dict[str, Any] | None:
    path = context.root / "manifest.json"
    manifest = load_json(path, context)
    if not isinstance(manifest, dict):
        return None

    if manifest.get("program_id") != "candoitall-unix-portability":
        context.error(path, "Unexpected or missing program_id.")
    if manifest.get("portable_source_token") != PORTABLE_TOKEN:
        context.error(path, "portable_source_token must remain the canonical token.")

    bundles = manifest.get("bundles")
    if not isinstance(bundles, list) or len(bundles) != 2:
        context.error(path, "Root manifest must declare exactly the core and runtime bundles.")
    else:
        ids = [item.get("id") for item in bundles if isinstance(item, dict)]
        if ids != ["core", "runtime"]:
            context.error(path, "Bundle execution order must be core followed by runtime.")

    anchor_path = context.root / "shared/source-anchor.json"
    anchor_document = load_json(anchor_path, context)
    shared_anchor = anchor_document.get("current") if isinstance(anchor_document, dict) else None
    root_anchor = manifest.get("source_anchor")
    if isinstance(shared_anchor, dict) and isinstance(root_anchor, dict):
        for field in ("repository", "branch", "commit", "dotnet_sdk"):
            if shared_anchor.get(field) != root_anchor.get(field):
                context.error(path, f"Root source anchor field '{field}' differs from shared/source-anchor.json current anchor.")
    return manifest


def bundle_directories(root_manifest: dict[str, Any] | None, selected: str) -> list[tuple[str, Path]]:
    defaults = [
        ("core", Path("bundles/01-core-portability-foundation")),
        ("runtime", Path("bundles/02-runtime-tools-process-drivers")),
    ]
    if root_manifest is None or not isinstance(root_manifest.get("bundles"), list):
        return [(bundle_id, directory) for bundle_id, directory in defaults if selected in ("all", bundle_id)]

    result: list[tuple[str, Path]] = []
    for item in root_manifest["bundles"]:
        if not isinstance(item, dict):
            continue
        bundle_id = str(item.get("id", ""))
        directory = Path(str(item.get("directory", "")))
        if bundle_id in ("core", "runtime") and selected in ("all", bundle_id):
            result.append((bundle_id, directory))
    return result


def validate_bundle_directory(
    context: ValidationContext,
    bundle_id: str,
    relative_directory: Path,
    stage: str,
) -> None:
    bundle_root = context.root / relative_directory
    manifest_path = bundle_root / "manifest.json"
    requirements_path = bundle_root / "requirements/requirements.json"
    manifest = load_json(manifest_path, context)
    requirements_document = load_json(requirements_path, context)
    if not isinstance(manifest, dict) or not isinstance(requirements_document, dict):
        return

    requirements = requirements_document.get("requirements")
    if not isinstance(requirements, list):
        context.error(requirements_path, "requirements must be an array.")
        return

    requirement_by_id: dict[str, dict[str, Any]] = {}
    owner_to_requirements: dict[str, set[str]] = {}
    for item in requirements:
        if not isinstance(item, dict):
            context.error(requirements_path, "Every requirement must be an object.")
            continue
        requirement_id = str(item.get("id", "")).strip()
        owner = str(item.get("owner", "")).strip()
        if not requirement_id or not re.fullmatch(r"[A-Z][A-Z0-9-]*-\d{3}", requirement_id):
            context.error(requirements_path, f"Invalid requirement id '{requirement_id}'.")
            continue
        if requirement_id in requirement_by_id:
            context.error(requirements_path, f"Duplicate requirement id '{requirement_id}'.")
            continue
        if not owner:
            context.error(requirements_path, f"Requirement '{requirement_id}' has no owner.")
        requirement_by_id[requirement_id] = item
        owner_to_requirements.setdefault(owner, set()).add(requirement_id)

        if stage == "completed" and str(item.get("status", "")).casefold() not in {
            "completed",
            "verified",
            "accepted",
            "deferred-with-approval",
        }:
            context.error(requirements_path, f"Requirement '{requirement_id}' is not complete at completed stage.")

    expected_count = manifest.get("requirements_count")
    if expected_count != len(requirement_by_id):
        context.error(
            manifest_path,
            f"requirements_count is {expected_count}, but {len(requirement_by_id)} unique requirements were found.",
        )

    subbundles = manifest.get("subbundles")
    if not isinstance(subbundles, list) or not subbundles:
        context.error(manifest_path, "Bundle manifest has no subbundles.")
        return

    subbundle_ids: set[str] = set()
    listed_requirement_ids: set[str] = set()
    task_ids: set[str] = set()
    for subbundle in subbundles:
        if not isinstance(subbundle, dict):
            context.error(manifest_path, "Subbundle entry must be an object.")
            continue
        subbundle_id = str(subbundle.get("id", "")).strip()
        directory_value = str(subbundle.get("directory", "")).strip()
        if not subbundle_id or subbundle_id in subbundle_ids:
            context.error(manifest_path, f"Missing or duplicate subbundle id '{subbundle_id}'.")
            continue
        subbundle_ids.add(subbundle_id)
        if not directory_value:
            context.error(manifest_path, f"Subbundle '{subbundle_id}' has no directory.")
            continue

        subbundle_root = bundle_root / directory_value
        if not subbundle_root.is_dir():
            context.error(subbundle_root, f"Subbundle directory for '{subbundle_id}' is missing.")
            continue
        actual_files = {path.name for path in subbundle_root.iterdir() if path.is_file()}
        missing_files = EXPECTED_SUBBUNDLE_FILES - actual_files
        for file_name in sorted(missing_files):
            context.error(subbundle_root / file_name, f"Required subbundle file for '{subbundle_id}' is missing.")

        tasks_path = subbundle_root / "tasks.json"
        tasks_document = load_json(tasks_path, context)
        if not isinstance(tasks_document, dict):
            continue
        if tasks_document.get("subbundle_id") != subbundle_id:
            context.error(tasks_path, "tasks.json subbundle_id differs from the bundle manifest.")
        if bool(tasks_document.get("conditional")) != bool(subbundle.get("conditional")):
            context.error(tasks_path, "tasks.json conditional flag differs from the bundle manifest.")

        declared_requirements = tasks_document.get("requirements")
        if not isinstance(declared_requirements, list):
            context.error(tasks_path, "Subbundle requirements must be an array.")
            declared_requirements = []
        declared_set = {str(item) for item in declared_requirements}
        expected_owner_set = owner_to_requirements.get(subbundle_id, set())
        if declared_set != expected_owner_set:
            missing = sorted(expected_owner_set - declared_set)
            extra = sorted(declared_set - expected_owner_set)
            context.error(
                tasks_path,
                f"Requirement ownership mismatch for {subbundle_id}; missing={missing}, extra={extra}.",
            )
        listed_requirement_ids.update(declared_set)

        tasks = tasks_document.get("tasks")
        if not isinstance(tasks, list) or not tasks:
            context.error(tasks_path, "Subbundle must contain at least one task.")
            continue
        task_requirement_union: set[str] = set()
        for task in tasks:
            if not isinstance(task, dict):
                context.error(tasks_path, "Every task must be an object.")
                continue
            task_id = str(task.get("id", "")).strip()
            if not task_id or task_id in task_ids:
                context.error(tasks_path, f"Missing or duplicate task id '{task_id}'.")
            else:
                task_ids.add(task_id)
            if not task_id.startswith(subbundle_id + "-"):
                context.error(tasks_path, f"Task '{task_id}' does not belong to subbundle '{subbundle_id}'.")
            task_requirements = task.get("requirements")
            if not isinstance(task_requirements, list):
                context.error(tasks_path, f"Task '{task_id}' requirements must be an array.")
                continue
            task_requirement_ids = {str(item) for item in task_requirements}
            unknown = task_requirement_ids - set(requirement_by_id)
            if unknown:
                context.error(tasks_path, f"Task '{task_id}' references unknown requirements {sorted(unknown)}.")
            foreign = task_requirement_ids - declared_set
            if foreign:
                context.error(tasks_path, f"Task '{task_id}' references requirements outside its subbundle: {sorted(foreign)}.")
            task_requirement_union.update(task_requirement_ids)
            if stage == "completed" and str(task.get("status", "")).casefold() not in {
                "completed",
                "verified",
                "accepted",
                "deferred-with-approval",
            }:
                context.error(tasks_path, f"Task '{task_id}' is not complete at completed stage.")
        if task_requirement_union != declared_set:
            context.error(
                tasks_path,
                f"Tasks do not cover the exact subbundle requirement set; missing={sorted(declared_set - task_requirement_union)}.",
            )

        validate_subbundle_headings(context, subbundle_root, subbundle_id)

    unknown_owners = set(owner_to_requirements) - subbundle_ids
    if unknown_owners:
        context.error(requirements_path, f"Requirements reference unknown subbundle owners: {sorted(unknown_owners)}.")
    if listed_requirement_ids != set(requirement_by_id):
        context.error(
            requirements_path,
            f"Subbundle coverage differs from requirement register; missing={sorted(set(requirement_by_id) - listed_requirement_ids)}.",
        )

    if bundle_id == "runtime" and "blocked" not in str(manifest.get("status", "")).casefold() and stage != "completed":
        status = str(manifest.get("status", "")).casefold()
        entry_gate = str(manifest.get("entry_gate", "")).casefold()
        provisional_record = (
            bundle_root.parent
            / "01-core-portability-foundation"
            / "reviews"
            / "22-a07-hosted-validation-deferral.md"
        )
        provisional_entry_is_recorded = (
            "provisional" in status
            and "provisional" in entry_gate
            and provisional_record.is_file()
        )
        if not provisional_entry_is_recorded:
            context.warning(
                manifest_path,
                "Runtime bundle is expected to remain blocked until the Core C4 handoff or an explicit provisional handoff is accepted.",
            )

    if stage == "completed":
        validate_completed_gate(context, bundle_root, bundle_id)


def validate_subbundle_headings(context: ValidationContext, subbundle_root: Path, subbundle_id: str) -> None:
    expected_fragments = {
        "README.md": f"# {subbundle_id}",
        "tasks.md": f"# {subbundle_id} tasks",
        "validation.md": f"# {subbundle_id} validation",
        "exit-criteria.md": "# Exit criteria",
        "agent-prompt.md": "# Agent prompt",
    }
    for file_name, fragment in expected_fragments.items():
        path = subbundle_root / file_name
        if not path.is_file():
            continue
        try:
            first_lines = "\n".join(path.read_text(encoding="utf-8").splitlines()[:3])
        except UnicodeDecodeError as exc:
            context.error(path, f"File is not valid UTF-8: {exc}")
            continue
        if fragment not in first_lines:
            context.error(path, f"Expected heading fragment '{fragment}' was not found near the top of the file.")


def validate_completed_gate(context: ValidationContext, bundle_root: Path, bundle_id: str) -> None:
    if bundle_id == "core":
        gate_path = bundle_root / "reviews/CORE-C4-HANDOFF.md"
        required_tokens = ("C4", "GO", "commit")
    else:
        gate_path = bundle_root / "reviews/RUNTIME-R4-HANDOFF.md"
        required_tokens = ("R4", "GO", "commit")
    if not gate_path.is_file():
        context.error(gate_path, "Completed bundle is missing its final handoff report.")
        return
    text = gate_path.read_text(encoding="utf-8")
    for token in required_tokens:
        if token.casefold() not in text.casefold():
            context.error(gate_path, f"Final handoff report does not contain required token '{token}'.")
    if "not started" in text.casefold() or "pending" in text.casefold():
        context.error(gate_path, "Final handoff still contains an unfinished status marker.")


def validate_source_references(
    context: ValidationContext,
    stage: str,
    repo_root: Path | None,
    root_manifest: dict[str, Any] | None,
    allow_different_commit: bool,
) -> None:
    manifest_paths = [context.root / "shared/source-reference-manifest.json"]
    manifest_paths.extend(sorted(context.root.glob("bundles/*/inventories/source-reference-manifest.json")))
    for path in manifest_paths:
        document = load_json(path, context)
        if not isinstance(document, dict):
            continue
        references = document.get("references")
        if not isinstance(references, list):
            context.error(path, "Source-reference manifest references must be an array.")
            continue
        ids: set[str] = set()
        for item in references:
            if not isinstance(item, dict):
                context.error(path, "Source reference must be an object.")
                continue
            reference_id = str(item.get("id", "")).strip()
            relative_path = str(item.get("relative_path", "")).strip()
            portable_reference = str(item.get("portable_reference", "")).strip()
            if not reference_id or reference_id in ids:
                context.error(path, f"Missing or duplicate source reference id '{reference_id}'.")
            ids.add(reference_id)
            if not relative_path or Path(relative_path).is_absolute() or ".." in Path(relative_path).parts:
                context.error(path, f"Source reference '{reference_id}' has an invalid relative_path '{relative_path}'.")
            if stage == "portable":
                if not portable_reference.startswith(PORTABLE_TOKEN + "/"):
                    context.error(path, f"Portable source reference '{reference_id}' does not use {PORTABLE_TOKEN}.")
            elif repo_root is not None:
                if PORTABLE_TOKEN in portable_reference:
                    context.error(path, f"Prepared source reference '{reference_id}' was not materialized.")
                candidate = repo_root / relative_path
                if not candidate.exists():
                    context.error(path, f"Repository source reference '{reference_id}' does not exist: {relative_path}")

    if stage == "portable":
        return
    if repo_root is None:
        context.error(context.root, "--repo-root is required for prepared or completed validation.")
        return
    if not (repo_root / "CanDoItAll.slnx").is_file():
        context.error(repo_root, "Repository root does not contain CanDoItAll.slnx.")
        return
    try:
        head = run_git(repo_root, "rev-parse", "HEAD")
    except (subprocess.CalledProcessError, FileNotFoundError) as exc:
        context.error(repo_root, f"Unable to read repository HEAD: {exc}")
        return
    prepared_commit = ""
    if isinstance(root_manifest, dict):
        anchor = root_manifest.get("source_anchor")
        if isinstance(anchor, dict):
            prepared_commit = str(anchor.get("commit", ""))
    if prepared_commit and head != prepared_commit:
        message = f"Repository HEAD {head} differs from prepared anchor {prepared_commit}; run the rebase protocol before implementation."
        if allow_different_commit:
            context.warning(repo_root, message)
        else:
            context.error(repo_root, message)


def markdown_links(text: str) -> Iterable[str]:
    in_fence = False
    for line in text.splitlines():
        if line.lstrip().startswith("```"):
            in_fence = not in_fence
            continue
        if in_fence:
            continue
        for match in re.finditer(r"(?<!!)\[[^\]]+\]\(([^)]+)\)", line):
            yield match.group(1).strip().split("#", 1)[0]


def validate_markdown_links(context: ValidationContext) -> None:
    for path in sorted(context.root.rglob("*.md")):
        try:
            text = path.read_text(encoding="utf-8")
        except UnicodeDecodeError as exc:
            context.error(path, f"Markdown file is not valid UTF-8: {exc}")
            continue
        for target in markdown_links(text):
            if not target or target.startswith(("http://", "https://", "mailto:", "#")):
                continue
            if "{{" in target or "<" in target or ">" in target:
                continue
            candidate = (path.parent / target).resolve()
            try:
                candidate.relative_to(context.root.resolve())
            except ValueError:
                context.error(path, f"Markdown link escapes the bundle root: {target}")
                continue
            if not candidate.exists():
                context.error(path, f"Broken relative Markdown link: {target}")


def validate_portable_tokens(context: ValidationContext, stage: str) -> None:
    occurrences: list[Path] = []
    disallowed_materialized_occurrences: list[Path] = []
    for path in sorted(context.root.rglob("*")):
        if not path.is_file() or path.suffix.lower() not in TEXT_SUFFIXES:
            continue
        if path.name in {"CHECKSUMS.sha256", "bundle-index.json"}:
            continue
        try:
            text = path.read_text(encoding="utf-8")
        except UnicodeDecodeError:
            continue
        if PORTABLE_TOKEN not in text:
            continue
        occurrences.append(path)
        relative = path.relative_to(context.root)
        allowed_metadata_location = (
            (relative.parts and relative.parts[0] == "scripts")
            or path.name == "manifest.json"
            or path.name == "source-reference-manifest.json"
        )
        if not allowed_metadata_location:
            disallowed_materialized_occurrences.append(path)
    if stage == "portable" and not occurrences:
        context.error(context.root, f"Portable bundle contains no {PORTABLE_TOKEN} references.")
    if stage in {"prepared", "completed"} and disallowed_materialized_occurrences:
        sample = ", ".join(context._relative(path) for path in disallowed_materialized_occurrences[:8])
        context.error(
            context.root,
            f"Materialized bundle still contains executable source references using {PORTABLE_TOKEN}; sample: {sample}",
        )


def validate_no_obvious_secrets(context: ValidationContext) -> None:
    token_patterns = {
        "private key": re.compile(r"-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----"),
        "GitHub token": re.compile(r"\bgh[opusr]_[A-Za-z0-9]{30,}\b"),
        "OpenAI token": re.compile(r"\bsk-[A-Za-z0-9_-]{32,}\b"),
        "AWS access key": re.compile(r"\bAKIA[0-9A-Z]{16}\b"),
        "Slack token": re.compile(r"\bxox[baprs]-[A-Za-z0-9-]{20,}\b"),
    }
    for path in sorted(context.root.rglob("*")):
        if not path.is_file() or path.suffix.lower() not in TEXT_SUFFIXES:
            continue
        if path.name == "validate_bundle.py":
            continue
        try:
            text = path.read_text(encoding="utf-8")
        except UnicodeDecodeError:
            continue
        for label, pattern in token_patterns.items():
            if pattern.search(text):
                context.error(path, f"Bundle contains text matching a {label} pattern.")


def validate_integrity(context: ValidationContext) -> None:
    index_path = context.root / "bundle-index.json"
    checksums_path = context.root / "CHECKSUMS.sha256"
    index = load_json(index_path, context)
    if not isinstance(index, dict):
        return
    files = index.get("files")
    if not isinstance(files, list):
        context.error(index_path, "Index files must be an array.")
        return

    actual_payload = {
        path.relative_to(context.root).as_posix(): path
        for path in context.root.rglob("*")
        if is_integrity_file(context.root, path)
        and path.name not in {"bundle-index.json", "CHECKSUMS.sha256"}
    }
    indexed_paths: set[str] = set()
    for item in files:
        if not isinstance(item, dict):
            context.error(index_path, "Every index item must be an object.")
            continue
        relative_path = str(item.get("path", ""))
        if relative_path in indexed_paths:
            context.error(index_path, f"Duplicate indexed path '{relative_path}'.")
            continue
        indexed_paths.add(relative_path)
        actual = actual_payload.get(relative_path)
        if actual is None:
            context.error(index_path, f"Indexed file is missing: {relative_path}")
            continue
        if item.get("size_bytes") != actual.stat().st_size:
            context.error(index_path, f"Size mismatch for indexed file '{relative_path}'.")
        expected_hash = str(item.get("sha256", ""))
        if expected_hash != sha256_file(actual):
            context.error(index_path, f"SHA-256 mismatch for indexed file '{relative_path}'.")

    if indexed_paths != set(actual_payload):
        context.error(
            index_path,
            f"Index payload differs from actual payload; missing={sorted(set(actual_payload) - indexed_paths)[:10]}, extra={sorted(indexed_paths - set(actual_payload))[:10]}.",
        )
    if index.get("payload_file_count") != len(actual_payload):
        context.error(index_path, "payload_file_count differs from actual payload file count.")

    if not checksums_path.is_file():
        context.error(checksums_path, "Checksum file is missing.")
        return
    checksum_entries: dict[str, str] = {}
    for line_number, line in enumerate(checksums_path.read_text(encoding="utf-8").splitlines(), start=1):
        if not line.strip():
            continue
        match = re.fullmatch(r"([0-9a-f]{64})  (.+)", line)
        if match is None:
            context.error(checksums_path, f"Invalid checksum line {line_number}.")
            continue
        digest, relative_path = match.groups()
        if relative_path in checksum_entries:
            context.error(checksums_path, f"Duplicate checksum entry '{relative_path}'.")
            continue
        checksum_entries[relative_path] = digest

    actual_checksum_files = {
        path.relative_to(context.root).as_posix(): path
        for path in context.root.rglob("*")
        if is_integrity_file(context.root, path) and path.name != "CHECKSUMS.sha256"
    }
    if set(checksum_entries) != set(actual_checksum_files):
        context.error(
            checksums_path,
            f"Checksum coverage differs from actual files; missing={sorted(set(actual_checksum_files) - set(checksum_entries))[:10]}, extra={sorted(set(checksum_entries) - set(actual_checksum_files))[:10]}.",
        )
    for relative_path, digest in checksum_entries.items():
        actual = actual_checksum_files.get(relative_path)
        if actual is not None and sha256_file(actual) != digest:
            context.error(checksums_path, f"Checksum mismatch for '{relative_path}'.")


def print_report(context: ValidationContext, stage: str, selected_bundle: str) -> None:
    print(f"Bundle root: {context.root}")
    print(f"Validation stage: {stage}")
    print(f"Selected bundle: {selected_bundle}")
    print(f"Files: {sum(1 for path in context.root.rglob('*') if path.is_file())}")
    for issue in context.issues:
        print(f"{issue.severity}: {issue.path}: {issue.message}")
    warnings = sum(1 for issue in context.issues if issue.severity == "WARNING")
    print(f"Errors: {len(context.errors)}; warnings: {warnings}")
    print("RESULT: PASS" if not context.errors else "RESULT: FAIL")


def main() -> int:
    args = parse_args()
    root = args.bundle_root.expanduser().resolve()
    context = ValidationContext(root)
    if not root.is_dir():
        print(f"ERROR: bundle root does not exist: {root}", file=sys.stderr)
        return 2

    validate_root_files(context)
    validate_all_json(context)
    root_manifest = validate_root_manifest(context)
    for bundle_id, directory in bundle_directories(root_manifest, args.bundle):
        validate_bundle_directory(context, bundle_id, directory, args.stage)
    validate_source_references(
        context,
        args.stage,
        args.repo_root.expanduser().resolve() if args.repo_root else None,
        root_manifest,
        args.allow_different_commit,
    )
    validate_markdown_links(context)
    validate_portable_tokens(context, args.stage)
    validate_no_obvious_secrets(context)
    if not args.skip_checksums:
        validate_integrity(context)

    print_report(context, args.stage, args.bundle)
    return 1 if context.errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
