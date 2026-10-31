#!/usr/bin/env python3
"""Validate this briefing package, not the CanDoItAll application."""
from __future__ import annotations

import argparse
import hashlib
import itertools
import json
import re
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any
from urllib.parse import unquote


def same(left: Any, right: Any) -> bool:
    """Keep null, boolean, numeric and string identities distinct."""
    return type(left) is type(right) and left == right


def scalar(base: Any, local: Any, remote: Any) -> dict[str, Any]:
    if same(local, remote):
        return {"state": "Unchanged" if same(local, base) else "SameChange", "value": local}
    if same(local, base):
        return {"state": "RemoteOnly", "value": remote}
    if same(remote, base):
        return {"state": "LocalOnly", "value": local}
    return {"state": "Conflict"}


def validate(root: Path, pending_report: Path | None = None) -> dict[str, Any]:
    errors: list[str] = []
    checks: dict[str, Any] = {}
    all_files = sorted(p for p in root.rglob("*") if p.is_file() and "__pycache__" not in p.parts)
    data: dict[str, Any] = {}
    for path in all_files:
        if path.suffix == ".json":
            try:
                data[path.relative_to(root).as_posix()] = json.loads(path.read_text(encoding="utf-8"))
            except (ValueError, OSError) as exc:
                errors.append(f"Invalid JSON: {path.relative_to(root)}: {exc}")
    checks["json_files_parsed"] = len(data)

    required = [
        "00_START_HERE.md", "CODEX_PROMPT.md", "01_AUDIT_FINDINGS.md",
        "architecture/01_BOUNDARIES.md", "architecture/02_MERGE_PROTOCOL.md",
        "architecture/03_CHANGE_OBSERVATION.md", "architecture/04_OVERLAYS.md",
        "architecture/05_AUTHORITY_AND_HISTORY.md", "architecture/06_PERFORMANCE.md",
        "inventory/surfaces.json", "inventory/01_SURFACES.md", "inventory/02_WRITE_PERIMETER.md",
        "evidence/sources.json", "review/requirements.json", "review/02_FINAL_REVIEW.md",
        "examples/merge-vectors.json", "pilot/01_IMPLEMENTATION.md", "pilot/02_MANUAL_ACCEPTANCE.md",
        "operations/01_EXECUTION_AND_5032.md", "operations/02_ROLLOUT.md",
        "tests/01_TEST_STRATEGY.md", "tests/02_TEST_CLEANUP.md",
        "scripts/audit_repository.py", "scripts/validate_package.py", "scripts/README.md",
    ]
    for name in required:
        if not (root / name).is_file():
            errors.append(f"Missing required file: {name}")
    checks["required_files"] = len(required)

    def unique_ids(items: list[dict[str, Any]], label: str) -> set[str]:
        ids = [row.get("id", "") for row in items]
        if any(not value for value in ids) or len(set(ids)) != len(ids):
            errors.append(f"Missing or duplicate IDs: {label}")
        return set(ids)

    sources = data.get("evidence/sources.json", {})
    source_rows = sources.get("sources", [])
    external_rows = sources.get("external_references", [])
    source_ids = unique_ids(source_rows + external_rows, "sources")
    baseline = sources.get("baseline", {}).get("commit", "")
    if not re.fullmatch(r"[0-9a-f]{40}", baseline):
        errors.append("Invalid baseline commit")
    for item in source_rows:
        if not re.fullmatch(r"[0-9a-f]{40}", item.get("commit", "")):
            errors.append(f"Invalid source SHA: {item.get('id')}")
        if not item.get("inspected_scope") or not item.get("evidence_kind"):
            errors.append(f"Missing evidence qualification: {item.get('id')}")
    checks["repository_sources"] = len(source_rows)
    checks["external_references"] = len(external_rows)

    inventory = data.get("inventory/surfaces.json", {})
    rows = inventory.get("rows", [])
    unique_ids(rows, "surfaces")
    if inventory.get("baseline_commit") != baseline:
        errors.append("Inventory/source baseline mismatch")
    allowed_states = set(inventory.get("evidence_states", {}))
    if not allowed_states:
        errors.append("Missing explicit evidence-state dictionary")
    for row in rows:
        for field in ("module", "surface", "ui_entry", "owner_entries", "required_action", "test_focus", "rollout_phase"):
            if not row.get(field):
                errors.append(f"Missing {field} on {row.get('id')}")
        if row.get("evidence_status") not in allowed_states:
            errors.append(f"Unknown evidence state: {row.get('id')}")
        # A combined policy is legitimate, but each token must be declared.
        for policy in re.split(r"[+/|,]", row.get("policy", "")):
            if policy.strip() not in inventory.get("policies", {}):
                errors.append(f"Unknown policy {policy!r} on {row.get('id')}")
        for ref in row.get("source_refs", []):
            if ref not in source_ids:
                errors.append(f"Unknown source reference {ref} on {row.get('id')}")
    checks["surface_entries"] = len(rows)
    checks["surface_evidence_states"] = {
        state: sum(row.get("evidence_status") == state for row in rows) for state in sorted(allowed_states)
    }

    requirements = data.get("review/requirements.json", [])
    unique_ids(requirements, "requirements")
    for req in requirements:
        if not req.get("requirement") or not req.get("documents"):
            errors.append(f"Incomplete requirement: {req.get('id')}")
        for document in req.get("documents", []):
            if not (root / document).is_file():
                errors.append(f"Missing mapped document: {req.get('id')}: {document}")
    checks["requirements_mapped"] = len(requirements)

    link_count = 0
    for path in all_files:
        if path.suffix != ".md":
            continue
        text = path.read_text(encoding="utf-8")
        for ref in set(re.findall(r"\b[SW]\d{2}\b", text)):
            if ref not in source_ids:
                errors.append(f"Unknown evidence ID {ref}: {path.relative_to(root)}")
        for raw in re.findall(r"\[[^\]]*\]\(([^)]+)\)", text):
            target = raw.split(' "', 1)[0].strip("<>")
            if re.match(r"^[a-zA-Z][a-zA-Z0-9+.-]*:", target) or target.startswith("#"):
                continue
            target = unquote(target.split("#", 1)[0])
            if not target:
                continue
            resolved = (path.parent / target).resolve()
            link_count += 1
            if not resolved.is_relative_to(root):
                errors.append(f"Link escapes package: {path.relative_to(root)} -> {target}")
            elif not resolved.exists() and resolved != pending_report:
                errors.append(f"Broken link: {path.relative_to(root)} -> {target}")
    checks["internal_markdown_links"] = link_count

    cases = data.get("examples/merge-vectors.json", {}).get("cases", [])
    unique_ids(cases, "vectors")
    scalar_count = scenario_count = 0
    for case in cases:
        if case.get("kind") == "scalar":
            scalar_count += 1
            try:
                actual = scalar(case["base"], case["local"], case["remote"])
                if actual != case["expected"]:
                    errors.append(f"Scalar vector mismatch: {case['id']}: {actual}")
            except KeyError as exc:
                errors.append(f"Missing scalar input on {case['id']}: {exc}")
        elif case.get("kind") == "scenario":
            scenario_count += 1
            if not case.get("case") or not case.get("expected_behavior"):
                errors.append(f"Incomplete scenario: {case['id']}")
        else:
            errors.append(f"Unknown vector kind: {case.get('id')}")
    checks["scalar_reference_vectors_executed"] = scalar_count
    checks["scenario_descriptions_structure_checked_only"] = scenario_count

    # These are properties of the reference truth table, not production tests.
    property_cases = 0
    swaps = {"RemoteOnly": "LocalOnly", "LocalOnly": "RemoteOnly"}
    domain = [None, "", "A", "B", 0, False]
    for base, local, remote in itertools.product(domain, repeat=3):
        result = scalar(base, local, remote)
        reverse = scalar(base, remote, local)
        property_cases += 1
        if reverse["state"] != swaps.get(result["state"], result["state"]):
            errors.append("Truth-table symmetry failed")
        if "value" in result and not any(same(result["value"], value) for value in (local, remote)):
            errors.append("Truth table invented a value")
        if (result["state"] == "Conflict") != (not same(local, remote) and not same(local, base) and not same(remote, base)):
            errors.append("Truth-table conflict condition failed")
    checks["scalar_reference_property_combinations"] = property_cases

    for document in ("00_START_HERE.md", "CODEX_PROMPT.md", "operations/01_EXECUTION_AND_5032.md"):
        if (root / document).is_file():
            text = (root / document).read_text(encoding="utf-8")
            if "AWAITING_USER_APPROVAL_PROMPT_GALLERY" not in text or "5032" not in text:
                errors.append(f"Missing mandatory stop-gate/host: {document}")
    checks["stop_gate_documents_checked"] = 3

    manifest = root / "MANIFEST.sha256"
    checked_hashes = 0
    if manifest.is_file():
        manifest_names: set[str] = set()
        for line in manifest.read_text(encoding="utf-8").splitlines():
            if not line.strip():
                continue
            try:
                expected, name = line.split("  ", 1)
                entry = (root / name).resolve()
                if not entry.is_relative_to(root) or not entry.is_file():
                    raise ValueError("Missing or unsafe entry")
                actual = hashlib.sha256(entry.read_bytes()).hexdigest()
                if expected != actual:
                    errors.append(f"Checksum mismatch: {name}")
                if name in manifest_names:
                    errors.append(f"Duplicate checksum entry: {name}")
                manifest_names.add(name)
                checked_hashes += 1
            except (ValueError, OSError) as exc:
                errors.append(f"Invalid checksum line: {exc}")
        payload_names = {p.relative_to(root).as_posix() for p in all_files if p != manifest}
        if manifest_names != payload_names:
            errors.append(f"Manifest coverage differs: missing={sorted(payload_names-manifest_names)}; extra={sorted(manifest_names-payload_names)}")
    checks["checksum_entries_verified"] = checked_hashes
    checks["manifest_present"] = manifest.is_file()
    return {
        "status": "PASS" if not errors else "FAIL",
        "scope": "Static briefing-package validation and reference scalar truth-table checks only",
        "checked_at_utc": datetime.now(timezone.utc).isoformat(),
        "application_builds_run": False,
        "application_tests_run": False,
        "application_runtime_verified": False,
        "production_merge_implementation_tested": False,
        "checks": checks,
        "errors": errors,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--write-report", type=Path, help="Write JSON report before sealing the hash manifest")
    args = parser.parse_args()
    root = args.root.resolve()
    if not root.is_dir():
        parser.error("Package root does not exist")
    report_path = args.write_report.resolve() if args.write_report else None
    if report_path and (root / "MANIFEST.sha256").exists():
        parser.error("The package is sealed. Validate without --write-report; do not mutate hashed reports.")
    report = validate(root, report_path)
    output = json.dumps(report, ensure_ascii=False, indent=2) + "\n"
    if report_path:
        report_path.parent.mkdir(parents=True, exist_ok=True)
        report_path.write_text(output, encoding="utf-8")
    print(output, end="")
    return 0 if report["status"] == "PASS" else 1


if __name__ == "__main__":
    sys.exit(main())
