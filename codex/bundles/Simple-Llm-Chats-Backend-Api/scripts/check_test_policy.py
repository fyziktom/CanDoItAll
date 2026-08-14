#!/usr/bin/env python3
"""Enforce the bundle's narrow-test budget before the final subbundle."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
import re
import sys


UNIT_PROJECT = "CanDoItAll.Tests.Unit.csproj"
INTEGRATION_PROJECT = "CanDoItAll.Tests.Integration.csproj"
SOLUTION = "CanDoItAll.slnx"
FINAL_ID = "SB11"

ALLOWED_POLICY_FILES = {
    "plan/04-test-budget-and-gates.md",
    "plan/06-release-gate.md",
    "subbundles/SB11-final-regression-and-release-gate/README.md",
    "CODEX-EXECUTION-CONTRACT.md",
}


def normalize_command(command: str) -> str:
    return " ".join(command.replace("`\n", " ").replace("\\\n", " ").split())


def is_dotnet_test(command: str) -> bool:
    return bool(re.search(r"\bdotnet\s+test\b", command, re.IGNORECASE))


def has_filter(command: str) -> bool:
    return bool(re.search(r"(?:--filter|-f)\s+", command, re.IGNORECASE))


def broad_reason(command: str) -> str | None:
    normalized = normalize_command(command)
    if not is_dotnet_test(normalized):
        return None
    if SOLUTION.lower() in normalized.lower():
        return "solution-wide dotnet test"
    if UNIT_PROJECT.lower() in normalized.lower() and not has_filter(normalized):
        return "unfiltered Unit project"
    if INTEGRATION_PROJECT.lower() in normalized.lower() and not has_filter(normalized):
        return "unfiltered Integration project"
    if re.search(r"tests[/\\]Playwright", normalized, re.IGNORECASE):
        return "Playwright lane"
    if re.search(r"Category\s*=\s*(LiveProcess|LongRunning|Quarantined)", normalized, re.IGNORECASE):
        return "extended or quarantined lane"
    return None


def markdown_commands(text: str) -> list[str]:
    commands: list[str] = []
    in_fence = False
    buffer: list[str] = []
    for line in text.splitlines():
        if line.strip().startswith("```"):
            if in_fence:
                commands.extend(buffer)
                buffer = []
            in_fence = not in_fence
            continue
        if in_fence and "dotnet test" in line.lower():
            buffer.append(line.strip())
    return commands


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--bundle-root", type=Path, required=True)
    args = parser.parse_args()
    root = args.bundle_root.resolve()

    policy = json.loads((root / "test-budget.json").read_text(encoding="utf-8"))
    maximum_focused = int(policy["normalSubbundle"]["maxFocusedTestCommands"])
    maximum_final_stable = int(policy["finalSubbundle"]["maxStableSolutionTestRuns"])

    errors: list[str] = []
    for path in root.rglob("*.md"):
        relative = path.relative_to(root).as_posix()
        if relative in ALLOWED_POLICY_FILES:
            continue
        for command in markdown_commands(path.read_text(encoding="utf-8", errors="replace")):
            reason = broad_reason(command)
            if reason:
                errors.append(f"Broad test command ({reason}) is forbidden in {relative}: {command}")

    for subbundle in sorted((root / "subbundles").glob("SB*")):
        subbundle_id = subbundle.name.split("-", 1)[0]
        readme = subbundle / "README.md"
        if readme.is_file() and "test-budget.json" not in readme.read_text(encoding="utf-8"):
            errors.append(f"Subbundle does not reference test-budget.json: {subbundle.name}")

        proof = subbundle / "proof" / "proof-manifest.json"
        if not proof.is_file():
            continue
        try:
            data = json.loads(proof.read_text(encoding="utf-8"))
        except json.JSONDecodeError as exc:
            errors.append(f"Invalid proof manifest {proof}: {exc}")
            continue
        commands = [
            str(item.get("command", item)) if isinstance(item, dict) else str(item)
            for item in data.get("commands", [])
        ]
        test_commands = [command for command in commands if is_dotnet_test(command)]
        if subbundle_id != FINAL_ID:
            if len(test_commands) > maximum_focused:
                errors.append(
                    f"{subbundle_id} recorded {len(test_commands)} test commands; limit is {maximum_focused}."
                )
            for command in test_commands:
                reason = broad_reason(command)
                if reason:
                    errors.append(f"{subbundle_id} recorded forbidden {reason}: {command}")
        else:
            stable_runs = sum(
                1 for command in test_commands if SOLUTION.lower() in command.lower()
            )
            if stable_runs > maximum_final_stable:
                errors.append(
                    f"{FINAL_ID} recorded {stable_runs} stable solution test runs; limit is {maximum_final_stable}."
                )

    if errors:
        print("\n".join(errors))
        return 1

    print("Bundle test-policy validation passed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
