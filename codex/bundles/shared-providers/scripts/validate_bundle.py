#!/usr/bin/env python3
"""Validate the structural and semantic readiness of the shared-provider bundle."""

from __future__ import annotations

import argparse
import json
import re
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
    for i in range(1, 61):
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


def validate_bundle(root: Path) -> tuple[list[str], list[str]]:
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

    status = parse_status(root / "STATUS.md")
    if sorted(status) != EXPECTED_IDS:
        errors.append(f"STATUS.md must list exactly {EXPECTED_IDS}")
    ready = [sid for sid, state in status.items() if state == "READY"]
    if ready != ["SB00"]:
        errors.append(f"Initial status must have only SB00 READY, found {ready}")
    for sid in EXPECTED_IDS[1:]:
        if status.get(sid) != "LOCKED":
            errors.append(f"{sid} must initially be LOCKED")

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

    if deps:
        validate_graph(deps, errors)
        if deps.get("SB00") != []:
            errors.append("SB00 must have no dependency")
        if deps.get("SB08") != ["SB07"]:
            errors.append("SB08 must be locked directly behind SB07 backend gate")
        if deps.get("SB12") != ["SB11"]:
            errors.append("SB12 must depend on SB11")

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

    return errors, warnings


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("bundle", nargs="?", default=".", help="Bundle root")
    args = parser.parse_args()
    root = Path(args.bundle).resolve()
    errors, warnings = validate_bundle(root)
    for warning in warnings:
        print(f"WARNING: {warning}")
    if errors:
        for error in errors:
            print(f"ERROR: {error}")
        print(f"FAIL: {len(errors)} error(s), {len(warnings)} warning(s)")
        return 1
    print(f"PASS: bundle is structurally ready ({len(warnings)} warning(s))")
    return 0


if __name__ == "__main__":
    sys.exit(main())
