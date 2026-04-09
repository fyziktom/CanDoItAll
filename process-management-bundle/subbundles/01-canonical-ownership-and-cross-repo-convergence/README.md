# 01 Canonical Ownership And Cross-Repo Convergence

## Status

- `Ready`

## Objective

- Lock the single-source-of-truth rules before any process-module code is implemented, with explicit decisions for CRM-HR, Workspace, Projects, Processes, AgentFramework, and IPFS seams.

## Covered Inputs

- `REQ-002`
- `REQ-003`
- `REQ-004`
- `REQ-011`
- Raw notes `N05` and `N06`
- Legacy features `PRM-F03`, `PRM-F13`, and `PRM-F23`

## Prerequisites

- none

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\CrmHrBusinessModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\WorkspaceModels.cs`
- `C:\repositories\CanDoItAll.AgentFramework\src\CanDoItAll.AgentFramework.Models\ProviderModels.cs`
- `C:\repositories\CanDoItAll.AgentFramework\src\CanDoItAll.AgentFramework.Core\AgentFrameworkWorkspaceService.Agents.cs`
- `C:\repositories\CanDoItAll\process-management-bundle\02-architecture\13-cross-repo-convergence-processes-projects-and-agentframework.md`
- `C:\repositories\CanDoItAll\process-management-bundle\inventories\02-cross-repo-single-source-of-truth-inventory.md`

## Deliverables

- Finalized canonical-ownership matrix for process, staffing, provider, project, runtime, and evidence concerns.
- Explicit migration direction for provider-profile and agent-identity overlaps.
- Role-first execution rule that prevents executor-first process modeling.
- Written guardrails for the future AgentFramework and IPFS bridge seams.

## Dependency Impact

- Every later subbundle depends on this decision set.
- If this subbundle is weak, later staffing, runtime, bridge, and analytics work can silently create dual truth and invalidate downstream proof.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Reinspect the current repo evidence with `candoitall-codeanalytics-mcp`.
2. Finalize the ownership inventory and explicit overlap decisions.
3. Confirm the role-first rule and prohibited patterns for the first merge.
4. Record reopen triggers for any later duplicate-registry drift.

## Scope Exceptions

- Full `CanDoItAll.AgentFramework` runtime embedding remains deferred to a later bundle after the core process module ships.

## Do Not Do

- Do not create a second durable provider registry.
- Do not create a second durable AI identity registry.
- Do not treat runtime-side sandbox models as production truth.

## Acceptance Checklist

- Ownership of process, role, provider, project, runtime, and artifact concerns is explicit.
- The bundle states where AgentFramework may integrate later and where it may not.
- The bundle states where IPFS may integrate later and where it may not.
- The bundle explains why a process role survives executor changes.

## Proof Required

- CodeAnalytics-backed evidence from the current main and AgentFramework snapshots.
- Updated inventory and architecture documents with no unresolved ownership ambiguity.
- Explicit confirmation that the first merge requires no `CanDoItAll.AgentFramework.*` project reference.

## Browser Validation Logging

- `N/A`

## Progression Gate

- This subbundle passes only when the canonical-ownership inventory is internally consistent and no later phase depends on an unresolved duplicate-truth decision.

## Suggested Agent Prompt

```text
Implement only the canonical-ownership hardening work for process management. Recheck the current CanDoItAll and AgentFramework repos, finalize the single-source-of-truth inventory, and stop if any overlap decision remains ambiguous.
```
