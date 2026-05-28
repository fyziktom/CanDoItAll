# 01-live-process-approval-actions

## Status

- `Completed`

## Objective

- Repair Live Processes escalation quick actions so blocked-step recovery requests governed rework directly and true approvals use direct approval continuation.

## Success Criteria

- Blocked/failed step escalations do not render `Approve`/`Deny`.
- Step-scoped recovery escalations call `IProcessEscalationService.RequestReworkAsync`.
- True approval escalations with source ids call `IAgentFrameworkWorkspaceService.ContinueExecutionRunAsync` and record the decision.
- Focused tests and live validation prove the reported stuck path is removed.

## Covered Inputs

- N001 and requirements R001-R005.

## Prerequisites

- Prepared bundle validation passes.
- Live evidence confirms the reported escalation is `BlockedStep`.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Components/LiveProcessesDashboard.razor`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationModels.cs`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationService.cs`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessOperatorControlPlane.cs`
- `repo://tests/CanDoItAll.Tests.Integration`

## Deliverables

- Typed action policy for Live Processes escalation actions.
- Updated live escalation projection carrying source approval metadata.
- Updated dashboard action rendering and dispatch.
- Focused test coverage.
- 5032 validation evidence.

## Dependency Impact

- This is the only implementation subbundle. Weak proof invalidates final closure because the user's reported click path is the entire defect.

## Validation Depth

- Process-critical UI/action closure.

## Implementation Steps

1. Add source approval metadata to `ProcessLiveEscalationCard` and its projection.
2. Add a small action policy for approval, rework, resolve, and manager-message classification.
3. Update Live Processes card and detail dialog actions to use the policy.
4. Dispatch rework and approval actions directly through existing services.
5. Add focused tests.
6. Build/test, deploy/restart if needed, and validate port 5032.

## Scope Exceptions

- Do not change the broader manager-chat runtime unless proof shows it is still required after quick actions stop using it for blocked-step continuation.

## Do Not Do

- Do not rewrite process recovery, artifact validation, or Process Workspace.
- Do not make approval silently override a blocked-step contract.
- Do not hide failures behind a fallback chat message.

## Acceptance Checklist

- The observed blocked escalation has a rework-oriented action label: `Request rework`.
- The rework action queues governed rerun/rework and completed the live process run after metadata repair.
- Direct approval code path is guarded by source execution approval metadata.
- Tests pass.

## Proof Required

- Test proof: `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessLiveEscalationActionPolicyTests|FullyQualifiedName~BuildProcessInvocationMetadataJson_grants_read_only_upstream_external_artifact_paths_for_managed_review_contract" --no-restore` exited `0`; 4 passed.
- Build proof: the targeted test command built affected projects successfully; existing `MSB3277` Entity Framework Core warnings remain.
- Live 5032 proof: health returned `200 Healthy`; Live Processes HTML rendered `Request rework` and no blocked-step `Approve`.
- Runtime proof: process run `01ee78c6-077e-4a6c-8139-1f4120e659a5` completed after rework packet `8bb0da31-0215-461e-942a-201df38ff3d6`; execution run `2635c7a1-f057-418e-b929-32b21c241ba7` recorded successful receipts for both external product files.

## Browser Validation Logging

- Target: Live Processes route on `http://localhost:5032`.
- Viewport: desktop; narrower follow-up if layout changes are visible.
- Assertions: blocked-step action label is not `Approve`; rework action is visible; no overlapping action text.
- Screenshots/evidence: record paths or note API-only validation blocker in `reviews/01-execution-report.md`.

## Progression Gate

- Passed. Targeted tests passed and the 5032 validation result is recorded in `reviews/01-execution-report.md`.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
