#!/usr/bin/env python3
"""Check package integrity and evidence structure; never certify product execution."""
from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from pathlib import Path
from urllib.parse import unquote, urlsplit

HEX40 = re.compile(r"^[0-9a-f]{40}$")
HEX64 = re.compile(r"^[0-9a-f]{64}$")
STATUSES = {"NOT_RUN", "PASS", "FAIL", "BLOCKED", "QUALIFIED"}
LINK = re.compile(r"\[[^\]\n]*\]\(([^)\n]+)\)")
IGNORED = {"__pycache__", ".pytest_cache"}


def files(root: Path) -> list[Path]:
    return sorted(p for p in root.rglob("*") if p.is_file() and not any(x in IGNORED for x in p.parts))


def load(path: Path) -> dict:
    value = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ValueError(f"Expected a JSON object: {path.name}")
    return value


def under(root: Path, value: str) -> Path:
    path = (root / value).resolve()
    if not path.is_relative_to(root.resolve()):
        raise ValueError(f"Path leaves package: {value}")
    return path


def check_manifest(root: Path) -> tuple[list[str], int]:
    errors: list[str] = []
    entries: dict[str, str] = {}
    for line in (root / "MANIFEST.sha256").read_text(encoding="utf-8").splitlines():
        parts = line.split("  ", 1)
        if len(parts) != 2 or not HEX64.fullmatch(parts[0]):
            errors.append("Malformed manifest entry")
            continue
        digest, name = parts
        if name in entries:
            errors.append(f"Duplicate manifest path: {name}")
        entries[name] = digest
        try:
            path = under(root, name)
            if not path.is_file() or hashlib.sha256(path.read_bytes()).hexdigest() != digest:
                errors.append(f"Manifest mismatch: {name}")
        except ValueError as exc:
            errors.append(str(exc))
    expected = {p.relative_to(root).as_posix() for p in files(root) if p.name != "MANIFEST.sha256" or p.parent != root}
    if set(entries) != expected:
        errors.append("Manifest does not cover the exact package file set")
    return errors, len(entries)


def check_links(root: Path) -> tuple[list[str], int]:
    errors: list[str] = []
    count = 0
    for document in files(root):
        if document.suffix != ".md":
            continue
        text = document.read_text(encoding="utf-8")
        for match in LINK.finditer(text):
            target = match.group(1).strip().strip("<>")
            parsed = urlsplit(target)
            if parsed.scheme or parsed.netloc or target.startswith("#"):
                continue
            count += 1
            path = (document.parent / unquote(parsed.path)).resolve()
            if not path.is_relative_to(root.resolve()) or not path.exists():
                errors.append(f"Broken local link in {document.relative_to(root)}: {target}")
    return errors, count


