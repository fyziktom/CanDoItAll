from __future__ import annotations

import argparse
import re
import subprocess
import sys
from pathlib import Path


TEXT_SUFFIXES = {".cs", ".razor", ".csproj", ".props", ".targets", ".razor.css"}

FORBIDDEN_NEUTRAL_PATTERNS = {
    "AgentFramework namespace": re.compile(r"\bCanDoItAll\.AgentFramework\b"),
    "Agent module namespace": re.compile(r"\bCanDoItAll\.Modules\.AgentFramework\b"),
    "Simple Chat module namespace": re.compile(r"\bCanDoItAll\.Modules\.LlmChats\b"),
    "EF Core": re.compile(r"\bMicrosoft\.EntityFrameworkCore\b"),
    "AppDbContext": re.compile(r"\bAppDbContext\b"),
    "service location": re.compile(r"\bIServiceProvider\b|\bGetRequiredService\s*<|\bGetService\s*<"),
    "Simple Chat product type": re.compile(r"\b(?:LlmChatDefinition|ILlmChat\w*|SimpleChat\w*)\b"),
}

FORBIDDEN_PROJECT_REFERENCE_FRAGMENTS = [
    "/src/MAF/",
    "/src/Modules/",
    "/src/Foundation/CanDoItAll.Infrastructure",
    "/src/App/",
    "/src/Processes/",
    "\\src\\MAF\\",
    "\\src\\Modules\\",
    "\\src\\Foundation\\CanDoItAll.Infrastructure",
    "\\src\\App\\",
    "\\src\\Processes\\",
]

UI_DIFF_PREFIXES = (
    "src/UI/",
    "src/MAF/Common/CanDoItAll.AgentFramework.Components/",
    "src/Modules/CanDoItAll.Modules.AgentFramework/",
    "src/Modules/CanDoItAll.Modules.Processes/",
)

FORBIDDEN_UI_DIFF_PATTERNS = {
    "direct Simple Chat namespace": re.compile(r"\bCanDoItAll\.Modules\.LlmChats\b"),
    "direct Simple Chat product type": re.compile(r"\b(?:LlmChatDefinition|ILlmChat\w*|SimpleChat\w*)\b"),
    "visible Simple Chat phase feature": re.compile(
        r"\bSimple Chats?\b|\bAgents\s*/\s*Simple Chats\b|\bAdd context\b",
        re.IGNORECASE,
    ),
}

LARGE_TYPE_PARTIAL_PATTERNS = [
    re.compile(r"(?:^|/)AgentChatPanel\.(?!razor\.cs$).+\.cs$", re.IGNORECASE),
    re.compile(r"(?:^|/)ChatWorkspacePanel\.(?!razor\.cs$).+\.cs$", re.IGNORECASE),
    re.compile(r"(?:^|/)FloatingAgentChatHost\.(?!razor\.cs$).+\.cs$", re.IGNORECASE),
    re.compile(r"(?:^|/)AgentDetailsDialog\.(?!razor\.cs$).+\.cs$", re.IGNORECASE),
]


def run_git(repo: Path, args: list[str]) -> str:
    process = subprocess.run(
        ["git", *args],
        cwd=repo,
        check=False,
        capture_output=True,
        text=True,
    )
    if process.returncode != 0:
        raise RuntimeError(process.stderr.strip() or "git command failed")
    return process.stdout


def inspect_neutral_project(repo: Path, neutral_relative: Path, errors: list[str]) -> None:
    neutral = repo / neutral_relative
    if not neutral.exists():
        errors.append(f"Neutral project path does not exist: {neutral_relative}")
        return

    for path in neutral.rglob("*"):
        if not path.is_file() or path.suffix not in TEXT_SUFFIXES:
            continue
        relative = path.relative_to(repo)
        text = path.read_text(encoding="utf-8", errors="replace")
        for label, pattern in FORBIDDEN_NEUTRAL_PATTERNS.items():
            if pattern.search(text):
                errors.append(f"{relative}: forbidden neutral dependency/source pattern: {label}")

        if path.suffix == ".csproj":
            normalized = text.replace("..", "").replace("\\", "/")
            for fragment in FORBIDDEN_PROJECT_REFERENCE_FRAGMENTS:
                if fragment.replace("\\", "/") in normalized:
                    errors.append(f"{relative}: forbidden project-reference fragment: {fragment}")


def inspect_diff(repo: Path, base_sha: str, errors: list[str]) -> None:
    output = run_git(repo, ["diff", "--name-status", f"{base_sha}...HEAD"])
    entries: list[tuple[str, str]] = []
    for raw in output.splitlines():
        if not raw.strip():
            continue
        parts = raw.split("\t")
        status = parts[0]
        path = parts[-1].replace("\\", "/")
        entries.append((status, path))

    for status, path in entries:
        if path.startswith("src/Modules/CanDoItAll.Modules.LlmChats/"):
            errors.append(f"{path}: Simple Chat backend/product source changed in Phase 1.")

        if status.startswith("A"):
            for pattern in LARGE_TYPE_PARTIAL_PATTERNS:
                if pattern.search(path):
                    errors.append(f"{path}: newly added partial/helper file expands a named large UI type.")

        if not path.startswith(UI_DIFF_PREFIXES):
            continue
        source = repo / path
        if not source.is_file() or source.suffix not in TEXT_SUFFIXES:
            continue
        text = source.read_text(encoding="utf-8", errors="replace")
        for label, pattern in FORBIDDEN_UI_DIFF_PATTERNS.items():
            if pattern.search(text):
                errors.append(f"{path}: forbidden Phase 1 UI pattern: {label}")


def main() -> int:
    parser = argparse.ArgumentParser(description="Check Phase 1 repository architecture and scope boundaries.")
    parser.add_argument("repository_root", type=Path)
    parser.add_argument(
        "--neutral-project",
        type=Path,
        default=Path("src/UI/CanDoItAll.Conversations.Components"),
    )
    parser.add_argument("--base-sha")
    args = parser.parse_args()

    repo = args.repository_root.resolve()
    errors: list[str] = []

    if not (repo / ".git").exists():
        print(f"Not a Git repository root: {repo}", file=sys.stderr)
        return 2

    inspect_neutral_project(repo, args.neutral_project, errors)

    if args.base_sha:
        try:
            inspect_diff(repo, args.base_sha, errors)
        except RuntimeError as exc:
            errors.append(f"Cannot inspect Git diff: {exc}")

    if errors:
        print("Repository boundary guard failed:", file=sys.stderr)
        for error in errors:
            print(f"- {error}", file=sys.stderr)
        return 1

    print("Repository boundary guard passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
