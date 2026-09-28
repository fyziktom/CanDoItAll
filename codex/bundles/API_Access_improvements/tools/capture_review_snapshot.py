#!/usr/bin/env python3
"""Capture selected read-only Git evidence; never fetch, checkout, or change refs."""
from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import subprocess
import sys
from datetime import datetime, timezone

DEVELOPMENT = "b82ffc57283f5e4819d82322e1c5bf836dcd9536"
UI = "e101d5db1478ea329a572db79c0104b927d97f15"
PATHS = (
    "src/App/CanDoItAll.Web/Api/ApiEndpointRouteBuilderExtensions.cs",
    "src/App/CanDoItAll.Web/Api/ProcessDefinitionsApi.cs",
    "src/App/CanDoItAll.Web/Api/ProcessesApi.cs",
    "src/App/CanDoItAll.Web/Api/ProjectsApi.cs",
    "src/App/CanDoItAll.Web/Api/PromptGalleryApi.cs",
    "src/App/CanDoItAll.Web/Api/WorkflowsApi.cs",
    "src/App/CanDoItAll.Web/Program.cs",
    "src/App/CanDoItAll.Web/ProjectStructureAgentApi.cs",
)


class CaptureError(RuntimeError):
    """A failed validation or Git read operation."""


def run_git(repo: Path, *args: str) -> str:
    env = os.environ.copy()
    env["GIT_OPTIONAL_LOCKS"] = "0"
    env["GIT_TERMINAL_PROMPT"] = "0"
    try:
        result = subprocess.run(
            ["git", "-c", "core.pager=cat", "-C", str(repo), *args],
            check=False, capture_output=True, encoding="utf-8", errors="strict",
            timeout=90, env=env,
        )
    except (OSError, subprocess.TimeoutExpired, UnicodeError) as exc:
        raise CaptureError(f"Git read failed: {type(exc).__name__}.") from exc
    if result.returncode:
        # Avoid printing Git stderr: configuration and remote URLs may contain secrets.
        raise CaptureError(f"Git read '{args[0]}' failed with exit code {result.returncode}.")
    return result.stdout


def resolve_commit(repo: Path, ref: str) -> str:
    if not ref or ref.startswith("-") or any(c in ref for c in "\r\n\x00"):
        raise CaptureError("Invalid Git reference argument.")
    return run_git(repo, "rev-parse", "--verify", "--end-of-options", ref + "^{commit}").strip()


def is_within(child: Path, parent: Path) -> bool:
    return child == parent or parent in child.parents


def capture(repo: Path, out: Path, development: str, ui_ref: str) -> None:
    repo = repo.expanduser().resolve()
    root = Path(run_git(repo, "rev-parse", "--show-toplevel").strip()).resolve()
    git_dir = Path(run_git(root, "rev-parse", "--absolute-git-dir").strip()).resolve()
    common = Path(run_git(root, "rev-parse", "--path-format=absolute", "--git-common-dir").strip()).resolve()
    out = out.expanduser().resolve()
    if any(is_within(out, path) for path in (root, git_dir, common)):
        raise CaptureError("Output must be outside the checkout and its Git directories.")
    if out.exists() and (not out.is_dir() or any(out.iterdir())):
        raise CaptureError("Output must be a new or empty directory.")

    # Read everything before creating any output files.
    dev = resolve_commit(root, development)
    ui = resolve_commit(root, ui_ref)
    base = run_git(root, "merge-base", dev, ui).strip()
    left, right = map(int, run_git(root, "rev-list", "--left-right", "--count", f"{dev}...{ui}").split())
    head_before = resolve_commit(root, "HEAD")
    status_before = run_git(root, "status", "--porcelain=v1", "--untracked-files=normal")
    branch = run_git(root, "rev-parse", "--abbrev-ref", "HEAD").strip()
    diff_args = ("diff", "--no-ext-diff", "--no-textconv", "--no-color")
    ui_delta = run_git(root, *diff_args, base, ui, "--", *PATHS)
    head_comparison = run_git(root, *diff_args, dev, ui, "--", *PATHS)
    stat = run_git(root, *diff_args, "--stat", base, ui, "--", *PATHS)
    head_after = resolve_commit(root, "HEAD")
    status_after = run_git(root, "status", "--porcelain=v1", "--untracked-files=normal")
    if head_before != head_after or status_before != status_after:
        raise CaptureError("Checkout changed during capture; retry at a stable checkpoint.")
    metadata = {
        "createdAtUtc": datetime.now(timezone.utc).isoformat(),
        "repositoryPath": str(root),
        "development": dev,
        "uiRef": ui,
        "mergeBase": base,
        "developmentOnlyCommits": left,
        "uiOnlyCommits": right,
        "checkoutHead": head_before,
        "checkoutBranch": branch,
        "checkoutHasChanges": bool(status_before),
        "checkoutHeadAndStatusUnchanged": True,
        "selectedPaths": list(PATHS),
        "applicationTestsRun": False,
        "warning": "Diffs may contain sensitive tracked source; review before sharing. Do not apply the head-to-head comparison as a patch.",
    }
    files = {
        "metadata.json": json.dumps(metadata, indent=2) + "\n",
        "ui-selected-delta-from-common-base.patch": ui_delta,
        "HEAD_COMPARISON_DO_NOT_APPLY.patch": head_comparison,
        "selected-delta-stat.txt": stat,
        "README.txt": (
            "Read-only Git evidence for selective integration.\n"
            "The UI delta is measured from the common ancestor, not current development.\n"
            "The head-to-head file also includes development-only changes in reverse.\n"
            "Do not apply either file blindly; adapt intent to current owners.\n"
            "Only the eight selected source paths are captured, not the whole branch.\n"
            "No fetch, checkout, ref change, commit, merge or application test was performed.\n"
            "Tracked source can contain secrets. Review before forwarding this directory.\n"
        ),
    }
    out.mkdir(parents=True, exist_ok=True)
    for name, content in files.items():
        # Exclusive creation prevents accidental replacement if another process writes here.
        with (out / name).open("x", encoding="utf-8", newline="\n") as stream:
            stream.write(content)
    print(f"Captured selected Git evidence in {out}")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", required=True, type=Path, help="Existing local Git checkout.")
    parser.add_argument("--out", required=True, type=Path, help="New/empty output directory outside the checkout.")
    parser.add_argument("--development", default=DEVELOPMENT, help="Existing development commit/ref; no network fetch.")
    parser.add_argument("--ui-ref", default=UI, help="Existing UI commit/ref; no network fetch.")
    args = parser.parse_args()
    try:
        capture(args.repo, args.out, args.development, args.ui_ref)
    except (CaptureError, OSError, ValueError) as exc:
        print(f"Capture failed: {exc}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