def check_evidence(data: dict, required: list[str], complete: bool = False) -> list[str]:
    errors: list[str] = []
    if data.get("schema_version") != 1 or data.get("bundle_id") != "CDA-PROVIDER-HISTORY-PP3":
        errors.append("Evidence schema or bundle identity mismatch")
    groups = data.get("groups", [])
    ids = [group.get("id") for group in groups]
    if len(ids) != len(set(ids)) or set(ids) != set(required):
        errors.append("Evidence groups must match the exact required identities")
    attempts = data.get("attempts", [])
    attempt_ids = [attempt.get("id") for attempt in attempts]
    if None in attempt_ids or len(attempt_ids) != len(set(attempt_ids)):
        errors.append("Attempt identities must be present and unique")
    indexed = {attempt.get("id"): attempt for attempt in attempts}
    for attempt in attempts:
        name = attempt.get("id", "unnamed")
        if attempt.get("status") not in STATUSES:
            errors.append(f"Invalid attempt status: {name}")
        if attempt.get("status") == "PASS":
            if not all(attempt.get(key) for key in ("command", "source_fingerprint", "artifact_refs")):
                errors.append(f"Passing attempt needs command, source fingerprint and artifact references: {name}")
            if not HEX64.fullmatch(str(attempt.get("source_fingerprint", ""))):
                errors.append(f"Invalid source fingerprint: {name}")
        if attempt.get("kind") == "test" and attempt.get("status") == "PASS":
            expected = attempt.get("expected_discovery")
            actual = attempt.get("actual_discovery")
            executed = attempt.get("executed")
            passed = attempt.get("passed")
            counts = (expected, actual, executed, passed, attempt.get("failed"), attempt.get("skipped"))
            if not all(type(value) is int and value >= 0 for value in counts):
                errors.append(f"Passing test needs nonnegative integer counters: {name}")
            elif expected <= 0 or actual != expected or executed <= 0 or passed != executed or attempt.get("failed") != 0 or attempt.get("skipped") != 0:
                errors.append(f"Passing test has inconsistent or empty counters: {name}")
            if type(executed) is int and type(actual) is int and executed != actual and not attempt.get("expansion_note"):
                errors.append(f"Discovery/runtime difference needs an expansion note: {name}")
    for group in groups:
        status = group.get("status")
        name = group.get("id")
        if status not in STATUSES:
            errors.append(f"Invalid group status: {name}")
        refs = group.get("attempt_ids", [])
        if any(ref not in indexed for ref in refs):
            errors.append(f"Unknown attempt referenced by {name}")
        if status == "PASS" and (not refs or not any(indexed.get(ref, {}).get("status") == "PASS" for ref in refs)):
            errors.append(f"Passing group needs a referenced passing attempt: {name}")
        if status in {"FAIL", "BLOCKED", "QUALIFIED"} and not group.get("notes"):
            errors.append(f"Non-passing result needs its qualification: {name}")
        if complete and status != "PASS":
            errors.append(f"Required group not passed: {name}")
    if complete:
        pair = data.get("source_pair", {})
        for key in ("main_head", "components_head", "filetools_head"):
            if not HEX40.fullmatch(str(pair.get(key, ""))):
                errors.append(f"Missing final source revision: {key}")
        closure = data.get("closure", {})
        for key in ("history_ui_complete", "tooltip_followup_complete", "native_history_campaign_passed"):
            if closure.get(key) is not True:
                errors.append(f"Closure not established: {key}")
        if closure.get("unresolved_findings"):
            errors.append("Unresolved findings remain; retain them, do not relabel the task complete")
        if closure.get("dependency_delivery") not in {"VERIFIED_LOCAL", "VERIFIED_REMOTE"}:
            errors.append("Dependency delivery has not been verified")
        commits = closure.get("signed_commits", [])
        if not commits or any(not HEX40.fullmatch(str(c.get("sha", ""))) or c.get("signature_verified") is not True for c in commits):
            errors.append("Verified signed checkpoints are missing")
    return errors


def validate(root: Path, evidence: Path | None = None, complete: bool = False) -> tuple[list[str], dict]:
    errors, manifest_count = check_manifest(root)
    link_errors, link_count = check_links(root)
    errors.extend(link_errors)
    metadata = load(root / "bundle.json")
    required = metadata["required_group_ids"]
    if len(required) != len(set(required)) or not required:
        errors.append("Duplicate or missing required group identity")
    sources = load(root / "sources.json")["sources"]
    source_ids = [source["id"] for source in sources]
    if len(source_ids) != len(set(source_ids)):
        errors.append("Duplicate source identity")
    if any(not HEX40.fullmatch(source["ref"]) or not source.get("url") for source in sources):
        errors.append("A source lacks an exact ref or URL")
    provenance = load(root / "shared-provenance.json")
    shared_paths = {item["path"] for item in provenance["files"]}
    actual_shared = {p.relative_to(root).as_posix() for p in files(root / "shared")}
    if len(shared_paths) != 22 or shared_paths != actual_shared:
        errors.append("Shared foundation is not the exact retained 22-file set")
    for item in provenance["files"]:
        if hashlib.sha256(under(root, item["path"]).read_bytes()).hexdigest() != item["sha256"]:
            errors.append(f"Sealed shared content changed: {item['path']}")
    template = load(root / "templates/evidence.json")
    errors.extend(check_evidence(template, required))
    if template["attempts"] or any(group["status"] != "NOT_RUN" for group in template["groups"]):
        errors.append("The sealed template must not claim product execution")
    if evidence:
        errors.extend(check_evidence(load(evidence), required, complete))
    elif complete:
        errors.append("--require-complete needs an actual external --evidence file")
    return errors, {"manifest_entries": manifest_count, "local_links": link_count,
                    "shared_files": len(shared_paths), "source_records": len(sources), "required_groups": len(required)}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--evidence", type=Path)
    parser.add_argument("--require-complete", action="store_true")
    args = parser.parse_args()
    try:
        errors, metrics = validate(args.root.resolve(), args.evidence, args.require_complete)
    except (OSError, ValueError, KeyError, TypeError) as exc:
        print(f"INVALID: {exc}", file=sys.stderr)
        return 1
    print(json.dumps(metrics, indent=2))
    for error in errors:
        print(f"INVALID: {error}", file=sys.stderr)
    print("Structure/integrity check only; product execution and authenticity are not certified.")
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
