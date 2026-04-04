# 02 - Node lifecycle reconciliation and canonical guardrails

## Status

- `Completed`

## Objective

- Reconcile canonical node-scoped assignment rows when nodes are deleted or when descendants move into another project.

## Covered Inputs

- `RQ-04 Lifecycle Reconciliation`
- `RQ-05 Boundary Discipline`
- `RQ-06 Test Coverage`

## Prerequisites

- `01-canonical-node-assignment-owner-and-editor-read-path` must be completed or honestly blocked before this subbundle starts.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\ProjectPartyIntegrationContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\CrmHrServices.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchModels.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProjectPartyAssignmentIntegrationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProjectWorkbenchServiceIntegrationTests.cs`

## Deliverables

- Workbench delete flow removes node-scoped assignment rows for deleted nodes.
- Workbench subtree-transfer flow moves node-scoped assignment rows to the target project.
- Integration coverage protects those lifecycle seams.

## Dependency Impact

- `03` depends on this phase because browser proof and the post-fix architecture review are not trustworthy if stale canonical assignments remain after lifecycle mutations.

## Validation Depth

- Critical lifecycle foundation

## Implementation Steps

1. Extend the project-facing bridge with node-delete cleanup and subtree-transfer reassignment operations.
2. Implement those operations in CRM/HR using one canonical storage boundary.
3. Call those operations from Workbench delete and subtree-transfer flows.
4. Add or extend integration tests that explicitly assert assignment cleanup and transfer.
5. Re-run the targeted lifecycle slices before browser proof starts.

## Scope Exceptions

- This phase does not replace `NodeKey` with a typed identifier.

## Do Not Do

- Do not add a second source of truth for lifecycle bookkeeping.
- Do not hide failed lifecycle reconciliation behind no-op fallback behavior.

## Acceptance Checklist

- Deleting a node subtree removes its canonical node-scoped assignments.
- Moving descendants into another project moves canonical node-scoped assignments to the target project.
- Assignment validation still rejects missing or cross-project node references after the lifecycle changes.

## Proof Required

- Run targeted integration tests for delete and subtree-transfer flows.
- Run the relevant build slice.
- If lifecycle behavior changes surface UI behavior, include that verification in the browser closure phase.

## Browser Validation Logging

- Target route: `/projects/{ProjectId}/structure`.
- Required viewport passes: `1600x1000`.
- Required Playwright MCP evidence: if executed from the browser, perform the affected structure mutation and confirm the surface stays healthy afterward; otherwise record that lifecycle proof is test-backed and the UI smoke remains in `03`.
- Expected screenshots: final structure screenshot set can be recorded in `03`.
- Screenshot review questions: does the structure remain stable after the mutation and is there any visible stale selection or broken shell state?

## Progression Gate

- `03` may start only after lifecycle integration tests prove canonical assignments do not drift on delete or subtree transfer.

## Suggested Agent Prompt

```text
Implement this subbundle only. Reconcile node-scoped assignments through the project-facing bridge during Workbench delete and subtree-transfer flows, and add the missing lifecycle integration coverage.
```
