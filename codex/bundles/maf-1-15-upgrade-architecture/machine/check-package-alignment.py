#!/usr/bin/env python3
"""Validate resolved Microsoft Agent Framework package versions in project.assets.json files."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

STABLE_VERSION = "1.15.0"
PREVIEW_VERSION = "1.15.0-preview.260722.1"

KNOWN_STABLE_PACKAGES = {
    "Microsoft.Agents.AI",
    "Microsoft.Agents.AI.Abstractions",
    "Microsoft.Agents.AI.OpenAI",
    "Microsoft.Agents.AI.Workflows",
}

KNOWN_PREVIEW_PACKAGES = {
    "Microsoft.Agents.AI.A2A",
    "Microsoft.Agents.AI.Hosting",
    "Microsoft.Agents.AI.Hosting.A2A",
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "repository_root",
        nargs="?",
        default=".",
        help="Path to the CanDoItAll repository root.",
    )
    return parser.parse_args()


def load_maf_libraries(assets_path: Path) -> dict[str, str]:
    payload = json.loads(assets_path.read_text(encoding="utf-8"))
    result: dict[str, str] = {}

    for key in payload.get("libraries", {}):
        if "/" not in key:
            continue

        package_id, version = key.rsplit("/", 1)
        if package_id.startswith("Microsoft.Agents.AI"):
            result[package_id] = version

    return result


def main() -> int:
    args = parse_args()
    root = Path(args.repository_root).resolve()
    assets_files = [
        path
        for path in root.rglob("project.assets.json")
        if "ExternalPackages" not in path.parts
        and ".artifacts" not in path.parts
    ]

    if not assets_files:
        print("No project.assets.json files found. Run dotnet restore first.", file=sys.stderr)
        return 2

    failures: list[str] = []
    unknown: set[tuple[str, str]] = set()
    observed: dict[str, set[str]] = {}

    for assets_path in assets_files:
        for package_id, version in load_maf_libraries(assets_path).items():
            observed.setdefault(package_id, set()).add(version)

            if package_id in KNOWN_STABLE_PACKAGES:
                if version != STABLE_VERSION:
                    failures.append(
                        f"{assets_path}: {package_id} resolved to {version}, expected {STABLE_VERSION}"
                    )
            elif package_id in KNOWN_PREVIEW_PACKAGES:
                if version != PREVIEW_VERSION:
                    failures.append(
                        f"{assets_path}: {package_id} resolved to {version}, expected {PREVIEW_VERSION}"
                    )
            else:
                unknown.add((package_id, version))

    print("Observed MAF packages:")
    for package_id in sorted(observed):
        versions = ", ".join(sorted(observed[package_id]))
        print(f"  {package_id}: {versions}")

    if unknown:
        print("\nUnknown MAF package IDs require explicit classification:", file=sys.stderr)
        for package_id, version in sorted(unknown):
            print(f"  {package_id}: {version}", file=sys.stderr)
        failures.append("Unknown MAF package IDs were observed.")

    if failures:
        print("\nAlignment failures:", file=sys.stderr)
        for failure in failures:
            print(f"  {failure}", file=sys.stderr)
        return 1

    print("\nMAF package alignment is valid.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
