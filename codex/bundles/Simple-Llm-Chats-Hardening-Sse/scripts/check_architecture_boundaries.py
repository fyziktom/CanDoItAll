#!/usr/bin/env python3
"""Check implemented LLM Chat architecture, execution, API, and UI boundaries."""

from __future__ import annotations

import argparse
import os
from pathlib import Path
import re
import subprocess
import sys


PREPARED_BASELINE = "16b6aa4b60dc88a6134dd6c9c9e634c064ac5847"

FORBIDDEN_PRODUCT_REFERENCES = (
    "CanDoItAll.Web",
    "CanDoItAll.AppComponents",
    "CanDoItAll.AgentFramework.Core",
    "CanDoItAll.AgentFramework.Components",
    "CanDoItAll.AgentFramework.Maf",
    "CanDoItAll.AgentFramework.Tooling",
    "CanDoItAll.AgentFramework.Tools",
    "CanDoItAll.AgentFramework.Skills",
    "CanDoItAll.AgentFramework.Mcp",
    "CanDoItAll.AgentFramework.Memory",
    "CanDoItAll.Processes",
    "CanDoItAll.Modules.AgentFramework",
    "CanDoItAll.Modules.Workbench",
    "CanDoItAll.Modules.Projects",
    "CanDoItAll.Modules.Processes",
    "CanDoItAll.Modules.CrmHr",
    "CanDoItAll.Modules.Workspace",
    "Microsoft.EntityFrameworkCore",
    "Microsoft.AspNetCore",
)

FORBIDDEN_PERSISTENCE_REFERENCES = (
    "CanDoItAll.Web",
    "CanDoItAll.AppComponents",
    "CanDoItAll.AgentFramework.Core",
    "CanDoItAll.AgentFramework.Components",
    "CanDoItAll.AgentFramework.Maf",
    "CanDoItAll.AgentFramework.Tooling",
    "CanDoItAll.AgentFramework.Tools",
    "CanDoItAll.AgentFramework.Skills",
    "CanDoItAll.AgentFramework.Mcp",
    "CanDoItAll.AgentFramework.Memory",
    "CanDoItAll.Processes",
    "CanDoItAll.Modules.AgentFramework",
    "CanDoItAll.Modules.Workbench",
    "CanDoItAll.Modules.Projects",
    "CanDoItAll.Modules.Processes",
    "CanDoItAll.Modules.CrmHr",
    "CanDoItAll.Modules.Workspace",
    "Microsoft.AspNetCore",
)

FORBIDDEN_UI_TOKENS = (
    "FloatingAgentChat",
    "AgentChatPanel",
    "ChatWorkspacePanel",
    "ContextualAgentWorkspaceWindows",
)

BACKEND_UI_SUFFIXES = (".razor", ".razor.cs", ".razor.css", ".cshtml")

DORMANT_DEPLOYMENT_FIELD = re.compile(
    r"\b(?:LlmChatDeployment|ExternalParticipant|ParticipantId|ChannelId|ChannelKind|"
    r"Moderation|Quota|DataResidency|LegalHold|HumanHandoff|TenantId|"
    r"AnonymousAccess|RetentionPolicy)\b",
    re.IGNORECASE,
)

DUPLICATE_CAPABILITY_DECLARATION = re.compile(
    r"\b(?:enum|class|record(?:\s+(?:class|struct))?)\s+"
    r"(?:AgentReasoningEffortLevel|ProviderModelThinkingEffortCapability|"
    r"AgentThinkingEffortPolicy)\b"
)

DUPLICATE_PROVIDER_CATALOG = re.compile(
    r"\b(?:class|record(?:\s+class)?)\s+\w*LlmChat\w*Provider\w*Catalog\b"
)

FORBIDDEN_WEB_PATTERNS = (
    re.compile(r"\bCanDoItAll\.Infrastructure\.Persistence\b"),
    re.compile(r"\bCanDoItAll\.Modules\.LlmChats\.Persistence\b"),
    re.compile(r"\bILlmConversationStore\b"),
    re.compile(r"\bDbContext\b"),
    re.compile(r"\bProviderProfile\b"),
    re.compile(r"\b(?:ApiKey|Secret)\b"),
    re.compile(r"\bAgentExecution\w*\b"),
)


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8", errors="replace")


