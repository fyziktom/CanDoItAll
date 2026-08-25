#!/usr/bin/env python3
"""Regenerate the SHA-256 inventory for one subbundle proof directory."""

from __future__ import annotations

import argparse
import hashlib
from pathlib import Path


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("proof_directory")
    args = parser.parse_args()

    proof = Path(args.proof_directory).resolve()
    if not proof.is_dir() or proof.name != "proof":
        parser.error("proof_directory must be an existing directory named 'proof'")

    files = sorted(
        path
        for path in proof.rglob("*")
        if path.is_file()
        and path.name != "hashes.sha256"
        and "__pycache__" not in path.parts
        and path.suffix != ".pyc"
    )
    lines = [
        f"{hashlib.sha256(path.read_bytes()).hexdigest()}  {path.relative_to(proof).as_posix()}"
        for path in files
    ]
    target = proof / "hashes.sha256"
    target.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"Wrote {target} with {len(lines)} entries.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
