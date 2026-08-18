from __future__ import annotations

import hashlib
from pathlib import Path


def main() -> int:
    root = Path(__file__).resolve().parents[1]
    output = root / "CHECKSUMS.sha256"
    entries: list[str] = []

    for path in sorted(root.rglob("*")):
        if not path.is_file():
            continue
        if path == output:
            continue
        if "__pycache__" in path.parts or path.suffix == ".pyc":
            continue
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
        entries.append(f"{digest}  {path.relative_to(root).as_posix()}")

    output.write_text("\n".join(entries) + "\n", encoding="utf-8", newline="\n")
    print(f"Wrote {len(entries)} checksum entries.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
