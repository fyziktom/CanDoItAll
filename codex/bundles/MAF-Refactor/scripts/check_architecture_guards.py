#!/usr/bin/env python3
"""Check final target-repository architecture guards for this refactor."""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo-root", type=Path, required=True)
    return parser.parse_args()


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8", errors="replace")


def main() -> int:
    root = parse_args().repo_root.resolve()
    errors: list[str] = []

    maf_root = root / "src/MAF/Common/CanDoItAll.AgentFramework.Maf"
    maf_project = maf_root / "CanDoItAll.AgentFramework.Maf.csproj"
    if not maf_project.is_file():
        errors.append(f"Missing MAF project: {maf_project}")
        return report(errors)

    project_text = read(maf_project).replace("/", "\\")
    for match in re.finditer(r'<ProjectReference\s+Include="([^"]+)"', project_text):
        include = match.group(1)
        if "\\Modules\\" in include:
            errors.append(f"MAF project references a product module: {include}")
        if "CanDoItAll.AgentFramework.Workflows.MafAdapter" in include:
            errors.append(f"MAF project still references Workflows.MafAdapter: {include}")

    forbidden_maf_tokens = [
        "using CanDoItAll.Modules.",
        "ProcessStepOutcomeResult",
        "ProcessStepOutcomeStatus",
        "ProcessArtifactRecovery",
        "artifacts/process-runs",
        '"process-step"',
    ]
    for path in maf_root.rglob("*.cs"):
        text = read(path)
        for token in forbidden_maf_tokens:
            if token in text:
                errors.append(f"{path.relative_to(root)} contains forbidden MAF token: {token}")

    runtime_scan_roots = [
        maf_root / "Runtime",
        root / "src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution",
    ]
    approved_composition_fragments = {
        "ServiceCollectionExtensions.cs",
        "WorkspaceRuntimeScopeFactory.cs",
        "AgentFrameworkWorkspaceFactory.cs",
    }
    for scan_root in runtime_scan_roots:
        if not scan_root.exists():
            continue
        for path in scan_root.rglob("*.cs"):
            if path.name in approved_composition_fragments:
                continue
            text = read(path)
            if re.search(r"\b(?:private|protected|internal|public)\s+readonly\s+IServiceProvider\b", text):
                errors.append(f"{path.relative_to(root)} retains an IServiceProvider field")
            if re.search(r"\bservices?\.(?:GetService|GetRequiredService|GetServices)\s*<", text):
                errors.append(f"{path.relative_to(root)} performs runtime service location")

    workflow_invoker = root / (
        "src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/"
        "MafWorkflowLlmComponentInvoker.cs"
    )
    if workflow_invoker.is_file():
        text = read(workflow_invoker)
        for token in [
            "IAgentRuntime",
            "IAgentExecutionRuntime",
            "new AgentDefinition(",
            "new ChatSessionRecord(",
            "TryResolveProjectId(",
            "TryResolveProjectScope(",
            "RuntimeCapabilityComposer",
            "AgentRuntimeTransientContext",
        ]:
            if token in text:
                errors.append(
                    f"{workflow_invoker.relative_to(root)} still uses the full-agent workflow path: {token}"
                )
        if "ILlmInvocationPort" not in text:
            errors.append(
                f"{workflow_invoker.relative_to(root)} does not use ILlmInvocationPort"
            )

    llm_abstraction_roots = [
        root / "src/MAF/Common/CanDoItAll.AgentFramework.Llm.Abstractions",
        root / "src/MAF/Llm/CanDoItAll.AgentFramework.Llm.Abstractions",
    ]
    source_root = root / "src"
    if source_root.exists():
        for project in source_root.rglob("*.csproj"):
            project_name = project.stem.casefold()
            if "llm" in project_name and "abstraction" in project_name:
                llm_abstraction_roots.append(project.parent)
    forbidden_llm_abstraction_tokens = [
        "Microsoft.Agents.AI",
        "Microsoft.Extensions.AI",
        "OpenAI.",
        "Azure.AI.OpenAI",
        "AgentDefinition",
        "ChatSessionRecord",
        "IAgentRuntime",
        "IAgentExecutionRuntime",
        "WorkspaceScopeDescriptor",
        "AgentRuntimeTransientContext",
        "ProcessStepOutcome",
        "CanDoItAll.Modules.",
    ]
    existing_llm_roots = sorted({path.resolve() for path in llm_abstraction_roots if path.exists()})
    for llm_root in existing_llm_roots:
        for path in llm_root.rglob("*.cs"):
            text = read(path)
            for token in forbidden_llm_abstraction_tokens:
                if token in text:
                    errors.append(
                        f"{path.relative_to(root)} leaks forbidden lightweight LLM dependency: {token}"
                    )

    lightweight_paths: list[Path] = []
    if source_root.exists():
        lightweight_paths = [
            path
            for path in source_root.rglob("*.cs")
            if any(
                fragment in path.as_posix().casefold()
                for fragment in ("/llm/", ".llm.", "lightweightllm")
            )
        ]
    for path in lightweight_paths:
        text = read(path)
        for token in [
            "new AgentDefinition(",
            "new ChatSessionRecord(",
            "RuntimeCapabilityComposer",
            "AgentRuntimeTransientContext",
            "ApprovalRequiredAIFunction",
        ]:
            if token in text:
                errors.append(
                    f"{path.relative_to(root)} constructs agent-runtime behavior in lightweight LLM path: {token}"
                )

    broad_runtime_callers: list[str] = []
    source_root = root / "src"
    if source_root.exists():
        for path in source_root.rglob("*.cs"):
            if "AgentFramework.Runtime.Abstractions" in path.as_posix():
                continue
            text = read(path)
            if path.name in {"LegacyAgentRuntimeCompatibilityFacade.cs", "IAgentRuntime.Legacy.cs"}:
                continue
            if re.search(r"\bIAgentRuntime\b", text):
                broad_runtime_callers.append(path.relative_to(root).as_posix())
    for caller in broad_runtime_callers:
        errors.append(f"Broad IAgentRuntime production reference remains: {caller}")

    return report(errors)


def report(errors: list[str]) -> int:
    if errors:
        print("Architecture guard failed:")
        for error in sorted(set(errors)):
            print(f"- {error}")
        return 1

    print("Architecture guard passed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
