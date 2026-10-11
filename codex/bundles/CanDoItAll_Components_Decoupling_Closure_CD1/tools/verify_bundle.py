#!/usr/bin/env python3
"""Verify this handoff archive; never interpret its integrity as product validation."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import sys

REQUIRED = {
    "README.md", "prompt.md", "REVIEW_PC4.md", "REMAINING_SCOPE.md", "APPLICATION_SURFACES.md",
    "CENSUS_AND_GUARDS.md", "EXECUTION_PLAN.md", "VALIDATION_AND_REUSE.md", "BROWSER_JOURNEYS.md",
    "OPERATIONS_AND_SIGNING.md", "COMPLETION_AND_HANDOFF.md", "progress-template.md",
    "bundle.json", "acceptance-plan.json", "evidence-template.json", "family-register.json",
    "inventory-seed.json", "review-sources.json", "tools/collect_inventory.py", "tools/verify_bundle.py",
    "tools/test_bundle_tools.py", "PACKAGE_VALIDATION.md"
}


class BundleError(ValueError):
    """An invalid or internally inconsistent handoff archive."""


def read_json(path: Path) -> dict:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise BundleError(f"Cannot read JSON {path.name}: {error}") from error
    if not isinstance(value, dict):
        raise BundleError(f"Expected object in {path.name}")
    return value


def safe_relative(root: Path, value: str) -> Path:
    logical = PurePosixPath(value)
    if not value or logical.is_absolute() or ".." in logical.parts or "\\" in value or ":" in value:
        raise BundleError(f"Unsafe manifest path: {value!r}")
    result = root.joinpath(*logical.parts)
    probe = root
    for part in logical.parts:
        probe /= part
        if probe.is_symlink():
            raise BundleError(f"Symlink is not a package payload: {value}")
    if not result.resolve().is_relative_to(root.resolve()):
        raise BundleError(f"Escaping path: {value}")
    return result


def payload_paths(root: Path) -> set[str]:
    return {p.relative_to(root).as_posix() for p in root.rglob("*")
            if (p.is_file() or p.is_symlink()) and p.name != "MANIFEST.json"
            and "__pycache__" not in p.parts and p.suffix != ".pyc"}


def verify(root: Path) -> dict:
    root = root.resolve()
    actual = payload_paths(root)
    if not REQUIRED.issubset(actual):
        raise BundleError(f"Missing required files: {sorted(REQUIRED - actual)}")
    if any("UI_Decoupling_Shared_Bundle" in p.split("/") for p in actual):
        raise BundleError("A shared bundle must not be shipped")
    manifest = read_json(root / "MANIFEST.json")
    records = manifest.get("files")
    if not isinstance(records, list):
        raise BundleError("Manifest files must be a list")
    names = [item.get("path") for item in records if isinstance(item, dict)]
    if len(names) != len(records) or any(not isinstance(name, str) for name in names):
        raise BundleError("Malformed manifest record")
    if len(set(names)) != len(names):
        raise BundleError("Duplicate manifest paths")
    if set(names) != actual:
        raise BundleError("Manifest does not cover exactly the payload files")
    for item in records:
        path = safe_relative(root, item["path"])
        data = path.read_bytes()
        if item.get("bytes") != len(data) or item.get("sha256") != hashlib.sha256(data).hexdigest():
            raise BundleError(f"Integrity mismatch: {item['path']}")
        if path.suffix == ".json":
            read_json(path)

    bundle = read_json(root / "bundle.json")
    if bundle.get("id") != "CD1" or bundle.get("execution_pin") is not False:
        raise BundleError("Unexpected identity or execution pin")
    policy = bundle.get("shared_policy", {})
    if policy.get("ship_shared") is not False or policy.get("require_version") is not None or not policy.get("use_existing"):
        raise BundleError("Shared-bundle stability policy violated")
    if bundle.get("review_product_execution") != "NOT_RUN" or bundle.get("product_completion_state") != "NOT_RUN":
        raise BundleError("The handoff must not claim product execution")
    if bundle.get("signing", {}).get("automatic_push_authorized") is not False:
        raise BundleError("Automatic push is not authorized")
    acceptance = read_json(root / "acceptance-plan.json").get("groups", [])
    ids = [item.get("id") for item in acceptance]
    if not ids or len(ids) != len(set(ids)) or any(not isinstance(i, str) or not re.fullmatch(r"CD1-\d{2}", i) for i in ids):
        raise BundleError("Invalid acceptance identities")
    if any(not item.get("requirement") for item in acceptance):
        raise BundleError("Missing acceptance requirement")
    evidence = read_json(root / "evidence-template.json")
    evidence_ids = [item.get("id") for item in evidence.get("acceptance", [])]
    if len(evidence_ids) != len(set(evidence_ids)) or set(evidence_ids) != set(ids):
        raise BundleError("Acceptance/evidence identities differ")
    if evidence.get("status") != "NOT_RUN" or any(item.get("status") != "NOT_RUN" for item in evidence["acceptance"]):
        raise BundleError("Evidence template contains an invented pass")
    if any(status != "NOT_RUN" for status in evidence.get("dimensions", {}).values()):
        raise BundleError("Evidence dimensions contain an invented pass")
    sources = read_json(root / "review-sources.json")
    source_ids = [item.get("id") for item in sources.get("sources", [])]
    if not source_ids or len(source_ids) != len(set(source_ids)) or sources.get("review_commit") != bundle.get("review_commit"):
        raise BundleError("Source register identities do not match")
    seed = read_json(root / "inventory-seed.json")
    if seed.get("exhaustive") is not False or seed.get("semantic_certification") is not False:
        raise BundleError("The seed must not claim exhaustive semantic certification")
    links = 0
    for name in actual:
        if not name.endswith(".md"):
            continue
        for target in re.findall(r"(?<!!)\[[^\]]*\]\(([^)]+)\)", (root / name).read_text(encoding="utf-8")):
            target = target.split("#", 1)[0]
            if not target or re.match(r"^[A-Za-z][A-Za-z0-9+.-]*:", target):
                continue
            destination = ((root / name).parent / target).resolve()
            if not destination.is_relative_to(root) or not destination.is_file():
                raise BundleError(f"Broken local link in {name}: {target}")
            links += 1
    return {"payload_files": len(actual), "acceptance_groups": len(ids), "source_entries": len(source_ids),
            "checked_local_links": links, "package_integrity": "PASS", "product_validation": "NOT_RUN"}


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parent.parent)
    args = parser.parse_args(argv)
    try:
        print(json.dumps(verify(args.root), indent=2))
        return 0
    except (BundleError, OSError, KeyError, TypeError) as error:
        print(f"Package check failed: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
