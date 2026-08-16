#!/usr/bin/env python3
"""Run all prepared-bundle validators."""

from __future__ import annotations

import argparse
import subprocess
import sys
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument('--stage', default='prepared')
args = parser.parse_args()
root = Path(__file__).resolve().parents[1]
commands = [
    [sys.executable, str(root / 'scripts/validate_bundle.py'), '--stage', args.stage],
    [sys.executable, str(root / 'scripts/check_traceability.py')],
    [sys.executable, str(root / 'scripts/check_test_policy.py')],
    [sys.executable, str(root / 'scripts/check_phase_exclusions.py')],
    [sys.executable, str(root / 'scripts/verify_checksums.py')],
]
for command in commands:
    subprocess.run(command, check=True, cwd=root)
print('All prepared-bundle validations passed.')
