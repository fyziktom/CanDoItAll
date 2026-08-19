#!/usr/bin/env python3
"""Validate the completed LLM Chat backend's C# architecture boundaries."""

from __future__ import annotations

import argparse
import os
from pathlib import Path
import re
import subprocess
import sys
import xml.etree.ElementTree as ET


EXECUTION_BASELINE = "c3c7713927b9519200900583f227ead95fafb5e9"

BACKEND_UI_SUFFIXES = (".razor", ".razor.cs", ".razor.css", ".cshtml")
FORBIDDEN_UI_TOKENS = (
    "FloatingAgentChat",
    "AgentChatPanel",
    "ChatWorkspacePanel",
    "ContextualAgentWorkspaceWindows",
)

FORBIDDEN_CORE_REFERENCES = (
    "CanDoItAll.Modules.LlmChats.Persistence",
    "CanDoItAll.Web",
    "CanDoItAll.AppComponents",
    "CanDoItAll.Composition",
    "CanDoItAll.AgentFramework.Core",
    "CanDoItAll.AgentFramework.Components",
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
    "Npgsql",
)

FORBIDDEN_PERSISTENCE_REFERENCES = (
    "CanDoItAll.Web",
    "CanDoItAll.AppComponents",
    "CanDoItAll.AgentFramework.Core",
    "CanDoItAll.AgentFramework.Components",
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

FORBIDDEN_WEB_PATTERNS = (
    re.compile(r"\bCanDoItAll\.Modules\.LlmChats\.Persistence\b"),
    re.compile(r"\bILlmConversationStore\b"),
    re.compile(r"\bAppDbContext\b"),
    re.compile(r"\bEfLlmChat\w+\b"),
    re.compile(r"\bILlm(?:Streaming)?InvocationPort\b"),
    re.compile(r"\bILlmChatConversationEngine\b"),
    re.compile(r"\bLlmChatOperationExecutor\b"),
    re.compile(r"\bProviderBackedLlmStreamingInvocationAdapter\b"),
)

FORBIDDEN_OPERATION_DTO_FIELDS = re.compile(
    r"\b(?:RequestFingerprint|RawProvider|ProviderBody|ProviderEndpoint|"
    r"Credential|ApiKey|Secret|SystemInstruction|SystemPrompt|ExceptionMessage)\b",
    re.IGNORECASE,
)
PUBLIC_RAW_INNER_EXCEPTION = re.compile(
    r"new\s+LlmInvocationException\s*\([^;]*\bexception\b[^;]*\)",
    re.DOTALL,
)

PARTIAL_DECLARATION = re.compile(r"\bpartial\s+(?:class|record|struct)\b")
SHADOW_DISPATCH = re.compile(r"\b(?:Channel|ConcurrentQueue)\s*<|\bTask\.Run\s*\(")
SERVICE_LOCATION = re.compile(r"\bIServiceProvider\b|\bGetRequiredService\s*\(")


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8", errors="replace")


def production_files(root: Path):
    skipped = {".artifacts", ".git", "artifacts", "bin", "node_modules", "obj"}
    for directory, directory_names, file_names in os.walk(root, topdown=True):
        directory_names[:] = [name for name in directory_names if name not in skipped]
        for file_name in file_names:
            yield Path(directory) / file_name


def project_reference_names(project_file: Path) -> set[str]:
    root = ET.parse(project_file).getroot()
    names: set[str] = set()
    for element in root.iter():
        if element.tag.rsplit("}", 1)[-1] != "ProjectReference":
            continue
        include = element.attrib.get("Include")
        if include:
            names.add(Path(include.replace("\\", "/")).stem)
    return names


def forbidden_tokens(text: str, tokens: tuple[str, ...]) -> list[str]:
    return [token for token in tokens if token in text]


def contains_partial_declaration(text: str) -> bool:
    return PARTIAL_DECLARATION.search(text) is not None


def changed_paths_since_baseline(repo: Path) -> set[Path]:
    tracked = subprocess.run(
        ["git", "diff", "--name-only", EXECUTION_BASELINE, "--"],
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
        raise RuntimeError("Could not inspect changes from the bundle execution baseline.")
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


def validate(repo: Path) -> list[str]:
    errors: list[str] = []
    source = repo / "src"
    core = source / "Modules" / "CanDoItAll.Modules.LlmChats"
    persistence = source / "Modules" / "CanDoItAll.Modules.LlmChats.Persistence"
    web = source / "App" / "CanDoItAll.Web"
    web_api = web / "Api"
    composition = source / "App" / "CanDoItAll.Composition"
    provider_runtime = (
        source
        / "MAF"
        / "Common"
        / "CanDoItAll.AgentFramework.Llm.ProviderRuntime"
    )

    required_directories = (core, persistence, web_api, composition, provider_runtime)
    for directory in required_directories:
        if not directory.is_dir():
            errors.append(f"Missing architecture scope directory: {directory.relative_to(repo)}")
    if errors:
        return errors

    core_project = core / "CanDoItAll.Modules.LlmChats.csproj"
    persistence_project = persistence / "CanDoItAll.Modules.LlmChats.Persistence.csproj"
    web_project = web / "CanDoItAll.Web.csproj"
    composition_project = composition / "CanDoItAll.Composition.csproj"
    provider_project = (
        provider_runtime / "CanDoItAll.AgentFramework.Llm.ProviderRuntime.csproj"
    )

    expected_core = {
        "CanDoItAll.SharedKernel",
        "CanDoItAll.AgentFramework.Llm.Abstractions",
        "CanDoItAll.AgentFramework.Models",
    }
    expected_persistence = {
        "CanDoItAll.Modules.LlmChats",
        "CanDoItAll.Infrastructure",
        "CanDoItAll.AgentFramework.Llm.Abstractions",
        "CanDoItAll.AgentFramework.Llm.Conversations",
        "CanDoItAll.AgentFramework.Llm.ProviderRuntime",
        "CanDoItAll.AgentFramework.Providers",
    }
    expected_provider = {
        "CanDoItAll.AgentFramework.Llm.Abstractions",
        "CanDoItAll.AgentFramework.Models",
        "CanDoItAll.AgentFramework.Providers",
    }
    for project, expected, label in (
        (core_project, expected_core, "Core"),
        (persistence_project, expected_persistence, "Persistence"),
        (provider_project, expected_provider, "ProviderRuntime"),
    ):
        actual = project_reference_names(project)
        if actual != expected:
            errors.append(
                f"{label} project references changed. Expected={sorted(expected)} "
                f"Actual={sorted(actual)}"
            )

    web_references = project_reference_names(web_project)
    if "CanDoItAll.Modules.LlmChats.Persistence" in web_references:
        errors.append("Web must not reference the LLM Chat Persistence project.")
    for expected in ("CanDoItAll.Modules.LlmChats", "CanDoItAll.Composition"):
        if expected not in web_references:
            errors.append(f"Web is missing its expected direct reference to {expected}.")
    if "CanDoItAll.Modules.LlmChats.Persistence" not in project_reference_names(
        composition_project
    ):
        errors.append("Composition must own the LLM Chat Persistence reference.")

    for project, forbidden, label in (
        (core, FORBIDDEN_CORE_REFERENCES, "Core"),
        (persistence, FORBIDDEN_PERSISTENCE_REFERENCES, "Persistence"),
    ):
        project_file = project / f"{project.name}.csproj"
        if "Microsoft.NET.Sdk.Razor" in read_text(project_file):
            errors.append(f"{label} must not use the Razor SDK.")
        for path in production_files(project):
            relative = path.relative_to(repo)
            if path.name.lower().endswith(BACKEND_UI_SUFFIXES):
                errors.append(f"UI file is forbidden in {label}: {relative}")
            if path.suffix.lower() not in {".cs", ".csproj", ".props", ".targets"}:
                continue
            for token in forbidden_tokens(read_text(path), forbidden):
                errors.append(f"Forbidden {label} dependency '{token}' in {relative}")

    affected_csharp = [
        *(path for path in production_files(core) if path.suffix.lower() == ".cs"),
        *(path for path in production_files(persistence) if path.suffix.lower() == ".cs"),
        *web_api.glob("LlmChat*.cs"),
        web_api / "Streaming" / "LlmChatOperationEventReplayReader.cs",
        web_api / "Streaming" / "ServerSentEventResponseWriter.cs",
        composition / "LlmChatOperationDispatcherHostedService.cs",
        composition / "RuntimeHostServiceCollectionExtensions.cs",
        provider_runtime / "ProviderBackedLlmStreamingInvocationAdapter.cs",
    ]
    for path in dict.fromkeys(affected_csharp):
        if path.is_file() and contains_partial_declaration(read_text(path)):
            errors.append(
                f"Affected production code must not use partial types: {path.relative_to(repo)}"
            )

    for path in production_files(core):
        if path.suffix.lower() != ".cs":
            continue
        if any(
            part in {"Application", "Definitions", "Conversations", "Operations", "Ports"}
            for part in path.parts
        ) and SERVICE_LOCATION.search(read_text(path)):
            errors.append(
                f"Service location is forbidden in Core behavior: {path.relative_to(repo)}"
            )

    generic_conversations = (
        source / "MAF" / "Common" / "CanDoItAll.AgentFramework.Llm.Conversations"
    )
    source_files = list(production_files(source))
    for path in source_files:
        if path.suffix.lower() != ".cs" or generic_conversations in path.parents:
            continue
        if "AddLlmConversations(" in read_text(path):
            errors.append(
                "Generic file/in-memory conversation activation is forbidden: "
                f"{path.relative_to(repo)}"
            )

    agents_api = web_api / "AgentsApi.cs"
    if re.search(r"\bLlmChat", read_text(agents_api)):
        errors.append("Ordinary LLM Chat routes/contracts must not enter AgentsApi.cs.")

    llm_web_files = list(web_api.glob("LlmChat*.cs"))
    for path in llm_web_files:
        text = read_text(path)
        for pattern in FORBIDDEN_WEB_PATTERNS:
            if pattern.search(text):
                errors.append(
                    f"Forbidden Web LLM Chat token '{pattern.pattern}' in "
                    f"{path.relative_to(repo)}"
                )

    api_contracts = read_text(web_api / "LlmChatApiContracts.cs")
    if re.search(
        r"CreateLlmChatConversationApiRequest\s*\([^)]*\bOrigin\b",
        api_contracts,
        re.DOTALL,
    ):
        errors.append("Conversation origin must not be accepted from HTTP.")
    conversation_endpoints = read_text(web_api / "LlmChatConversationEndpoints.cs")
    require_marker(
        errors,
        conversation_endpoints,
        "LlmChatConversationOrigin.Api",
        "server-owned API conversation origin",
    )

    policies = read_text(web_api / "ApiAuthorizationPolicies.cs")
    for marker, description in (
        ('ReadLlmChats = "Api.LlmChats.Read"', "exact read scope"),
        ('ManageLlmChats = "Api.LlmChats.Manage"', "exact manage scope"),
        ('ExecuteLlmChats = "Api.LlmChats.Execute"', "exact execute scope"),
    ):
        require_marker(errors, policies, marker, description)
    definition_endpoints = read_text(web_api / "LlmChatDefinitionEndpoints.cs")
    operation_api = read_text(web_api / "LlmChatOperationsApi.cs")
    for text, marker, description in (
        (definition_endpoints, "ApiAuthorizationPolicies.ReadLlmChats", "definition read policy"),
        (definition_endpoints, "ApiAuthorizationPolicies.ManageLlmChats", "definition manage policy"),
        (conversation_endpoints, "ApiAuthorizationPolicies.ReadLlmChats", "conversation read policy"),
        (conversation_endpoints, "ApiAuthorizationPolicies.ManageLlmChats", "conversation manage policy"),
        (operation_api, "ApiAuthorizationPolicies.ExecuteLlmChats", "operation execute policy"),
        (operation_api, "ApiAuthorizationPolicies.ManageLlmChats", "operation manage policy"),
    ):
        require_marker(errors, text, marker, description)

    operation_service = read_text(
        core / "Application" / "LlmChatOperationApplicationService.cs"
    )
    require_marker(
        errors,
        operation_service,
        "dispatchSignal.Signal()",
        "durable operation dispatch signal",
    )
    require_marker(
        errors,
        operation_api,
        "Results.Accepted(location, LlmChatOperationApiMapper.ToResponse(details))",
        "202 Accepted turn admission",
    )

    hosted_dispatcher = read_text(
        composition / "LlmChatOperationDispatcherHostedService.cs"
    )
    host_registration = read_text(
        composition / "RuntimeHostServiceCollectionExtensions.cs"
    )
    require_marker(
        errors,
        hosted_dispatcher,
        "dispatcher.DispatchNextAsync",
        "background durable operation dispatcher",
    )
    require_marker(
        errors,
        host_registration,
        "AddHostedService<LlmChatOperationDispatcherHostedService>()",
        "dispatcher hosted-service registration",
    )
    for path in (
        core / "Application" / "LlmChatOperationDispatcher.cs",
        composition / "LlmChatOperationDispatcherHostedService.cs",
    ):
        if SHADOW_DISPATCH.search(read_text(path)):
            errors.append(f"Shadow/unbounded dispatch is forbidden: {path.relative_to(repo)}")

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
        errors.append("The canonical conversation store must share the scoped AppDbContext.")

    transfer_document = read_text(
        persistence / "DatabaseTransfer" / "LlmChatsTransferDocument.cs"
    )
    for marker, description in (
        (
            "BeginTransactionAsync(IsolationLevel.RepeatableRead",
            "repeatable-read transfer source snapshot",
        ),
        ("ProvidesRepeatableReads", "ambient transfer isolation validation"),
        (".Take(maximumLoadedRecords + 1)", "bounded transfer materialization"),
        ("remainingTotalRecords", "aggregate transfer load budget"),
    ):
        require_marker(errors, transfer_document, marker, description)

    allowed_post_commit = {
        core / "Ports" / "LlmChatRepositories.cs",
        core / "Application" / "LlmChatOperationEventJournal.cs",
        persistence / "Repositories" / "EfLlmChatUnitOfWork.cs",
    }
    for path in (
        item
        for item in [*production_files(core), *production_files(persistence)]
        if item.suffix.lower() == ".cs"
    ):
        if "RegisterPostCommit" in read_text(path) and path not in allowed_post_commit:
            errors.append(
                "Post-commit behavior is limited to event notification: "
                f"{path.relative_to(repo)}"
            )
    require_marker(
        errors,
        read_text(core / "Application" / "LlmChatOperationEventJournal.cs"),
        "unitOfWork.RegisterPostCommit(() => signal.Publish",
        "post-commit event notification",
    )

    sse_writers = [
        path
        for path in source_files
        if path.suffix.lower() == ".cs"
        and re.search(
            r"\b(?:static\s+)?class\s+ServerSentEventResponseWriter\b",
            read_text(path),
        )
    ]
    if len(sse_writers) != 1:
        errors.append(
            f"Expected one production ServerSentEventResponseWriter; found {len(sse_writers)}."
        )
    require_marker(
        errors,
        operation_api,
        "ServerSentEventResponseWriter.WriteAsync",
        "shared SSE writer reuse",
    )
    if "Response.WriteAsync" in operation_api or '"text/event-stream"' in operation_api:
        errors.append("The operation endpoint contains an inline SSE writer.")

    operation_contracts = "\n".join(
        read_text(web_api / name)
        for name in (
            "LlmChatOperationApiContracts.cs",
            "LlmChatOperationEventApiContracts.cs",
        )
    )
    forbidden_field = FORBIDDEN_OPERATION_DTO_FIELDS.search(operation_contracts)
    if forbidden_field:
        errors.append(
            f"Forbidden public operation/audit DTO field: {forbidden_field.group(0)}"
        )

    adapter = read_text(
        provider_runtime / "ProviderBackedLlmStreamingInvocationAdapter.cs"
    )
    if "CanDoItAll.Modules.LlmChats" in adapter:
        errors.append("ProviderRuntime must not depend on Modules.LlmChats.")
    for pattern, description in (
        (r"\bexception\.(?:Message|InnerException)\b", "raw exception text"),
        (r"Log(?:Warning|Error)\s*\(\s*exception\b", "logged exception object"),
    ):
        if re.search(pattern, adapter, re.DOTALL):
            errors.append(f"Provider adapter exposes {description}.")
    if PUBLIC_RAW_INNER_EXCEPTION.search(adapter):
        errors.append("Provider adapter exposes public raw inner exception.")

    postgres_tests = read_text(
        repo
        / "tests"
        / "Integration"
        / "CanDoItAll.Tests.Integration"
        / "LlmChatsApiPostgreSqlIntegrationTests.cs"
    )
    if "StubLlmChatOperationApplicationService" in postgres_tests:
        errors.append("The real PostgreSQL LLM Chat proof must not stub the application service.")

    try:
        changed_paths = changed_paths_since_baseline(repo)
    except RuntimeError as exception:
        errors.append(str(exception))
        changed_paths = set()
    for path in changed_paths:
        normalized = path.as_posix()
        lowered = normalized.lower()
        if lowered.endswith(BACKEND_UI_SUFFIXES):
            errors.append(f"UI changed during the backend-only bundle: {normalized}")
        if any(token.lower() in lowered for token in FORBIDDEN_UI_TOKENS):
            errors.append(f"Chat UI integration changed during the backend bundle: {normalized}")
        if lowered.endswith((".csproj", ".sln", ".slnx")):
            errors.append(f"Project/solution graph changed during bundle execution: {normalized}")

    return list(dict.fromkeys(errors))


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo-root", type=Path, required=True)
    args = parser.parse_args()
    errors = validate(args.repo_root.resolve())
    if errors:
        print("\n".join(f"ERROR: {error}" for error in errors))
        return 1
    print("Completed LLM Chat backend architecture boundary checks passed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
