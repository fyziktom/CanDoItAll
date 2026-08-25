#!/usr/bin/env python3
"""Validate one shared-provider subbundle at entry or closure."""

from __future__ import annotations

import argparse
import json
import re
from pathlib import Path


def load_json(path: Path, errors: list[str]) -> dict:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except Exception as exc:
        errors.append(f"Invalid JSON {path}: {exc}")
        return {}


def parse_status(path: Path) -> dict[str, str]:
    states: dict[str, str] = {}
    for line in path.read_text(encoding="utf-8").splitlines():
        match = re.match(r"\|\s*(SB\d{2})\s*\|\s*`([^`]+)`\s*\|", line)
        if match:
            states[match.group(1)] = match.group(2)
    return states


def resolve_bundle_artifact(root: Path, value: str) -> Path | None:
    prefix = "bundle://"
    if value.startswith(prefix):
        return root / value.removeprefix(prefix)
    return None


def validate(bundle: Path, subbundle_id: str, phase: str) -> list[str]:
    errors: list[str] = []
    matches = list((bundle / "subbundles").glob(f"{subbundle_id}-*"))
    if len(matches) != 1:
        return [f"Expected one directory for {subbundle_id}, found {len(matches)}"]

    subbundle = matches[0]
    required = [
        "README.md",
        "SESSION-HANDOFF.md",
        "test-selection.json",
        "proof/proof-manifest.json",
        "proof/README.md",
    ]
    for relative in required:
        if not (subbundle / relative).is_file():
            errors.append(f"Missing {subbundle_id} file: {relative}")
    if errors:
        return errors

    status = parse_status(bundle / "STATUS.md")
    state = status.get(subbundle_id)
    proof = load_json(subbundle / "proof/proof-manifest.json", errors)
    selection = load_json(subbundle / "test-selection.json", errors)
    if proof.get("subbundleId") != subbundle_id:
        errors.append("Proof manifest subbundle ID does not match")
    if selection.get("subbundleId") != subbundle_id:
        errors.append("Test selection subbundle ID does not match")

    active = [sid for sid, current in status.items() if current in {"READY", "IN_PROGRESS"}]
    if phase == "entry":
        if state not in {"READY", "IN_PROGRESS"}:
            errors.append(f"Entry requires {subbundle_id} READY or IN_PROGRESS, found {state}")
        if active != [subbundle_id]:
            errors.append(f"Entry requires exactly {subbundle_id} active, found {active}")
        for dependency in proof.get("dependencies", []):
            if status.get(dependency) != "DONE":
                errors.append(f"Dependency {dependency} is not DONE")
        if not selection.get("selections"):
            errors.append("Test selection is empty")
        if not selection.get("invalidationKeys"):
            errors.append("Invalidation keys are empty")
        return errors

    if state not in {"IN_PROGRESS", "DONE"}:
        errors.append(f"Closure requires {subbundle_id} IN_PROGRESS or DONE, found {state}")
    if str(proof.get("status", "")).lower() not in {"passed", "complete", "completed", "done"}:
        errors.append("Proof manifest status is not passing")
    progression = str(proof.get("progressionDecision", {}).get("result", "")).lower()
    if progression not in {"pass", "passed", "complete", "completed", "done"}:
        errors.append("Progression decision is not passing")

    planned_by_topic = {
        item.get("topic"): item.get("plannedExpectedDiscovery")
        for item in selection.get("selections", [])
    }
    proof_topics = proof.get("testSelections", [])
    if set(planned_by_topic) != {item.get("topic") for item in proof_topics}:
        errors.append("Proof topics do not exactly match selected topics")
    for item in proof_topics:
        topic = item.get("topic")
        expected = planned_by_topic.get(topic)
        actual = item.get("actualDiscovery")
        if not isinstance(actual, int) or actual <= 0:
            errors.append(f"{topic} has missing or zero discovery")
        elif isinstance(expected, int) and expected >= 0 and actual != expected:
            errors.append(f"{topic} expected {expected} but discovered {actual}")
        if item.get("exitCode") != 0 or item.get("failed") != 0:
            errors.append(f"{topic} did not pass cleanly")
        transcript = str(item.get("transcript", ""))
        transcript_path = resolve_bundle_artifact(bundle, transcript)
        if transcript_path is None or not transcript_path.is_file():
            errors.append(f"{topic} transcript is missing: {transcript!r}")

    proof_tier = str(proof.get("proofTier", "")).lower()
    if proof_tier == "governed":
        for relative in ("proof/manifest.md", "proof/semantic-invariants.md"):
            path = subbundle / relative
            if not path.is_file() or not path.read_text(encoding="utf-8").strip():
                errors.append(f"Governed closure is missing {relative}")

    proof_readme = (subbundle / "proof/README.md").read_text(encoding="utf-8")
    handoff = (subbundle / "SESSION-HANDOFF.md").read_text(encoding="utf-8")
    if "NOT_EXECUTED" in proof_readme or "NOT_EXECUTED" in handoff:
        errors.append("Closure records retain NOT_EXECUTED")
    if not proof.get("positiveEvidence") or not proof.get("negativeEvidence"):
        errors.append("Positive or negative evidence is empty")
    if not proof.get("artifacts"):
        errors.append("Artifact inventory is empty")
    return errors


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--phase", choices=("entry", "closure"), required=True)
    parser.add_argument("--subbundle", required=True, choices=[f"SB{i:02d}" for i in range(13)])
    parser.add_argument("bundle", nargs="?", default=".")
    args = parser.parse_args()

    errors = validate(Path(args.bundle).resolve(), args.subbundle, args.phase)
    if errors:
        for error in errors:
            print(f"ERROR: {error}")
        print(f"FAIL: {len(errors)} error(s)")
        return 1
    print(f"PASS: {args.subbundle} {args.phase} gate passed")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
