#!/usr/bin/env python3
"""Validate the structural and semantic readiness of the shared-provider bundle."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import subprocess
import sys
from collections import defaultdict, deque
from pathlib import Path

REQUIRED_ROOT = [
    "README.md",
    "START-CODEX-PROMPT.md",
    "CODEX-EXECUTION-CONTRACT.md",
    "STATUS.md",
    "bundle.json",
    "test-budget.json",
    "EXECUTION-REPORT.md",
    "CLOSURE.md",
    "inputs/00-user-request-verbatim.md",
    "requirements/01-functional-requirements.md",
    "requirements/02-security-and-nonfunctional.md",
    "architecture/00-csharp-current-state-inventory.md",
    "architecture/01-csharp-boundary-map.md",
    "architecture/02-csharp-dependency-direction.md",
    "architecture/03-csharp-pattern-selection-records.md",
    "architecture/04-csharp-testability-plan.md",
    "plan/architecture-checkpoints.md",
    "traceability/00-input-coverage.md",
    "traceability/01-requirements-matrix.md",
    "reviews/csharp-architecture-gate.md",
]

REQUIRED_SUBBUNDLE_HEADINGS = [
    "## Objective",
    "## Observable outcome",
    "## Scope",
    "## Out of scope",
    "## C# Architecture Impact",
    "## Boundary Ownership",
    "## Dependency Direction",
    "## Pattern Decision",
    "## Testability Contract",
    "## Partial Class Policy",
    "## Architecture Proof Required",
    "## Test selection",
    "## Acceptance criteria",
    "## Negative proof",
    "## Semantic invariants",
    "## Progression gate",
    "## Reopen triggers",
]

EXPECTED_IDS = [f"SB{i:02d}" for i in range(13)]
GOVERNED_PROOF_TIERS = {"governed"}


def load_json(path: Path, errors: list[str]) -> dict:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except Exception as exc:
        errors.append(f"Invalid JSON {path}: {exc}")
        return {}


def parse_status(path: Path) -> dict[str, str]:
    result: dict[str, str] = {}
    for line in path.read_text(encoding="utf-8").splitlines():
        match = re.match(r"\|\s*(SB\d{2})\s*\|\s*`([^`]+)`\s*\|", line)
        if match:
            result[match.group(1)] = match.group(2)
    return result


def parse_status_dependencies(path: Path) -> dict[str, list[str]]:
    result: dict[str, list[str]] = {}
    for line in path.read_text(encoding="utf-8").splitlines():
        match = re.match(r"\|\s*(SB\d{2})\s*\|\s*`[^`]+`\s*\|\s*([^|]+)\|", line)
        if not match:
            continue
        raw = match.group(2).strip()
        result[match.group(1)] = [] if raw.lower() == "none" else [part.strip() for part in raw.split(",")]
    return result


def parse_mermaid_dependencies(path: Path) -> dict[str, list[str]]:
    result = {sid: [] for sid in EXPECTED_IDS}
    content = path.read_text(encoding="utf-8")
    for dependency, dependent in re.findall(r"\b(SB\d{2})\s*-->\s*(SB\d{2})\b", content):
        result.setdefault(dependent, []).append(dependency)
    return result


def validate_file_manifest(root: Path, errors: list[str]) -> None:
    manifest_path = root / "bundle-file-manifest.json"
    manifest = load_json(manifest_path, errors)
    entries = manifest.get("files", [])
    if manifest.get("algorithm") != "SHA-256" or manifest.get("selfExcluded") is not True:
        errors.append("bundle-file-manifest.json must be a self-excluded SHA-256 inventory")
    if manifest.get("fileCount") != len(entries):
        errors.append("bundle file manifest count does not match its entries")

    declared: set[str] = set()
    total_bytes = 0
    for entry in entries:
        rel = str(entry.get("path", ""))
        if not rel or rel in declared:
            errors.append(f"Bundle file manifest has missing or duplicate path: {rel!r}")
            continue
        declared.add(rel)
        path = root / Path(rel)
        if not path.is_file():
            errors.append(f"Bundle file manifest path is missing: {rel}")
            continue
        content = path.read_bytes()
        total_bytes += len(content)
        if entry.get("sizeBytes") != len(content):
            errors.append(f"Bundle file manifest size mismatch: {rel}")
        if entry.get("sha256") != hashlib.sha256(content).hexdigest():
            errors.append(f"Bundle file manifest hash mismatch: {rel}")

    current = {
        path.relative_to(root).as_posix()
        for path in root.rglob("*")
        if path.is_file()
        and path.name != "bundle-file-manifest.json"
        and "__pycache__" not in path.parts
        and path.suffix != ".pyc"
    }
    for rel in sorted(current - declared):
        errors.append(f"Bundle file is missing from the file manifest: {rel}")
    for rel in sorted(declared - current):
        errors.append(f"Bundle file manifest has stale path: {rel}")
    if manifest.get("totalContentBytes") != total_bytes:
        errors.append("Bundle file manifest totalContentBytes does not match current content")

    git_root_result = subprocess.run(
        ["git", "-C", str(root), "rev-parse", "--show-toplevel"],
        capture_output=True,
        text=True,
        check=False,
    )
    if git_root_result.returncode != 0:
        return
    git_root = Path(git_root_result.stdout.strip())
    for rel in sorted(declared):
        repo_rel = (root / rel).relative_to(git_root).as_posix()
        ignored = subprocess.run(
            ["git", "-C", str(git_root), "check-ignore", "-q", "--", repo_rel],
            capture_output=True,
            check=False,
        )
        if ignored.returncode == 0:
            errors.append(f"Manifest-declared bundle file is ignored by Git: {repo_rel}")


def validate_graph(nodes: dict[str, list[str]], errors: list[str]) -> None:
    incoming = {node: 0 for node in nodes}
    outgoing: dict[str, list[str]] = defaultdict(list)
    for node, deps in nodes.items():
        for dep in deps:
            if dep not in nodes:
                errors.append(f"{node} depends on unknown {dep}")
                continue
            incoming[node] += 1
            outgoing[dep].append(node)
    queue = deque(sorted(node for node, count in incoming.items() if count == 0))
    visited: list[str] = []
    while queue:
        node = queue.popleft()
        visited.append(node)
        for child in outgoing[node]:
            incoming[child] -= 1
            if incoming[child] == 0:
                queue.append(child)
    if len(visited) != len(nodes):
        errors.append("Subbundle dependency graph contains a cycle")


def validate_requirements(root: Path, errors: list[str]) -> None:
    functional = (root / "requirements/01-functional-requirements.md").read_text(encoding="utf-8")
    nonfunctional = (root / "requirements/02-security-and-nonfunctional.md").read_text(encoding="utf-8")
    matrix = (root / "traceability/01-requirements-matrix.md").read_text(encoding="utf-8")
    for i in range(1, 62):
        rid = f"FR-{i:03d}"
        if rid not in functional:
            errors.append(f"Missing functional requirement definition {rid}")
        if rid not in matrix:
            errors.append(f"Missing functional requirement matrix row {rid}")
    for i in range(1, 38):
        rid = f"NFR-{i:03d}"
        if rid not in nonfunctional:
            errors.append(f"Missing non-functional requirement definition {rid}")
        if rid not in matrix:
            errors.append(f"Missing non-functional requirement matrix row {rid}")


def validate_bundle(root: Path, stage: str) -> tuple[list[str], list[str]]:
    errors: list[str] = []
    warnings: list[str] = []

    for rel in REQUIRED_ROOT:
        path = root / rel
        if not path.is_file():
            errors.append(f"Missing required file: {rel}")

    if errors:
        return errors, warnings

    manifest = load_json(root / "bundle.json", errors)
    budget = load_json(root / "test-budget.json", errors)
    if manifest.get("initialReadySubbundle") != "SB00":
        errors.append("bundle.json must declare SB00 as initial ready subbundle")
    if manifest.get("backendUiGate") != "SB07":
        errors.append("bundle.json must declare SB07 as backend UI gate")
    if budget.get("laneLimits", {}).get("stableAggregateRuns") != 1:
        errors.append("test budget must allow exactly one stable aggregate run")
    if budget.get("dockerPolicy", {}).get("canDoItAllApplicationInstances") != 3:
        errors.append("test budget must require three CanDoItAll application instances")
    if budget.get("dockerPolicy", {}).get("leaveFinalStackRunning") is not True:
        errors.append("test budget must require final stack to remain running")

    status_path = root / "STATUS.md"
    status = parse_status(status_path)
    status_dependencies = parse_status_dependencies(status_path)
    if sorted(status) != EXPECTED_IDS:
        errors.append(f"STATUS.md must list exactly {EXPECTED_IDS}")
    if stage == "prepared":
        ready = [sid for sid, state in status.items() if state == "READY"]
        if ready != ["SB00"]:
            errors.append(f"Initial status must have only SB00 READY, found {ready}")
        for sid in EXPECTED_IDS[1:]:
            if status.get(sid) != "LOCKED":
                errors.append(f"{sid} must initially be LOCKED")
    else:
        for sid in EXPECTED_IDS:
            if status.get(sid) != "DONE":
                errors.append(f"Completed stage requires {sid} DONE, found {status.get(sid)}")
        if "Overall state: `COMPLETE`" not in status_path.read_text(encoding="utf-8"):
            errors.append("Completed stage requires STATUS.md Overall state COMPLETE")

    sub_root = root / "subbundles"
    dirs = sorted(path for path in sub_root.iterdir() if path.is_dir())
    by_id: dict[str, Path] = {}
    deps: dict[str, list[str]] = {}
    for path in dirs:
        match = re.match(r"(SB\d{2})-", path.name)
        if not match:
            warnings.append(f"Ignoring non-subbundle directory {path.name}")
            continue
        sid = match.group(1)
        if sid in by_id:
            errors.append(f"Duplicate subbundle ID {sid}")
        by_id[sid] = path

    if sorted(by_id) != EXPECTED_IDS:
        errors.append(f"Expected subbundles {EXPECTED_IDS}, found {sorted(by_id)}")

    for sid in EXPECTED_IDS:
        path = by_id.get(sid)
        if path is None:
            continue
        for rel in ["README.md", "SESSION-HANDOFF.md", "test-selection.json", "proof/proof-manifest.json"]:
            if not (path / rel).is_file():
                errors.append(f"{sid} missing {rel}")
        if not (path / "README.md").is_file():
            continue
        readme = (path / "README.md").read_text(encoding="utf-8")
        for heading in REQUIRED_SUBBUNDLE_HEADINGS:
            if heading not in readme:
                errors.append(f"{sid} README missing heading {heading}")

        selection = load_json(path / "test-selection.json", errors)
        proof = load_json(path / "proof/proof-manifest.json", errors)
        if selection.get("subbundleId") != sid:
            errors.append(f"{sid} test-selection has wrong ID")
        if proof.get("subbundleId") != sid:
            errors.append(f"{sid} proof manifest has wrong ID")
        deps[sid] = list(proof.get("dependencies", []))
        selections = selection.get("selections", [])
        invalidation_keys = selection.get("invalidationKeys", [])
        if not invalidation_keys or not all(str(key).strip() for key in invalidation_keys):
            errors.append(f"{sid} test selection lacks explicit invalidation keys")
        if not str(selection.get("broadGateDecision", "")).strip():
            errors.append(f"{sid} test selection lacks an explicit broad-gate decision")
        if not selections:
            errors.append(f"{sid} has no test/non-test selection")
        for item in selections:
            expected = item.get("plannedExpectedDiscovery")
            if expected is None:
                errors.append(f"{sid} selection {item.get('topic')} lacks planned expected discovery")
            elif expected == 0:
                errors.append(f"{sid} selection {item.get('topic')} has zero planned discovery")
            elif expected < 0 and sid != "SB12":
                errors.append(f"{sid} selection {item.get('topic')} uses negative discovery outside final broad gate")
            for key in ("topic", "project", "filter", "selectionReason"):
                if not str(item.get(key, "")).strip():
                    errors.append(f"{sid} selection lacks {key}")

        if sid == "SB09" and selection.get("playwrightAllowed") is not True:
            errors.append("SB09 must own the focused Playwright lane")
        if sid not in ("SB09",) and selection.get("playwrightAllowed") is True:
            errors.append(f"{sid} may not own Playwright")
        if sid in ("SB07", "SB12") and selection.get("multiInstanceAllowed") is not True:
            errors.append(f"{sid} must own a multi-instance lane")
        if sid not in ("SB07", "SB12") and selection.get("multiInstanceAllowed") is True:
            errors.append(f"{sid} may not own multi-instance lane")
        if sid == "SB12" and selection.get("broadGateAllowed") is not True:
            errors.append("SB12 must own final broad gate")
        if sid != "SB12" and selection.get("broadGateAllowed") is True:
            errors.append(f"{sid} may not own broad gate")

        invariants = proof.get("semanticInvariants", [])
        if not invariants:
            errors.append(f"{sid} proof manifest lacks semantic invariants")

        proof_tier = str(proof.get("proofTier", "")).lower()
        if stage == "completed":
            proof_status = str(proof.get("status", "")).lower()
            if proof_status not in {"passed", "complete", "completed", "done"}:
                errors.append(f"{sid} completed proof manifest has non-passing status {proof_status!r}")
            progression = str(proof.get("progressionDecision", {}).get("result", "")).lower()
            if progression not in {"pass", "passed", "complete", "completed", "done"}:
                errors.append(f"{sid} completed proof manifest has non-passing progression {progression!r}")
            for item in proof.get("testSelections", []):
                expected = item.get("plannedExpectedDiscovery")
                actual = item.get("actualDiscovery")
                if not isinstance(actual, int) or actual <= 0:
                    errors.append(f"{sid} proof topic {item.get('topic')} has zero or missing actual discovery")
                elif isinstance(expected, int) and expected >= 0 and actual != expected:
                    errors.append(
                        f"{sid} proof topic {item.get('topic')} expected {expected} but discovered {actual}"
                    )
                if item.get("exitCode") != 0 or item.get("failed") != 0:
                    errors.append(f"{sid} proof topic {item.get('topic')} did not pass cleanly")
                if not str(item.get("transcript", "")).strip():
                    errors.append(f"{sid} proof topic {item.get('topic')} lacks a transcript")
            if proof_tier in GOVERNED_PROOF_TIERS:
                for rel in ("proof/manifest.md", "proof/semantic-invariants.md"):
                    governed_path = path / rel
                    if not governed_path.is_file() or not governed_path.read_text(encoding="utf-8").strip():
                        errors.append(f"{sid} completed Governed proof is missing {rel}")

    if deps:
        validate_graph(deps, errors)
        if deps.get("SB00") != []:
            errors.append("SB00 must have no dependency")
        if deps.get("SB08") != ["SB07"]:
            errors.append("SB08 must be locked directly behind SB07 backend gate")
        if deps.get("SB12") != ["SB11"]:
            errors.append("SB12 must depend on SB11")
        mermaid_dependencies = parse_mermaid_dependencies(root / "plan/00-dependency-graph.md")
        for sid in EXPECTED_IDS:
            expected = sorted(deps.get(sid, []))
            if sorted(status_dependencies.get(sid, [])) != expected:
                errors.append(
                    f"{sid} STATUS dependencies {status_dependencies.get(sid, [])} do not match proof dependencies {expected}"
                )
            if sorted(mermaid_dependencies.get(sid, [])) != expected:
                errors.append(
                    f"{sid} Mermaid dependencies {mermaid_dependencies.get(sid, [])} do not match proof dependencies {expected}"
                )

    validate_requirements(root, errors)

    execution = (root / "CODEX-EXECUTION-CONTRACT.md").read_text(encoding="utf-8")
    for phrase in [
        "All source-code comments must be in English",
        "PostgreSQL",
        "Do not start a locked subbundle",
        "leave",
        "CanDoItAll-Access-Context-Ref",
    ]:
        if phrase not in execution:
            errors.append(f"Execution contract missing required phrase: {phrase}")

    if not manifest.get("governedProofContract", {}).get("requiredManifestSections"):
        errors.append("bundle.json lacks the current Governed proof contract")
    if "## Subbundle Gate Results" not in (root / "EXECUTION-REPORT.md").read_text(encoding="utf-8"):
        errors.append("EXECUTION-REPORT.md lacks Subbundle Gate Results")
    if "## Browser Validation Analytics" not in (root / "EXECUTION-REPORT.md").read_text(encoding="utf-8"):
        errors.append("EXECUTION-REPORT.md lacks Browser Validation Analytics")

    sb12 = by_id.get("SB12")
    if sb12 is not None:
        sb12_proof = load_json(sb12 / "proof/proof-manifest.json", errors)
        broad_gate = sb12_proof.get("broadGate", {})
        if broad_gate.get("frozenCheckpoint") != "SB12-FINAL-FROZEN-V1":
            errors.append("SB12 broad gate lacks the named SB12-FINAL-FROZEN-V1 checkpoint")
        if not broad_gate.get("namedInvalidationTriggers"):
            errors.append("SB12 broad gate lacks named invalidation triggers")

    if stage == "completed":
        matrix = (root / "traceability/01-requirements-matrix.md").read_text(encoding="utf-8")
        for line in matrix.splitlines():
            if not re.match(r"\|\s*(?:FR|NFR)-\d{3}\s*\|", line):
                continue
            cells = [cell.strip() for cell in line.strip().strip("|").split("|")]
            if len(cells) < 5 or cells[3] != "Solved" or not cells[4] or cells[4] == "not executed":
                errors.append(f"Completed requirement row is not Solved with evidence: {cells[0] if cells else line}")
        closure = (root / "CLOSURE.md").read_text(encoding="utf-8")
        if "State: `COMPLETE`" not in closure or "NOT_EXECUTED" in closure:
            errors.append("Completed stage requires CLOSURE.md State COMPLETE without NOT_EXECUTED")
        report = (root / "EXECUTION-REPORT.md").read_text(encoding="utf-8")
        for stale in ("Not executed", "Locked | none", "planned | Not executed"):
            if stale in report:
                errors.append(f"Completed execution report retains stale marker: {stale}")

    validate_file_manifest(root, errors)

    return errors, warnings


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--stage", choices=("prepared", "completed"), default="prepared")
    parser.add_argument("bundle", nargs="?", default=".", help="Bundle root")
    args = parser.parse_args()
    root = Path(args.bundle).resolve()
    errors, warnings = validate_bundle(root, args.stage)
    for warning in warnings:
        print(f"WARNING: {warning}")
    if errors:
        for error in errors:
            print(f"ERROR: {error}")
        print(f"FAIL: {len(errors)} error(s), {len(warnings)} warning(s)")
        return 1
    print(f"PASS: bundle {args.stage} gate passed ({len(warnings)} warning(s))")
    return 0


if __name__ == "__main__":
    sys.exit(main())
