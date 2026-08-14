#!/usr/bin/env python3
"""Enforce the bundle's focused-test budget from proof-manifest commands."""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path


def command_text(command_entry) -> str:
    if isinstance(command_entry, str):
        return command_entry
    if isinstance(command_entry, dict):
        return str(command_entry.get("command", ""))
    return ""


def is_test(command: str) -> bool:
    return bool(re.search(r"\bdotnet\s+test\b", command, re.IGNORECASE))


def is_build(command: str) -> bool:
    return bool(re.search(r"\bdotnet\s+build\b", command, re.IGNORECASE))


def is_filtered(command: str) -> bool:
    return "--filter" in command.lower()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--bundle-root", type=Path, default=Path("."))
    args = parser.parse_args()
    root = args.bundle_root.resolve()
    index = json.loads((root / "bundle-index.json").read_text(encoding="utf-8"))
    budget = json.loads((root / "test-budget.json").read_text(encoding="utf-8"))
    errors: list[str] = []

    normal_max_tests = budget["normalSubbundles"][
        "maxFocusedTestCommandsPerSubbundle"
    ]
    normal_max_builds = budget["normalSubbundles"][
        "maxAffectedBuildCommandsPerSubbundle"
    ]

    for subbundle in index["subbundles"]:
        sb_id = subbundle["id"]
        proof = json.loads(
            (root / subbundle["path"] / "proof-manifest.json").read_text(
                encoding="utf-8"
            )
        )
        commands = [
            command_text(item).strip()
            for item in proof.get("commands", [])
            if command_text(item).strip()
        ]
        tests = [item for item in commands if is_test(item)]
        builds = [item for item in commands if is_build(item)]

        if sb_id != "SB13":
            if len(tests) > normal_max_tests:
                errors.append(
                    f"{sb_id} has {len(tests)} test commands; max is "
                    f"{normal_max_tests}"
                )
            if len(builds) > normal_max_builds:
                errors.append(
                    f"{sb_id} has {len(builds)} build commands; max is "
                    f"{normal_max_builds}"
                )
            for command in tests:
                lower = command.lower()
                if "candoitall.slnx" in lower:
                    errors.append(
                        f"{sb_id} runs a solution-wide test before SB13: {command}"
                    )
                if (
                    "tests.unit.csproj" in lower
                    or "tests.integration.csproj" in lower
                ) and not is_filtered(command):
                    errors.append(
                        f"{sb_id} runs an unfiltered test project: {command}"
                    )
                forbidden_markers = (
                    "playwright",
                    "category=liveprocess",
                    "category=longrunning",
                    "category=quarantined",
                )
                if any(marker in lower for marker in forbidden_markers):
                    errors.append(
                        f"{sb_id} runs a forbidden lane: {command}"
                    )
        else:
            stable_tests = [
                item
                for item in tests
                if "candoitall.slnx" in item.lower()
                and "--filter" in item.lower()
            ]
            if len(stable_tests) > budget["finalSubbundle"][
                "maxStableSolutionTestRuns"
            ]:
                errors.append("SB13 runs the stable solution gate more than once")
            if any(
                "candoitall.slnx" in item.lower() and "--filter" not in item.lower()
                for item in tests
            ):
                errors.append("SB13 includes an unfiltered solution suite")
            if sum(
                1 for item in commands
                if re.search(r"\bdotnet\s+restore\b", item, re.IGNORECASE)
            ) > budget["finalSubbundle"]["maxRestoreRuns"]:
                errors.append("SB13 runs restore more than once")
            if sum(
                1 for item in builds if "candoitall.slnx" in item.lower()
            ) > budget["finalSubbundle"]["maxSolutionBuildRuns"]:
                errors.append("SB13 runs the solution build more than once")

    if errors:
        print("\n".join(f"ERROR: {error}" for error in errors), file=sys.stderr)
        return 1

    print("Test-policy validation passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
