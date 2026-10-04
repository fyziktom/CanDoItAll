#!/usr/bin/env python3
"""Inventory tracked Agents-related Razor files; do not infer reachability or architecture."""
from __future__ import annotations
import argparse
import csv
import hashlib
import re
import subprocess
from pathlib import Path

FIELDS = ["path", "sha256", "lines", "inject_markers", "route_markers", "classification", "actual_callers", "renderer_owner", "native_owner", "proof_refs"]
ROOTS = ("src/Modules/CanDoItAll.Modules.AgentFramework/", "src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/", "src/MAF/Common/CanDoItAll.AgentFramework.Components/", "src/MAF/SimpleChats/")

def selected(name: str) -> bool:
    return name.endswith(".razor") and (name.startswith(ROOTS) or name.startswith("src/UI/CanDoItAll.AgentFramework"))

def inventory(repo: Path, names: list[str]) -> list[dict[str, object]]:
    root = repo.resolve()
    rows = []
    for name in sorted(set(names)):
        if not selected(name):
            continue
        path = (root / name).resolve()
        if not path.is_relative_to(root):
            raise ValueError("Tracked path escapes repository")
        data = path.read_bytes()
        text = data.decode("utf-8-sig")
        rows.append(dict(path=name, sha256=hashlib.sha256(data).hexdigest(), lines=len(text.splitlines()),
            inject_markers=len(re.findall(r"(?m)^\s*@inject\b", text)),
            route_markers=len(re.findall(r"(?m)^\s*@page\b", text)), classification="PENDING",
            actual_callers="", renderer_owner="", native_owner="", proof_refs=""))
    return rows

def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--overwrite", action="store_true")
    args = parser.parse_args()
    if args.output.exists() and not args.overwrite:
        parser.error("Output exists; use a new artifact path or explicitly authorize overwrite")
    result = subprocess.run(["git", "-C", str(args.repo), "ls-files", "-z", "--", "src"], check=True, capture_output=True)
    names = [name.decode("utf-8") for name in result.stdout.split(b"\0") if name]
    rows = inventory(args.repo, names)
    if not rows:
        raise ValueError("No tracked Agents Razor files found; verify the checkout")
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open("w", encoding="utf-8", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=FIELDS)
        writer.writeheader()
        writer.writerows(rows)
    print(f"Recorded {len(rows)} candidate files; every classification remains PENDING.")
    print("Static path/marker inventory only; inspect descendants, dynamic callers, assets and evaluated dependencies.")
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
