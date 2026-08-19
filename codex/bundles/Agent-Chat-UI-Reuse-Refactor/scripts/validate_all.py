from __future__ import annotations

import subprocess
import sys
from pathlib import Path


SCRIPTS = [
    "validate_bundle.py",
    "validate_traceability.py",
    "validate_test_policy.py",
    "validate_phase_exclusions.py",
    "validate_checksums.py",
]


def main() -> int:
    scripts_dir = Path(__file__).resolve().parent
    for name in SCRIPTS:
        process = subprocess.run(
            [sys.executable, str(scripts_dir / name)],
            check=False,
        )
        if process.returncode != 0:
            return process.returncode
    print("All prepared-bundle validations passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
