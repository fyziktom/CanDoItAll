#!/usr/bin/env python3
"""Check implemented LLM Chat project, composition, API-family, capability, and UI boundaries."""

from __future__ import annotations

import argparse
import os
from pathlib import Path
import re
import subprocess
import sys


FORBIDDEN_DOMAIN_REFERENCES = [
    "CanDoItAll.AgentFramework.Llm.Conversations",
    "CanDoItAll.AgentFramework.Core",
    "CanDoItAll.AgentFramework.Maf",
    "CanDoItAll.AgentFramework.Tooling",
    "CanDoItAll.AgentFramework.Tools",
    "CanDoItAll.AgentFramework.Skills",
    "CanDoItAll.AgentFramework.Mcp",
    "CanDoItAll.AgentFramework.Memory",
    "CanDoItAll.Processes",
    "CanDoItAll.Modules.AgentFramework",
    "CanDoItAll.Modules.Processes",
    "CanDoItAll.Modules.Workbench",
    "CanDoItAll.Modules.Projects",
    "CanDoItAll.Modules.CrmHr",
    "CanDoItAll.Modules.Workspace",
    "Microsoft.EntityFrameworkCore",
    "Microsoft.AspNetCore",
]

FORBIDDEN_PERSISTENCE_REFERENCES = [
    "CanDoItAll.Web",
    "CanDoItAll.AgentFramework.Maf",
    "CanDoItAll.AgentFramework.Tooling",
    "CanDoItAll.AgentFramework.Tools",
    "CanDoItAll.AgentFramework.Skills",
    "CanDoItAll.AgentFramework.Mcp",
    "CanDoItAll.AgentFramework.Memory",
    "CanDoItAll.Processes",
    "CanDoItAll.Modules.AgentFramework",
    "CanDoItAll.Modules.Processes",
    "CanDoItAll.Modules.Workbench",
    "CanDoItAll.Modules.Projects",
    "CanDoItAll.Modules.CrmHr",
]

FORBIDDEN_UI_TOKENS = [
    "FloatingAgentChat",
    "AgentChatPanel",
    "ChatWorkspacePanel",
    "ContextualAgentWorkspaceWindows",
]

BACKEND_UI_SUFFIXES = {".razor", ".cshtml"}

PREPARED_BASELINE = "c0117109c6ef6166d1d8b1b42d75e7f4af83c5ee"

DUPLICATE_CAPABILITY_DECLARATION = re.compile(
    r"\b(?:enum|class|record(?:\s+(?:class|struct))?)\s+"
    r"(?:AgentReasoningEffortLevel|ProviderModelThinkingEffortCapability|AgentThinkingEffortPolicy)\b"
)

DUPLICATE_PROVIDER_CATALOG = re.compile(
    r"\b(?:class|record(?:\s+class)?)\s+\w*LlmChat\w*Provider\w*Catalog\b"
)

FORBIDDEN_WEB_TOKENS = [
    re.compile(r"\bCanDoItAll\.Infrastructure\.Persistence\b"),
    re.compile(r"\bCanDoItAll\.Modules\.LlmChats\.Persistence\b"),
    re.compile(r"\bILlmConversationStore\b"),
    re.compile(r"\bDbContext\b"),
    re.compile(r"\bProviderProfile\b"),
    re.compile(r"\b(?:ApiKey|Secret)\b"),
    re.compile(r"\bAgentExecution\w*\b"),
]


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8", errors="replace")


def production_files(root: Path):
    skipped_directories = {".artifacts", ".git", "artifacts", "bin", "node_modules", "obj"}
    for directory, directory_names, file_names in os.walk(root, topdown=True):
        directory_names[:] = [
            name for name in directory_names
            if name not in skipped_directories
        ]
        for file_name in file_names:
            yield Path(directory) / file_name


