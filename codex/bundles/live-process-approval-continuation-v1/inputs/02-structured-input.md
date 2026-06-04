# Structured Input

## Core Objective

- Make Live Processes escalation actions continue the process through the correct runtime path and stop labeling blocked-step rework as approval.

## Success Criteria

- A blocked or failed step escalation offers a governed rework action instead of a misleading approval action.
- A true approval-required escalation can continue its source execution run only when it is bound to a real execution approval.
- The reported run `01ee78c6-077e-4a6c-8139-1f4120e659a5` no longer depends on the stuck manager-chat quick-decision path for the user's attempted unblock.
- Regression tests cover the escalation-action routing decision.

## Hard Constraints

- Preserve existing Process Workspace operator console behavior.
- Keep the fix strongly typed and scoped to process observation/action handling.
- Do not silently treat non-approval escalations as approved.

## Allowed Side Effects

- Changes may touch Live Processes dashboard components, process observation models/projection, and focused tests.
- The running 5032 app may be restarted after a successful build if required to load the repaired code.

## Source Artifacts

- `repo://src/CanDoItAll.Modules.Processes/Components/LiveProcessesDashboard.razor`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationModels.cs`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationService.cs`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessOperatorControlPlane.cs`
- Live API evidence from `http://localhost:5032/api/processes/runs/01ee78c6-077e-4a6c-8139-1f4120e659a5`

## Input Coverage Signals

- N001: User clicked Approve in Live Processes for a blocked process on port 5032 and it did not continue.

## Dependency And Sequencing Signals

- The UI action semantics must be fixed before validating the user's unblock workflow; otherwise another click can create another stuck manager-chat run.

## Validation Expectations

- Run focused tests for the new action routing and compile affected projects.
- Validate the live 5032 run state or action surface after deployment/restart.

## Evidence Contract

- Command proof: `dotnet test` for the targeted test project/filter.
- Host/API proof: query live 5032 run/escalation state before and after the repair.
- UI proof: browser route check for Live Processes if the app can be loaded with the repaired code.

## UI Validation Strategy

- Use the in-app browser against the Live Processes route on `http://localhost:5032` after build/restart. Check that the blocked escalation action label is not `Approve` and that the layout remains usable.

## Browser Validation Analytics

- Log route, viewport, action labels, and screenshot path in `reviews/01-execution-report.md` if browser validation is available.

## Working Assumptions

- The blocked run is `01ee78c6-077e-4a6c-8139-1f4120e659a5`.
- The blocked step escalation is not a pending execution approval; it is a blocked-step/recovery escalation.

## Primary Risks

- Retrying a blocked step can start new governed automation; this must be explicit and not hidden behind an approval label.
- Manager chat may still be useful for discussion but should not be the direct quick-action path for continuation.
