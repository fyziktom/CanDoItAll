# 01 - Workbench lifecycle compensation and typed node reference

## Status

- `Completed`

## Objective

- Make Workbench delete and subtree-transfer resilient if downstream canonical reconciliation fails, and replace raw node-key bridge parameters with a typed node-reference value for canonical node-scoped operations.

## Covered Inputs

- `RQ-01`
- `RQ-02`

## Prerequisites

- none

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\ProjectPartyIntegrationContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\CrmHrServices.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectNodeScopeBridge.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProjectWorkbenchServiceIntegrationTests.cs`

## Deliverables

- Typed node-reference value on the Workbench canonical bridge surface
- Compensation or equivalent rollback behavior for delete and subtree transfer if CRM/HR reconciliation fails
- Focused automated proof for the failure path and the normal path

## Dependency Impact

- `02` depends on this phase because metadata cleanup must sit on a stable bridge and lifecycle seam.

## Validation Depth

- Critical boundary foundation

## Implementation Steps

1. Introduce a typed node-reference contract for canonical node-scoped bridge operations.
2. Update Workbench and CRM/HR implementations to use that typed reference.
3. Add compensation logic to delete and subtree-transfer flows so Workbench state is restored if canonical reconciliation fails.
4. Add focused tests for the failure path and keep the existing normal-path tests green.

## Do Not Do

- Do not widen this into a generic transaction framework.
- Do not add direct Workbench-to-CRM/HR persistence coupling.

## Acceptance Checklist

- Delete no longer leaves Workbench mutated when canonical assignment cleanup fails.
- Subtree transfer no longer leaves Workbench mutated when canonical assignment move fails.
- Workbench bridge calls stop passing raw node-key strings in canonical node-scoped methods.

## Proof Required

- Relevant build slice
- Focused integration tests for delete and move compensation
- Existing lifecycle reconciliation tests still pass

## Browser Validation Logging

- Target route: `/projects/{ProjectId}/structure`
- Required viewport passes: `1600x1000`
- Required Playwright MCP evidence: smoke the affected structure mutations if browser-visible behavior changed, otherwise record test-backed lifecycle proof and defer visible proof to `03`
- Expected screenshots: structure screenshots refreshed in `03`

## Progression Gate

- `02` may start only after the compensation path and typed node-reference contract are proven by tests.

## Suggested Agent Prompt

```text
Implement this subbundle only. Harden the Workbench canonical bridge with a typed node reference and add compensation so delete and subtree-transfer do not leave partial state when CRM/HR reconciliation fails.
```
