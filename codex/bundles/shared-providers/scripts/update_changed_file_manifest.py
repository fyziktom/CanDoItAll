#!/usr/bin/env python3
"""Generate a Git-derived before/after SHA-256 inventory for the current worktree."""

from __future__ import annotations

import argparse
import hashlib
import subprocess
from pathlib import Path


def git(root: Path, *args: str) -> subprocess.CompletedProcess[bytes]:
    return subprocess.run(
        ["git", "-C", str(root), *args],
        capture_output=True,
        check=False,
    )


def split_zero(value: bytes) -> list[str]:
    return [part.decode("utf-8") for part in value.split(b"\0") if part]


def sha256(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True)
    parser.add_argument("repository")
    args = parser.parse_args()

    root = Path(args.repository).resolve()
    output = Path(args.output).resolve()
    try:
        output_relative = output.relative_to(root).as_posix()
    except ValueError as exc:
        parser.error(f"output must be inside repository: {exc}")

    tracked = git(root, "diff", "--name-status", "-z", "HEAD")
    untracked = git(root, "ls-files", "--others", "--exclude-standard", "-z")
    if tracked.returncode != 0 or untracked.returncode != 0:
        print((tracked.stderr + untracked.stderr).decode("utf-8", errors="replace"))
        return 1

    tracked_parts = split_zero(tracked.stdout)
    rows: dict[str, str] = {}
    index = 0
    while index < len(tracked_parts):
        status = tracked_parts[index]
        path = tracked_parts[index + 1]
        index += 2
        if status.startswith(("R", "C")):
            path = tracked_parts[index]
            index += 1
        rows[path] = status

    for path in split_zero(untracked.stdout):
        rows.setdefault(path, "?")
    rows.pop(output_relative, None)
    for path in list(rows):
        if path.endswith("bundle-file-manifest.json") or path.endswith("/proof/hashes.sha256"):
            rows.pop(path)

    subbundle_id = next(
        (
            part.split("-", 1)[0]
            for part in output.parts
            if part.startswith("SB") and "-" in part
        ),
        "Bundle",
    )

    lines = [
        f"# {subbundle_id} complete changed-file inventory",
        "",
        "Generated from the current CanDoItAll worktree against `HEAD`.",
        "The output file and recursively derived bundle/proof hash manifests are self-excluded.",
        "Those manifests validate the final after-state separately. `absent` means that side has no file bytes.",
        "",
        "| Status | Repository path | Before SHA-256 | After SHA-256 |",
        "| --- | --- | --- | --- |",
    ]
    for path, status in sorted(rows.items()):
        before = git(root, "show", f"HEAD:{path}")
        before_hash = sha256(before.stdout) if before.returncode == 0 else "absent"
        current = root / Path(path)
        after_hash = sha256(current.read_bytes()) if current.is_file() else "absent"
        lines.append(f"| `{status}` | `{path}` | `{before_hash}` | `{after_hash}` |")

    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"Wrote {output} with {len(rows)} changed/untracked files.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
