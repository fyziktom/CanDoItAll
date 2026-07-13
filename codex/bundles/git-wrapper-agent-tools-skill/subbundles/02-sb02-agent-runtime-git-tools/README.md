# SB02-agent-runtime-git-tools

## Status

- `Completed`

## Objective

Expose the bounded git operation set through workspace command execution, MAF runtime tool composition, and tool policy metadata.

## Success Criteria

- Workspace command execution exposes read-only git tools and mutation git tools backed by SB01 specs.
- MAF workspace plugin methods and tool factories attach every shipped git tool.
- Tool names are centralized in `ToolContractCatalog`.
- Read-only agents do not receive mutation git tools.

## Covered Inputs

- REQ-003
- REQ-004
- REQ-006
- REQ-007

## Prerequisites

- SB01 closure gate passed.
- `bundle://proof/SB01/manifest.md` exists and cites wrapper command-spec proof.

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Core/CanDoItAll.AgentFramework.Core.csproj`
- `repo://src/CanDoItAll.AgentFramework.Core/Workspace/Process/WorkspaceProcessContracts.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Workspace/Commands/WorkspaceCommandPlanBuilder.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Workspace/Commands/WorkspaceCommandExecutionService.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/MafAgentRuntime.WorkspaceRuntimePlugin.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Tools.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Tools.ConfiguredWorkspace.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/ToolContractCatalog.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/ToolCapabilityRegistry.cs`
- `repo://src/CanDoItAll.AgentFramework.Models/Agents/Access/AgentWorkspaceToolAccessModels.cs`
- `repo://tests/CanDoItAll.Tests.Unit/WorkspaceCommandExecutionServiceTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/AgentWorkspaceToolAccessMetadataTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/MafAgentRuntimeToolProviderCompositionTests.cs`

## Deliverables

- New runtime tools: `workspace_git_log`, `workspace_git_show`, `workspace_git_add`, `workspace_git_unstage`, `workspace_git_commit`, `workspace_git_branch_create`, and `workspace_git_switch`.
- Existing tools `workspace_git_status` and `workspace_git_diff` routed through shared wrapper specs.
- Access-policy and tool-classification updates.
- Focused tests for command plans, access gating, and runtime composition.

## Dependency Impact

- SB03 depends on final tool names and policy behavior.
- If SB02 misclassifies a mutation tool as read-only, downstream agents can receive unsafe capabilities.

## Validation Depth

- Critical foundation.
- Requires Semantic Adequacy Gate proof with shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, raw-note literal closure, `proof/SB02/manifest.md`, and `proof/SB02/semantic-invariants.md`.

## Implementation Steps

1. Add `CanDoItAll.Git` as a project reference to `CanDoItAll.AgentFramework.Core`.
2. Add new methods to `IWorkspaceCommandExecutionService` and `WorkspaceCommandExecutionService`.
3. Route workspace command plans through SB01 command specs.
4. Add MAF workspace plugin methods.
5. Add tool factories for individual capability keys and configured workspace plugin attachment.
6. Update tool constants, capability registry, and access metadata.
7. Add focused unit tests and proof artifacts.

## Scope Exceptions

- Do not add remote/network tools.
- Do not expose merge/conflict operations to agents in this bundle.

## Do Not Do

- Do not copy raw git argument grammar into each MAF method.
- Do not classify git mutations as read-only.
- Do not grant mutation git tools to read-only process contexts.

## Acceptance Checklist

- Each shipped tool exists in the command service, MAF plugin, tool factories, `ToolContractCatalog`, and policy mapping.
- Read-only git tools remain usable by read-only agents.
- Mutation git tools require manage-paths/software-development access and approval metadata.
- Focused runtime/access tests pass.

## Proof Required

- Focused command transcript covering workspace command and access/runtime tests.
- `bundle://proof/SB02/manifest.md`
- `bundle://proof/SB02/semantic-invariants.md`
- `bundle://proof/SB02/source-assertions.md`
- `bundle://proof/SB02/anti-stub-audit.txt`

## Browser Validation Logging

- N/A - no browser-visible or host-visible UI behavior.

## Progression Gate

- SB03 may start only after SB02 proof shows every final runtime tool name is present across command service, MAF composition, tool policy, and access metadata.
- SB02 proof must include read-only negative proof for mutation tools.

## Suggested Agent Prompt

```text
Implement SB02 only. Consume the SB01 git specs from workspace command execution, expose the bounded git tool surface through MAF, classify tools correctly, prove read-only and mutation access behavior, and update the SB02 proof artifacts. Stop before template and skill edits.
```
