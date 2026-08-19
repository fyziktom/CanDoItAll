from __future__ import annotations

import json
import sys
from pathlib import Path


def load(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def main() -> int:
    root = Path(__file__).resolve().parents[1]
    manifest = load(root / "manifest.json")
    requirements_doc = load(root / "requirements/requirements.json")
    trace = load(root / "traceability/requirements-to-subbundles.json")
    findings_doc = load(root / "analysis/findings-register.json")
    finding_trace = load(root / "traceability/findings-to-subbundles.json")
    checkpoint_trace = load(root / "traceability/checkpoint-coverage.json")

    errors: list[str] = []
    subbundles = manifest["subbundles"]
    sb_ids = {item["id"] for item in subbundles}
    req_ids = [item["id"] for item in requirements_doc["requirements"]]

    if len(req_ids) != len(set(req_ids)):
        errors.append("Requirement ids are not unique.")

    for req in requirements_doc["requirements"]:
        rid = req["id"]
        owners = req.get("ownedBy", [])
        if not owners:
            errors.append(f"{rid} has no subbundle owner.")
        unknown = set(owners) - sb_ids
        if unknown:
            errors.append(f"{rid} has unknown owners: {sorted(unknown)}")
        if rid not in trace:
            errors.append(f"{rid} is absent from traceability JSON.")
        else:
            if trace[rid].get("subbundles") != owners:
                errors.append(f"{rid} owner list differs between requirements and traceability.")

    declared_req_ids = set(req_ids)
    for sb in subbundles:
        for rid in sb.get("requirements", []):
            if rid not in declared_req_ids:
                errors.append(f"{sb['id']} references unknown requirement {rid}.")
            elif sb["id"] not in trace[rid]["subbundles"]:
                errors.append(f"{sb['id']} missing from traceability for {rid}.")

    for finding in findings_doc["findings"]:
        fid = finding["id"]
        owners = finding_trace.get(fid)
        if not owners:
            errors.append(f"{fid} has no closure owner.")
            continue
        unknown = set(owners) - sb_ids
        if unknown:
            errors.append(f"{fid} has unknown owners: {sorted(unknown)}")

    manifest_cps = {cp["id"]: cp for cp in manifest["checkpoints"]}
    if len(checkpoint_trace) != len(manifest_cps):
        errors.append("Checkpoint trace count differs from manifest.")
    for item in checkpoint_trace:
        cp_id = item["checkpoint"]
        if cp_id not in manifest_cps:
            errors.append(f"Unknown checkpoint in trace: {cp_id}")
            continue
        if item["afterSubbundle"] != manifest_cps[cp_id]["after"]:
            errors.append(f"{cp_id} afterSubbundle differs from manifest.")
        if item["afterSubbundle"] not in sb_ids:
            errors.append(f"{cp_id} references unknown subbundle.")

    if errors:
        print("Traceability validation failed:", file=sys.stderr)
        for error in errors:
            print(f"- {error}", file=sys.stderr)
        return 1

    print(
        f"Traceability passed: {len(req_ids)} requirements, "
        f"{len(findings_doc['findings'])} findings, "
        f"{len(manifest_cps)} checkpoints."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
