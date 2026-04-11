#!/usr/bin/env python3
import json
import sys
from pathlib import Path

def load_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))

def main():
    repo_root = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else Path.cwd()
    manifest_path = Path(sys.argv[2]).resolve() if len(sys.argv) > 2 else repo_root / "cdi_process_templates_bundle" / "apply-manifest.json"

    if not manifest_path.exists():
        print(json.dumps({
            "ok": False,
            "error": f"Apply manifest not found: {manifest_path}"
        }, indent=2))
        return 1

    manifest = load_json(manifest_path)
    rows = []
    missing = 0
    for entry in manifest.get("Entries", []):
        target_path = entry.get("TargetPath", "").replace("\\", "/")
        target = repo_root / target_path
        status = "present" if target.exists() else "missing"
        if status == "missing":
            missing += 1
        rows.append({
            "targetPath": target_path,
            "status": status,
            "sourceBundlePath": entry.get("BundlePath", "").replace("\\", "/"),
            "mode": entry.get("Mode", "")
        })

    print(json.dumps({
        "ok": missing == 0,
        "repoRoot": str(repo_root),
        "manifestPath": str(manifest_path),
        "entryCount": len(rows),
        "missingCount": missing,
        "rows": rows
    }, indent=2))
    return 0 if missing == 0 else 2

if __name__ == "__main__":
    raise SystemExit(main())
