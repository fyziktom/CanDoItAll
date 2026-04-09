# 14 AgentFramework Bridge And Registry Convergence

## Status

- `Ready`

## Objective

- Define and implement the future bridge contracts for external execution while keeping registries converged to CanDoItAll-owned truth and keeping the first process-module merge independent from AgentFramework projects.

## Covered Inputs

- `REQ-003`
- `REQ-004`
- `REQ-010`
- `REQ-011`
- `REQ-013`
- Legacy features `PRM-F13` and `PRM-F23`

## Prerequisites

- `12-post-implementation-bundle-phase02-generation`

## Exact Source References

- `C:\repositories\CanDoItAll\process-management-bundle\03-subbundles\PRM-F13-future-agentframework-adapter-and-ai-executor-seam\README.md`
- `C:\repositories\CanDoItAll\process-management-bundle\03-subbundles\PRM-F23-agentframework-convergence-process-bound-context-and-shared-registries\README.md`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\CrmHrBusinessModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\WorkspaceModels.cs`
- `C:\repositories\CanDoItAll.AgentFramework\src\CanDoItAll.AgentFramework.Models\AgentModels.cs`
- `C:\repositories\CanDoItAll.AgentFramework\src\CanDoItAll.AgentFramework.Models\ProviderModels.cs`
- `C:\repositories\CanDoItAll.AgentFramework\src\CanDoItAll.AgentFramework.Core\AgentFrameworkWorkspaceService.Chat.cs`

## Deliverables

- Process-owned external-execution bridge contracts with correlation IDs.
- Explicit mapping from process governance to future runtime permission envelopes and autonomy limits.
- Registry-convergence rules that keep role templates, identities, and provider profiles canonical in CanDoItAll.
- Tests proving the bridge stays compile-time independent from AgentFramework projects.

## Dependency Impact

- Management UX, analytics, and later runtime integration will build on these bridge contracts.
- If this subbundle is wrong, the later AgentFramework merge will reopen core process and CRM-HR boundaries.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Define process-owned bridge contracts and external execution links.
2. Map process governance into future permission and approval envelopes.
3. Lock registry-convergence rules for provider, identity, and capability truth.
4. Prove no compile-time AgentFramework dependency was introduced.

## Scope Exceptions

- Full runtime embedding stays deferred; this subbundle only creates the safe bridge seam.

## Do Not Do

- Do not add `CanDoItAll.AgentFramework.*` references to the first merge.
- Do not let runtime-side provider or agent shapes become canonical business truth.
- Do not allow external runtime evidence without process correlation IDs.

## Acceptance Checklist

- Bridge contracts are process-owned.
- Registry ownership rules are explicit and tested.
- Process policy can narrow future runtime permissions.
- Build proof shows no compile-time AgentFramework dependency.

## Proof Required

- Unit tests for bridge mapping and ownership rules.
- Project-reference review and successful build proof.
- CodeAnalytics or direct file proof that provider and agent truth stayed in Workspace and CRM-HR.

## Browser Validation Logging

- `N/A`

## Progression Gate

- Phase 03 UI work may finish, and phase 04 analytics may begin, only after the bridge contracts and registry-convergence rules are proven stable.

## Suggested Agent Prompt

```text
Implement only the future external-execution bridge seam. Keep the first process-module merge free of AgentFramework project references, map process policy into future runtime permissions, and lock registry convergence to CanDoItAll truth.
```
