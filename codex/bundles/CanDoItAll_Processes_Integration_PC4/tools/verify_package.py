"""Read-only integrity and handoff-contract checks for the PC4 instruction artifact."""
from __future__ import annotations

import argparse
import csv
import hashlib
import json
import re
import sys
from pathlib import Path, PurePosixPath
from urllib.parse import unquote, urlsplit

PACKAGE_ID = "CDA-PROCESSES-INTEGRATION-PC4"
PACKAGE_FOLDER = "CanDoItAll_Processes_Integration_PC4"
SHA40 = re.compile(r"^[0-9a-f]{40}$")
SHA256 = re.compile(r"^[0-9a-f]{64}$")
LINK = re.compile(r"(?<!!)\[[^\]\n]+\]\(([^)\n]+)\)")


class PackageError(ValueError):
    """A package invariant or integrity requirement was not satisfied."""


def require(condition: bool, message: str) -> None:
    if not condition:
        raise PackageError(message)


def load_json(path: Path) -> dict:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as exc:
        raise PackageError(f"Cannot read JSON {path.name}: {exc}") from exc
    require(isinstance(value, dict), f"Expected JSON object: {path.name}")
    return value


def safe_relative(value: object) -> str:
    require(isinstance(value, str) and bool(value), "Empty/non-string manifest path")
    require("\\" not in value and ":" not in value, f"Nonportable path: {value}")
    path = PurePosixPath(value)
    require(not path.is_absolute(), f"Absolute path: {value}")
    require(all(part not in ("", ".", "..") for part in value.split("/")), f"Unsafe path: {value}")
    return value


def package_files(root: Path) -> dict[str, Path]:
    found: dict[str, Path] = {}
    folded: set[str] = set()
    for path in root.rglob("*"):
        rel = path.relative_to(root)
        require(not path.is_symlink(), f"Symlink is not allowed: {rel}")
        if "__pycache__" in rel.parts:
            continue
        if not path.is_file():
            continue
        name = safe_relative(rel.as_posix())
        require(name.casefold() not in folded, f"Case-colliding file: {name}")
        folded.add(name.casefold())
        require(not any(part.casefold() in ("shared", "ui_decoupling_shared_bundle") for part in rel.parts),
                f"Shared copy is not allowed: {name}")
        require(path.suffix.lower() not in (".zip", ".pgp", ".gpg", ".key", ".pfx", ".pem"),
                f"Nested archive or key material is not part of this artifact: {name}")
        found[name] = path
    return found


def check_links(root: Path, files: dict[str, Path]) -> None:
    for name, path in files.items():
        if path.suffix != ".md":
            continue
        for match in LINK.finditer(path.read_text(encoding="utf-8")):
            target = match.group(1).strip().strip("<>")
            parsed = urlsplit(target)
            if parsed.scheme or parsed.netloc or not parsed.path:
                continue
            destination = (path.parent / unquote(parsed.path)).resolve()
            require(destination.is_relative_to(root), f"External relative link in {name}: {target}")
            require(destination.is_file(), f"Missing local link in {name}: {target}")


