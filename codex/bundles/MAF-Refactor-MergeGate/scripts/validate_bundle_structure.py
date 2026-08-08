from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
manifest_path = ROOT / "manifest.json"
errors: list[str] = []

try:
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
except Exception as exc:
    print(f"Cannot read manifest: {exc}")
    sys.exit(1)

required_root_files = [
    "00-READ-ME-FIRST.md",
    "01-REVIEW-VERDICT.md",
    "02-FINDINGS-REGISTER.md",
    "03-EXECUTION-ORDER.md",
    "04-CODEX-EXECUTION-GUIDE.md",
    "manifest.json",
]
for rel in required_root_files:
    if not (ROOT / rel).is_file():
        errors.append(f"Missing root file: {rel}")

ids = [item["id"] for item in manifest["subbundles"]]
if ids != manifest["executionOrder"]:
    errors.append("Subbundle order differs from manifest executionOrder.")

for item in manifest["subbundles"]:
    prefix = f"{item['id']}-{item['slug']}"
    directory = ROOT / "subbundles" / prefix
    for name in (
        "README.md",
        "CODEX-PROMPT.md",
        "proof-manifest.template.json",
        "SESSION-HANDOFF.template.md",
    ):
        if not (directory / name).is_file():
            errors.append(f"Missing {prefix}/{name}")
    try:
        json.loads((directory / "proof-manifest.template.json").read_text(encoding="utf-8"))
    except Exception as exc:
        errors.append(f"Invalid proof template for {prefix}: {exc}")

for path in ROOT.rglob("*.json"):
    try:
        json.loads(path.read_text(encoding="utf-8"))
    except Exception as exc:
        errors.append(f"Invalid JSON {path.relative_to(ROOT)}: {exc}")

if errors:
    print("Bundle validation FAILED:")
    for error in errors:
        print(f"- {error}")
    sys.exit(1)

print(
    f"Bundle validation passed: {len(manifest['subbundles'])} subbundles, "
    f"{len(manifest['findings'])} findings."
)
