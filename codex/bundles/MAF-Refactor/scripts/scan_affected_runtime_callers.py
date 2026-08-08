#!/usr/bin/env python3
"""Create an evidence report of runtime, context, process, and lightweight LLM adaptation points."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

TOKEN_GROUPS: dict[str, tuple[str, ...]] = {
    "broad_runtime_contracts": (
        "IAgentRuntime",
        "MafAgentRuntime",
        ".RunAsync(",
        "RespondToPendingApprovalsAsync",
        "CreateHostedAgentAsync",
    ),
    "runtime_construction_and_registration": (
        "new MafAgentRuntime(",
        "AddMafRuntimeArchitectureServices",
        "AddMafProviderRuntimeServices",
        "RuntimeCapabilityComposer",
        "MafRuntimeDependencyResolver",
        "BuildServiceProvider(",
    ),
    "service_location": (
        "IServiceProvider",
        "GetRequiredService<",
        "GetService<",
        "GetServices<",
    ),
    "floating_context_and_navigation": (
        "AgentChatContextRegistry",
        "AgentChatContextInvocationFactory",
        "AgentChatNavigationIdentity",
        "AgentChatWorkspacePosition",
        "AgentChatSurfacePosition",
        "FloatingAgentChatCoordinator",
    ),
    "turn_context_and_continuation": (
        "AgentRuntimeTransientContext",
        "AgentRunTransientContextRegistry",
        "AgentTurnContextLeaseRegistry",
        "TransientContextDigest",
        "RuntimeSessionKey",
        "SerializedSessionStateJson",
        "PendingToolApproval",
    ),
    "workspace_scope_and_audit": (
        "WorkspaceExecutionAuditContext",
        "ContextWorkspaceScope",
        "WorkspaceScopeDescriptor",
        "IWorkspaceFileService",
        "IWorkspaceCommandExecutionService",
    ),
    "process_semantics": (
        "ProcessStepOutcomeResult",
        "ProcessStepOutcomeStatus",
        '"process-step"',
        "ProcessArtifactRecovery",
        "ProcessRunId",
        "ProcessStepId",
    ),
    "workflow_llm": (
        "MafWorkflowLlmComponentInvoker",
        "LlmCallComponent",
        "TryResolveProjectId(",
        "TryResolveProjectScope(",
    ),
    "provider_lightweight_foundation": (
        "ILlmInvocationPort",
        "IStreamingLlmInvocationPort",
        "IProviderChatCompletionDriver",
        "ProviderChatCompletionRequest",
        "IProviderRuntimePool",
        "IMafProviderRuntimeGateway",
    ),
    "mocks_harnesses_and_diagnostics": (
        "ProcessMockAgentRuntime",
        "ScenarioHarnessAgentRuntime",
        "OpenAiContextProbe",
        "ProviderTestChat",
    ),
}

ALLOWED_SUFFIXES = {".cs", ".razor", ".csproj", ".props", ".targets"}
DEFAULT_SCAN_ROOTS = ("src", "tools", "tests")
IGNORED_DIRECTORIES = {
    ".artifacts",
    "artifacts",
    ".git",
    "node_modules",
    "bin",
    "obj",
    "output",
}


def iter_scan_files(scan_root: Path):
    import os

    for dirpath, dirnames, filenames in os.walk(scan_root):
        dirnames[:] = [d for d in dirnames if d not in IGNORED_DIRECTORIES]
        for name in filenames:
            yield Path(dirpath) / name


def classify_area(path: Path, repository_root: Path) -> str:
    relative = path.relative_to(repository_root)
    return relative.parts[0] if relative.parts else "root"


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo-root", type=Path, required=True)
    parser.add_argument("--json-out", type=Path, required=True)
    args = parser.parse_args()

    root = args.repo_root.resolve()
    report: dict[str, list[dict[str, object]]] = {
        key: [] for key in TOKEN_GROUPS
    }
    scanned_files = 0
    missing_roots: list[str] = []

    for root_name in DEFAULT_SCAN_ROOTS:
        scan_root = root / root_name
        if not scan_root.is_dir():
            missing_roots.append(root_name)
            continue

        for path in iter_scan_files(scan_root):
            if not path.is_file() or path.suffix.lower() not in ALLOWED_SUFFIXES:
                continue
            scanned_files += 1
            text = path.read_text(encoding="utf-8", errors="replace")
            relative = path.relative_to(root).as_posix()
            for category, tokens in TOKEN_GROUPS.items():
                matches = sorted(token for token in tokens if token in text)
                if not matches:
                    continue
                report[category].append(
                    {
                        "path": relative,
                        "area": classify_area(path, root),
                        "tokens": matches,
                    }
                )

    payload = {
        "repositoryRoot": str(root),
        "scanRoots": list(DEFAULT_SCAN_ROOTS),
        "missingScanRoots": missing_roots,
        "scannedFileCount": scanned_files,
        "categories": report,
    }
    args.json_out.parent.mkdir(parents=True, exist_ok=True)
    args.json_out.write_text(
        json.dumps(payload, indent=2) + "\n",
        encoding="utf-8",
    )

    print(f"Scanned {scanned_files} file(s).")
    if missing_roots:
        print("Missing optional scan roots: " + ", ".join(missing_roots))
    for category, entries in report.items():
        production = sum(1 for entry in entries if entry["area"] in {"src", "tools"})
        tests = sum(1 for entry in entries if entry["area"] == "tests")
        print(f"{category}: {len(entries)} file(s) ({production} production/tool, {tests} tests)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
