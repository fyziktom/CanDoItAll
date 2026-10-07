#!/usr/bin/env python3
"""Validate package/evidence structure, never the authenticity of product testing."""
from __future__ import annotations
import argparse
import hashlib
import json
import re
import sys
from pathlib import Path
from urllib.parse import unquote

ID = "CanDoItAll_Workbench_Completion_WB6"
STATUSES = {"NOT_RUN", "PASS", "FAIL", "BLOCKED", "BLOCKED_AUTHORIZATION", "QUALIFIED"}
HEX40 = re.compile(r"[0-9a-f]{40}")
HEX64 = re.compile(r"[0-9a-f]{64}")


def validate_evidence(evidence: dict, bundle: dict, require_complete: bool = False, require_demo: bool = False) -> list[str]:
    errors: list[str] = []
    if evidence.get("bundle_id") != ID:
        errors.append("Wrong evidence bundle ID")
    expected = {g["id"]: g for g in bundle["validation_groups"]}
    rows = evidence.get("groups", [])
    groups = {g.get("id"): g for g in rows}
    if len(rows) != len(groups) or set(groups) != set(expected):
        errors.append("Missing, extra or duplicate validation groups")
    for key, group in groups.items():
        if group.get("status") not in STATUSES:
            errors.append(f"{key}: unknown status")
        if key in expected and group.get("lane") != expected[key]["lane"]:
            errors.append(f"{key}: wrong evidence lane")
        if group.get("status") in {"PASS", "QUALIFIED"}:
            if not group.get("attempts") or not group.get("evidence"):
                errors.append(f"{key}: passing/qualified status needs attempts and evidence")
    ready = evidence.get("readiness", {})
    is_template = evidence.get("is_template") is True
    if is_template:
        if any(g.get("status") != "NOT_RUN" or g.get("attempts") or g.get("evidence") for g in rows) or any(ready.values()):
            errors.append("Template contains product results")
        if require_complete or require_demo:
            errors.append("An unexecuted template cannot meet completion gates")
    complete = require_complete or require_demo or ready.get("workbench_ui_complete") is True
    demo = require_demo or ready.get("demo_ready") is True
    if complete:
        for key, group in expected.items():
            if group["lane"] == "structure" and groups.get(key, {}).get("status") != "PASS":
                errors.append(f"{key}: required Workbench structure is not PASS")
        census = evidence.get("census", {})
        if census.get("unfinished_active") != 0 or census.get("unclassified_active") != 0:
            errors.append("Active Workbench census is incomplete")
    if demo:
        if is_template:
            return errors
        for key, group in groups.items():
            if key == "G26":
                if group.get("status") not in {"PASS", "QUALIFIED"}:
                    errors.append("Broad checkpoint has no acceptable explicit disposition")
                elif group.get("status") == "QUALIFIED" and not evidence.get("broad_result", {}).get("exceptions"):
                    errors.append("Qualified broad result requires exact exceptions")
            elif group.get("status") != "PASS":
                errors.append(f"{key}: mandatory demo gate is not PASS")
        if evidence.get("blocking_findings"):
            errors.append("Blocking findings remain")
        pair = evidence.get("source_pair", {})
        for key in ("main_commit", "components_commit", "filetools_commit"):
            if not HEX40.fullmatch(str(pair.get(key, ""))):
                errors.append(f"Missing actual source identity: {key}")
        for key in ("production_fingerprint", "published_hash"):
            if not HEX64.fullmatch(str(pair.get(key, ""))):
                errors.append(f"Missing actual source/publish hash: {key}")
        candidate = evidence.get("candidate", {})
        for key in ("start_script", "stop_script", "check_script", "runbook"):
            if not candidate.get(key):
                errors.append(f"Candidate missing {key}")
        if candidate.get("restart_count", 0) < 2 or candidate.get("restore_test_passed") is not True:
            errors.append("Candidate restart/recovery proof missing")
        real = evidence.get("genuine_provider", {})
        if real.get("is_scripted") is not False or real.get("real_language_vision_attempts", 0) <= 0 or real.get("real_image_attempts", 0) <= 0:
            errors.append("Genuine model/image attempts missing")
        if not real.get("authorization_reference"):
            errors.append("Real-provider authorization or no-paid-local scope missing")
        for flag in ("workbench_ui_complete", "candidate_runnable", "deterministic_integration_passed", "genuine_model_rehearsal_passed"):
            if ready.get(flag) is not True:
                errors.append(f"Readiness flag not established: {flag}")
        if not evidence.get("artifact_oracles") or not evidence.get("signed_commits") or not evidence.get("resource_disposition"):
            errors.append("Artifact, signature or resource records missing")
    return errors