def verify(root: Path) -> dict[str, int]:
    root = root.resolve()
    require(root.is_dir(), f"Missing package directory: {root}")
    files = package_files(root)
    require("MANIFEST.json" in files, "Missing MANIFEST.json")
    manifest = load_json(root / "MANIFEST.json")
    require(manifest.get("algorithm") == "sha256", "Manifest must use SHA-256")
    records = manifest.get("files")
    require(isinstance(records, list), "Manifest files must be a list")
    expected: set[str] = set()
    for record in records:
        require(isinstance(record, dict), "Invalid manifest record")
        name = safe_relative(record.get("path"))
        require(name != "MANIFEST.json", "Manifest must not hash itself")
        require(name not in expected, f"Duplicate manifest path: {name}")
        expected.add(name)
        require(name in files, f"Manifested file is missing: {name}")
        digest = record.get("sha256", "")
        require(isinstance(digest, str) and SHA256.fullmatch(digest) is not None, f"Invalid SHA-256: {name}")
        require(hashlib.sha256(files[name].read_bytes()).hexdigest() == digest, f"Hash mismatch: {name}")
    require(expected == set(files) - {"MANIFEST.json"}, "Manifest coverage differs from delivered files")

    for name, path in files.items():
        if path.suffix == ".json":
            load_json(path)
    bundle = load_json(root / "bundle.json")
    require(bundle.get("id") == PACKAGE_ID, "Wrong bundle identity")
    require(bundle.get("folder") == PACKAGE_FOLDER, "Wrong declared package folder")
    require(bundle.get("language") == "en", "Repository artifact language must be English")
    require(bundle.get("entry") == "prompt.md" and "prompt.md" in files, "Missing execution entry")
    require(bundle.get("delivery") == "single_archive_task_only", "Delivery must be a single task archive")
    require(bundle.get("execution_pin") is False, "Review commit must not be an execution pin")
    require(SHA40.fullmatch(bundle.get("review_commit", "")) is not None, "Invalid review commit")
    require(bundle.get("product_evidence_status") == "NOT_RUN", "Handoff cannot claim product execution")
    shared = bundle.get("shared_policy", {})
    require(shared.get("default_change_policy") == "NO_CHANGE", "Shared must remain unchanged by default")
    for field in ("version_pin", "manifest_pin", "separate_download_required"):
        require(shared.get(field) is False, f"Shared policy forbids {field}")

    sources = load_json(root / "SOURCES.json")
    require(sources.get("review_commit") == bundle["review_commit"], "Source review commit mismatch")
    require(sources.get("execution_pin") is False, "Source register cannot pin execution")
    ids = [s.get("id") for s in sources.get("sources", [])]
    require(ids and len(ids) == len(set(ids)), "Source IDs are empty or duplicated")
    for source in sources["sources"]:
        require(bool(source.get("coverage")), f"Missing read scope: {source['id']}")
        require(str(source.get("url", "")).startswith("https://"), f"Missing source URL: {source['id']}")
        if "path" in source:
            safe_relative(source["path"])
            require(SHA40.fullmatch(source.get("ref", "")) is not None, f"Invalid source ref: {source['id']}")
            blob = source.get("git_blob_sha")
            require(blob is None or SHA40.fullmatch(blob) is not None, f"Invalid blob SHA: {source['id']}")

    findings = load_json(root / "findings.json")["findings"]
    finding_ids = [f["id"] for f in findings]
    require(len(finding_ids) == len(set(finding_ids)), "Duplicate finding IDs")
    for finding in findings:
        require(set(finding["sources"]) <= set(ids), f"Unknown source in {finding['id']}")
    plan = load_json(root / "acceptance-plan.json")
    require(plan.get("assignment_id") == PACKAGE_ID and plan.get("review_commit") == bundle["review_commit"], "Acceptance identity mismatch")
    require(plan.get("execution_pin") is False, "Acceptance plan cannot pin execution")
    groups = plan.get("groups", [])
    group_ids = [g["id"] for g in groups]
    require(group_ids and len(group_ids) == len(set(group_ids)), "Duplicate or empty acceptance IDs")
    require(len(groups) == plan.get("group_count") == bundle.get("coverage_group_count"), "Acceptance count mismatch")
    referenced: set[str] = set()
    for group in groups:
        require(group.get("stage") in {f"S{i}" for i in range(6)}, f"Invalid stage: {group['id']}")
        require(bool(group.get("title")) and bool(group.get("obligation")), f"Empty acceptance: {group['id']}")
        require(set(group["findings"]) <= set(finding_ids), f"Unknown finding in {group['id']}")
        referenced.update(group["findings"])
    require(referenced == set(finding_ids), "Every finding needs acceptance coverage")

    with (root / "CARRY_FORWARD.csv").open(encoding="utf-8", newline="") as stream:
        rows = list(csv.DictReader(stream))
    identities = [(r["origin"], r["group"]) for r in rows]
    require(len(identities) == len(set(identities)), "Duplicate carry-forward identity")
    for origin, count in {"PC1": 42, "PC2": 31, "PC3": 50}.items():
        actual = [r["group"] for r in rows if r["origin"] == origin]
        require(len(actual) == count == bundle["carry_forward_counts"].get(origin), f"Wrong {origin} carry-forward count")
        require(actual == bundle["carry_forward_ids"].get(origin), f"Changed {origin} carry-forward IDs")
    require(len(rows) == 123, "Unexpected carry-forward origin")
    require(all(r["current_execution_status"] == "NOT_RUN" for r in rows), "Carry-forward cannot claim current execution")

    evidence = load_json(root / "evidence-template.json")
    require(evidence.get("assignment_id") == PACKAGE_ID, "Evidence identity mismatch")
    require(evidence.get("product_execution_status") == "NOT_RUN", "Evidence must start NOT_RUN")
    require([r["id"] for r in evidence["acceptance"]] == group_ids, "Evidence acceptance mismatch")
    require(all(r["status"] == "NOT_RUN" for r in evidence["acceptance"] + evidence["lanes"]), "Premature product PASS/status")
    require(all(r["reproduction_status"] == "NOT_RUN" for r in evidence["findings"]), "Premature reproduction claim")
    checks = load_json(root / "PACKAGE_CHECKS.json")
    require(checks.get("product_status") == "NOT_RUN", "Package checks are not product tests")
    check_links(root, files)
    return {"files": len(files), "acceptance_groups": len(groups), "carry_forward": len(rows), "sources": len(ids)}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    args = parser.parse_args()
    try:
        result = verify(args.root)
    except (PackageError, OSError, KeyError, TypeError, csv.Error) as exc:
        print(f"PACKAGE FAIL: {exc}", file=sys.stderr)
        return 1
    print("PACKAGE PASS (instruction artifact only): " + json.dumps(result, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
