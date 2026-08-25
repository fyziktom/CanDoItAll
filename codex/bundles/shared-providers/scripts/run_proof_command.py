#!/usr/bin/env python3
"""Run a proof command and capture a portable transcript with its exit code."""

from __future__ import annotations

import argparse
import datetime as dt
import shlex
import subprocess
from pathlib import Path


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True)
    parser.add_argument("--cwd", required=True)
    parser.add_argument("command", nargs=argparse.REMAINDER)
    args = parser.parse_args()
    command = args.command[1:] if args.command[:1] == ["--"] else args.command
    if not command:
        parser.error("a command is required after --")

    cwd = Path(args.cwd).resolve()
    output = Path(args.output).resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    started = dt.datetime.now(dt.timezone.utc)
    process = subprocess.run(
        command,
        cwd=cwd,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        check=False,
    )
    completed = dt.datetime.now(dt.timezone.utc)
    transcript = "\n".join(
        [
            f"command: {shlex.join(command)}",
            f"working-directory: {cwd}",
            f"started-utc: {started.isoformat()}",
            f"completed-utc: {completed.isoformat()}",
            f"duration-seconds: {(completed - started).total_seconds():.3f}",
            f"exit-code: {process.returncode}",
            "output:",
            process.stdout,
            "stderr:",
            process.stderr,
        ]
    )
    output.write_text(transcript, encoding="utf-8")
    print(transcript)
    return process.returncode


if __name__ == "__main__":
    raise SystemExit(main())