def production_files(root: Path):
    skipped_directories = {
        ".artifacts",
        ".git",
        "artifacts",
        "bin",
        "node_modules",
        "obj",
    }
    for directory, directory_names, file_names in os.walk(root, topdown=True):
        directory_names[:] = [
            name for name in directory_names if name not in skipped_directories
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
        raise RuntimeError(
            "The LLM Chat UI-diff guard could not inspect the prepared baseline."
        )
    return {
        Path(value.strip())
        for value in [*tracked.stdout.splitlines(), *untracked.stdout.splitlines()]
        if value.strip()
    }


def require_marker(
    errors: list[str],
    text: str,
    marker: str,
    description: str,
) -> None:
    if marker not in text:
        errors.append(f"Missing {description}: marker {marker}")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo-root", type=Path, required=True)
    args = parser.parse_args()
    repo = args.repo_root.resolve()

    errors: list[str] = []
    source_root = repo / "src"
    product = source_root / "Modules" / "CanDoItAll.Modules.LlmChats"
    persistence = (
        source_root / "Modules" / "CanDoItAll.Modules.LlmChats.Persistence"
    )
    web_api = source_root / "App" / "CanDoItAll.Web" / "Api"
    composition = source_root / "App" / "CanDoItAll.Composition"

    for project, forbidden, label in (
        (product, FORBIDDEN_PRODUCT_REFERENCES, "product"),
        (persistence, FORBIDDEN_PERSISTENCE_REFERENCES, "persistence"),
    ):
        if not project.is_dir():
            errors.append(f"Missing LLM Chat {label} project: {project}")
            continue
        project_file = project / f"{project.name}.csproj"
        if project_file.is_file() and "Microsoft.NET.Sdk.Razor" in read_text(project_file):
            errors.append(
                f"{label.capitalize()} project must not use the Razor SDK: "
                f"{project_file.relative_to(repo)}"
            )
        for path in production_files(project):
            relative = path.relative_to(repo)
            normalized = path.name.lower()
            if normalized.endswith(BACKEND_UI_SUFFIXES):
                errors.append(f"UI file is forbidden in {label} project: {relative}")
            text = read_text(path)
            if path.suffix.lower() in {".cs", ".csproj", ".props", ".targets"}:
                for token in forbidden:
                    if token in text:
                        errors.append(
                            f"Forbidden {label} dependency token '{token}' in {relative}"
                        )
            if path.suffix.lower() != ".cs":
                continue
            if re.search(r"\bpartial\s+class\b", text):
                errors.append(
                    f"LLM Chat production code must not grow partial classes: {relative}"
                )
            if DUPLICATE_CAPABILITY_DECLARATION.search(text):
                errors.append(f"Duplicate thinking-effort declaration in {relative}")
            if DUPLICATE_PROVIDER_CATALOG.search(text):
                errors.append(f"Duplicate LLM Chat provider catalog in {relative}")
            if DORMANT_DEPLOYMENT_FIELD.search(text):
                errors.append(f"Dormant deployment field or type in {relative}")

    for path in production_files(product):
        if path.suffix.lower() != ".cs":
            continue
        relative = path.relative_to(repo)
        if any(
            part in {"Application", "Definitions", "Conversations", "Operations", "Ports"}
            for part in path.parts
        ):
            text = read_text(path)
            if re.search(r"\bIServiceProvider\b|\bGetRequiredService\s*\(", text):
                errors.append(
                    f"Service location is forbidden in product behavior: {relative}"
                )

    generic_project = (
        source_root
        / "MAF"
        / "Common"
        / "CanDoItAll.AgentFramework.Llm.Conversations"
    )
    source_files = list(production_files(source_root))
    for path in (item for item in source_files if item.suffix.lower() == ".cs"):
        if generic_project in path.parents:
            continue
        if "AddLlmConversations(" in read_text(path):
            errors.append(
                "Generic conversation activation is forbidden in production source: "
                f"{path.relative_to(repo)}"
            )

    agents_api = web_api / "AgentsApi.cs"
    if agents_api.is_file() and re.search(r"\bLlmChat", read_text(agents_api)):
        errors.append("Ordinary LLM Chat routes/contracts must not be added to AgentsApi.cs.")

    llm_web_files = list(web_api.glob("LlmChat*.cs"))
    for path in llm_web_files:
        text = read_text(path)
        for pattern in FORBIDDEN_WEB_PATTERNS:
            if pattern.search(text):
                errors.append(
                    f"Forbidden Web LLM Chat boundary token '{pattern.pattern}' in "
                    f"{path.relative_to(repo)}"
                )

    api_contracts = read_text(web_api / "LlmChatApiContracts.cs")
    if re.search(
        r"CreateLlmChatConversationApiRequest\s*\([^)]*\bOrigin\b",
        api_contracts,
        re.DOTALL,
    ):
        errors.append("Conversation origin must not be accepted from an HTTP request.")
    llm_chats_api = read_text(web_api / "LlmChatsApi.cs")
    require_marker(
        errors,
        llm_chats_api,
        "LlmChatConversationOrigin.Api",
        "server-owned API conversation origin",
    )

    operation_service = read_text(
        product / "Application" / "LlmChatOperationApplicationService.cs"
    )
    require_marker(
        errors,
        operation_service,
        "dispatchSignal.Signal()",
        "durable-operation dispatcher signal",
    )
    if re.search(r"conversationEngine\.(?:Send|Execute)Async\s*\(", operation_service):
        errors.append("Request-owned provider execution returned to the operation service.")

    operation_api_path = web_api / "LlmChatOperationsApi.cs"
    operation_api = read_text(operation_api_path)
    require_marker(
        errors,
        operation_api,
        "Results.Accepted(location, LlmChatOperationApiMapper.ToResponse(details))",
        "202 Accepted turn admission",
    )
    if "ILlmChatConversationEngine" in operation_api or "LlmChatOperationExecutor" in operation_api:
        errors.append("The HTTP operation endpoint directly owns provider execution.")

    hosted_dispatcher = read_text(
        composition / "LlmChatOperationDispatcherHostedService.cs"
    )
    host_registration = read_text(composition / "RuntimeHostServiceCollectionExtensions.cs")
    require_marker(
        errors,
        hosted_dispatcher,
        "dispatcher.DispatchNextAsync",
        "background durable-operation dispatcher",
    )
    require_marker(
        errors,
        host_registration,
        "AddHostedService<LlmChatOperationDispatcherHostedService>()",
        "dispatcher host registration",
    )

    conversation_store = read_text(
        persistence / "Conversations" / "EfLlmConversationStore.cs"
    )
    require_marker(
        errors,
        conversation_store,
        "AppDbContext dbContext",
        "transaction-sharing conversation store",
    )
    if "IDbContextFactory" in conversation_store or "CreateDbContextAsync" in conversation_store:
        errors.append(
            "The canonical conversation store returned to an independent DbContext path."
        )

    allowed_post_commit_paths = {
        product / "Ports" / "LlmChatRepositories.cs",
        product / "Application" / "LlmChatOperationEventJournal.cs",
        persistence / "Repositories" / "EfLlmChatUnitOfWork.cs",
    }
    for path in (
        item
        for item in [*production_files(product), *production_files(persistence)]
        if item.suffix.lower() == ".cs"
    ):
        if "RegisterPostCommit" in read_text(path) and path not in allowed_post_commit_paths:
            errors.append(
                "Post-commit behavior is permitted only for committed event notification: "
                f"{path.relative_to(repo)}"
            )
    event_journal = read_text(
        product / "Application" / "LlmChatOperationEventJournal.cs"
    )
    require_marker(
        errors,
        event_journal,
        "unitOfWork.RegisterPostCommit(() => signal.Publish",
        "post-commit event notification",
    )

    sse_writer_paths = [
        path
        for path in source_files
        if path.suffix.lower() == ".cs"
        and re.search(
            r"\b(?:static\s+)?class\s+ServerSentEventResponseWriter\b",
            read_text(path),
        )
    ]
    if len(sse_writer_paths) != 1:
        errors.append(
            "Expected exactly one production ServerSentEventResponseWriter; found "
            f"{len(sse_writer_paths)}."
        )
    require_marker(
        errors,
        operation_api,
        "ServerSentEventResponseWriter.WriteAsync",
        "shared SSE response writer reuse",
    )
    if "Response.WriteAsync" in operation_api or "text/event-stream" in operation_api:
        errors.append("The LLM Chat endpoint contains a duplicate SSE writer path.")

    for path in (
        item for item in source_files if item.name.lower().endswith(BACKEND_UI_SUFFIXES)
    ):
        if re.search(r"\bLlmChat\b|Simple\s+Chat", read_text(path), re.IGNORECASE):
            errors.append(
                f"LLM Chat UI integration is outside this bundle: {path.relative_to(repo)}"
            )

    try:
        changed_paths = changed_paths_since_baseline(repo)
    except RuntimeError as exception:
        errors.append(str(exception))
        changed_paths = set()
    for path in changed_paths:
        normalized = path.as_posix()
        lowered = normalized.lower()
        if lowered.endswith(BACKEND_UI_SUFFIXES):
            errors.append(f"UI file changed after the reviewed feature baseline: {normalized}")
        if any(token.lower() in lowered for token in FORBIDDEN_UI_TOKENS):
            errors.append(
                "Floating-agent-chat file changed after the reviewed feature baseline: "
                f"{normalized}"
            )

    if errors:
        print("\n".join(f"ERROR: {error}" for error in dict.fromkeys(errors)))
        return 1

    print("Implemented LLM Chat architecture boundary checks passed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