def changed_paths_since_baseline(repo: Path) -> set[Path]:
    tracked = subprocess.run(
        ["git", "diff", "--name-only", PREPARED_BASELINE, "--"],
        cwd=repo,
        check=False,
        capture_output=True,
        text=True,
    )
    untracked = subprocess.run(
        ["git", "ls-files", "--others", "--exclude-standard"],
        cwd=repo,
        check=False,
        capture_output=True,
        text=True,
    )
    if tracked.returncode != 0 or untracked.returncode != 0:
        raise RuntimeError("The LLM Chat UI-diff guard could not inspect the prepared baseline.")
    return {
        Path(value.strip())
        for value in [*tracked.stdout.splitlines(), *untracked.stdout.splitlines()]
        if value.strip()
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo-root", type=Path, required=True)
    args = parser.parse_args()
    repo = args.repo_root.resolve()

    errors: list[str] = []
    domain = repo / "src" / "Modules" / "CanDoItAll.Modules.LlmChats"
    persistence = repo / "src" / "Modules" / "CanDoItAll.Modules.LlmChats.Persistence"

    for project, forbidden, label in [
        (domain, FORBIDDEN_DOMAIN_REFERENCES, "domain"),
        (persistence, FORBIDDEN_PERSISTENCE_REFERENCES, "persistence"),
    ]:
        if not project.exists():
            continue
        project_file = project / f"{project.name}.csproj"
        if project_file.is_file() and "Microsoft.NET.Sdk.Razor" in read_text(project_file):
            errors.append(f"{label.capitalize()} project must not use the Razor SDK: {project_file.relative_to(repo)}")
        for path in production_files(project):
            relative = path.relative_to(repo)
            if path.suffix.lower() in BACKEND_UI_SUFFIXES or path.name.endswith(".razor.cs"):
                errors.append(f"UI file is forbidden in {label} project: {relative}")
            text = read_text(path)
            if path.suffix.lower() in {".cs", ".csproj", ".props", ".targets"}:
                for token in forbidden:
                    if token in text:
                        errors.append(f"Forbidden {label} dependency token '{token}' in {relative}")
            if path.suffix.lower() == ".cs" and re.search(r"\bpartial\s+class\b", text):
                errors.append(f"New backend project must not grow production partial classes: {relative}")
            if path.suffix.lower() == ".cs" and DUPLICATE_CAPABILITY_DECLARATION.search(text):
                errors.append(f"Duplicate thinking-effort capability declaration in {relative}")
            if path.suffix.lower() == ".cs" and DUPLICATE_PROVIDER_CATALOG.search(text):
                errors.append(f"Duplicate LLM Chat provider catalog declaration in {relative}")

    if domain.exists():
        for path in production_files(domain):
            if path.suffix.lower() != ".cs":
                continue
            relative = path.relative_to(repo)
            if any(part in {"Application", "Definitions", "Conversations", "Operations", "Ports"} for part in path.parts):
                text = read_text(path)
                if re.search(r"\bIServiceProvider\b|\bGetRequiredService\s*\(", text):
                    errors.append(f"Service location is forbidden in domain/application behavior: {relative}")

    module_registration = (
        repo / "src" / "Modules" / "CanDoItAll.Modules.AgentFramework" / "Services"
        / "AgentFrameworkModuleServiceCollectionExtensions.cs"
    )
    if module_registration.is_file() and "AddLlmConversations(" in read_text(module_registration):
        errors.append("Generic AddLlmConversations production activation returned in AgentFramework module.")

    generic_project = (
        repo / "src" / "MAF" / "Common" / "CanDoItAll.AgentFramework.Llm.Conversations"
    )
    source_files = list(production_files(repo / "src"))
    for path in (item for item in source_files if item.suffix.lower() == ".cs"):
        if generic_project in path.parents:
            continue
        if "AddLlmConversations(" in read_text(path):
            errors.append(f"Generic conversation activation is forbidden in production source: {path.relative_to(repo)}")

    agents_api = repo / "src" / "App" / "CanDoItAll.Web" / "Api" / "AgentsApi.cs"
    if agents_api.is_file() and re.search(r"\bLlmChat", read_text(agents_api)):
        errors.append("Ordinary LLM Chat routes/contracts must not be added to AgentsApi.cs.")

    web_api = repo / "src" / "App" / "CanDoItAll.Web" / "Api"
    for path in web_api.glob("LlmChat*.cs"):
        text = read_text(path)
        for pattern in FORBIDDEN_WEB_TOKENS:
            if pattern.search(text):
                errors.append(
                    f"Forbidden Web LLM Chat boundary token '{pattern.pattern}' in {path.relative_to(repo)}"
                )

    for path in (item for item in source_files if item.suffix.lower() == ".razor"):
        if re.search(r"\bLlmChat", read_text(path)):
            errors.append(f"Simple-chat UI integration is outside this bundle: {path.relative_to(repo)}")

    for path in (item for item in source_files if item.suffix.lower() == ".cs"):
        if any(token in path.name for token in FORBIDDEN_UI_TOKENS):
            if re.search(r"\bLlmChat", read_text(path)):
                errors.append(f"Agent UI/coordinator file contains simple-chat integration: {path.relative_to(repo)}")

    try:
        changed_paths = changed_paths_since_baseline(repo)
    except RuntimeError as exception:
        errors.append(str(exception))
        changed_paths = set()
    for path in changed_paths:
        normalized = path.as_posix()
        if normalized.endswith((".razor", ".razor.cs", ".razor.css", ".cshtml")):
            errors.append(f"UI file changed after the prepared bundle baseline: {normalized}")
        if any(token in path.name for token in FORBIDDEN_UI_TOKENS):
            errors.append(f"Floating-agent-chat file changed after the prepared bundle baseline: {normalized}")

    if errors:
        print("\n".join(dict.fromkeys(errors)))
        return 1

    print("Implemented LLM Chat architecture boundary checks passed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
