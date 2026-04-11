#!/usr/bin/env python3
import json
import sys
from pathlib import Path

def main():
    repo_root = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else Path.cwd()
    source_root = repo_root / "src" / "CanDoItAll.Modules.Processes"
    threshold = int(sys.argv[2]) if len(sys.argv) > 2 else 500

    rows = []
    for path in sorted(source_root.rglob("*.cs")):
        line_count = sum(1 for _ in path.open("r", encoding="utf-8"))
        if line_count >= threshold:
            rows.append({
                "path": str(path.relative_to(repo_root)).replace("\\", "/"),
                "lineCount": line_count
            })

    print(json.dumps({
        "repoRoot": str(repo_root),
        "threshold": threshold,
        "fileCount": len(rows),
        "rows": rows
    }, indent=2))
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