def validate_package(root: Path) -> dict:
    root = root.resolve()
    bundle = json.loads((root / "bundle.json").read_text(encoding="utf-8"))
    if bundle.get("id") != ID or bundle.get("commits_are_checkout_pins") is not False or bundle.get("reviewer_ran_product_tests") is not False:
        raise ValueError("Unexpected bundle/review claims")
    manifest: dict[str, str] = {}
    for line in (root / "MANIFEST.sha256").read_text(encoding="utf-8").splitlines():
        digest, name = line.split("  ", 1)
        path = (root / name).resolve()
        if not path.is_relative_to(root) or name in manifest or not HEX64.fullmatch(digest):
            raise ValueError("Invalid or duplicate manifest entry")
        if not path.is_file() or hashlib.sha256(path.read_bytes()).hexdigest() != digest:
            raise ValueError(f"Manifest mismatch: {name}")
        manifest[name] = digest
    actual = {p.relative_to(root).as_posix() for p in root.rglob("*") if p.is_file() and "__pycache__" not in p.parts and p.name != "MANIFEST.sha256"}
    # Nested shared manifest is itself a sealed input and must be tracked.
    if (root / "shared/MANIFEST.sha256").is_file():
        actual.add("shared/MANIFEST.sha256")
    if set(manifest) != actual:
        raise ValueError(f"Manifest membership mismatch: {sorted(set(manifest) ^ actual)}")
    provenance = json.loads((root / "shared-provenance.json").read_text(encoding="utf-8"))
    for name, digest in provenance["files"].items():
        if hashlib.sha256((root / "shared" / name).read_bytes()).hexdigest() != digest:
            raise ValueError(f"Changed shared input: {name}")
    link_count = 0
    for document in root.rglob("*.md"):
        # Historical shared audit links point into the source checkout, not the package.
        if "shared" in document.relative_to(root).parts:
            continue
        content = document.read_text(encoding="utf-8")
        for match in re.finditer(r"\[[^\]]*\]\(([^)]+)\)", content):
            target = match.group(1).strip().split("#", 1)[0]
            if not target or "://" in target or target.startswith("mailto:"):
                continue
            path = (document.parent / unquote(target)).resolve()
            if not path.is_relative_to(root) or not path.exists():
                raise ValueError(f"Broken local link in {document.name}: {target}")
            link_count += 1
    evidence = json.loads((root / "templates/evidence.json").read_text(encoding="utf-8"))
    errors = validate_evidence(evidence, bundle)
    if errors:
        raise ValueError("; ".join(errors))
    return {"package_files": len(manifest) + 1, "shared_files": len(provenance["files"]),
            "local_links": link_count, "groups": len(bundle["validation_groups"]),
            "product_tests_executed_by_this_check": False}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--evidence", type=Path)
    parser.add_argument("--require-workbench-complete", action="store_true")
    parser.add_argument("--require-demo-ready", action="store_true")
    args = parser.parse_args()
    try:
        result = validate_package(args.root)
        bundle = json.loads((args.root / "bundle.json").read_text(encoding="utf-8"))
        evidence_path = args.evidence or args.root / "templates/evidence.json"
        evidence = json.loads(evidence_path.read_text(encoding="utf-8"))
        errors = validate_evidence(evidence, bundle, args.require_workbench_complete, args.require_demo_ready)
        if errors:
            raise ValueError("; ".join(errors))
        print(json.dumps(result, indent=2))
        return 0
    except Exception as exc:
        print(f"Validation failed: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
