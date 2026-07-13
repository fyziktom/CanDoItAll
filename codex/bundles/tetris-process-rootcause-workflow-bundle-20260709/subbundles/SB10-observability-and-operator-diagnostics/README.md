# SB10 Observability And Operator Diagnostics

## Status

- `Completed`

## Objective

Expose branch-aware gate decisions, skipped receipt rules, runtime gate findings, completion issue routes, and failed criteria so operators can diagnose repair loopbacks without reading raw logs.

## Covered Inputs

- GPTPro observability and UI diagnostics recommendations.
- User escalation around repairs and branching loopbacks.
- Requirements R04 and R10.

## Prerequisites

- SB03 gate trace shape is available.
- SB04 completion issue route metadata is available.
- SB08 criteria failure records are available or represented by the target contract.

## Exact Source References

- `bundle://codex-tasks/10-observability-and-ui-diagnostics.md`
- `bundle://traceability/01-requirement-traceability.md`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeOperatorActionDiagnostics.cs`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeOperatorContracts.cs`
- `repo://src/Processes/CanDoItAll.Processes.Projections/ProcessRuntimeProjectionProjector.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Components/LiveProcessesDashboard.razor`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessCompletionIssueResultFactory.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessManagedArtifactService.cs`

## Deliverables

- Operator-facing trace model for gate decisions and route outcomes.
- Diagnostic projection showing branch, issue type, selected route, skipped rules, failed criteria, and next action.
- Logs with actionable state and masked sensitive data.
- UI smoke or component test proving diagnostics are visible where process operators inspect blocked runs.

## Dependency Impact

- SB11 depends on observable final proof for the incident regression.
- Future repair work depends on this phase to avoid silent loopbacks.
- Browser validation analytics use the same terminology as operator diagnostics.

## Validation Depth

- Operator UX and diagnostics phase.
- Requires unit tests for projection/log payloads plus UI/component smoke where applicable.

## Implementation Steps

1. Define the trace fields needed by operators: assignment id, branch id, gate id, receipt rule id, applicability result, issue type, route, failed criteria ids, and next action.
2. Add projection support without leaking domain-specific .NET/Blazor terms into generic process state.
3. Add log entries at gate skip, gate failure, route selection, route exhaustion, and repair handoff points.
4. Mask sensitive paths or command arguments in logs while keeping product-root aliases and branch ids.
5. Update the process dashboard/details UI to show concise blocked-run diagnostics using existing component patterns.
6. Add tests for trace projection, masked logs, and UI rendering of a blocked repair-routed completion issue.

## C# Architecture Impact

This phase must improve diagnosis without turning UI components into runtime policy owners.

## Boundary Ownership

- Runtime and application services own trace data.
- Projections own read models.
- Blazor components render diagnostics and call existing services only.

## Dependency Direction

- UI depends on projection/application contracts.
- Runtime must not depend on UI components or Workbench pages.

## Pattern Decision

- Structured diagnostic records projected to UI.
- Rejected: concatenated natural-language log parsing as the UI data source.

## Testability Contract

- Diagnostic projection is unit-testable.
- UI rendering test uses a fixed projection model.
- Log tests assert masked sensitive values.

## Partial Class Policy

- Do not add UI partials as an excuse to grow large components.
- If `LiveProcessesDashboard.razor` grows materially, extract a small wrapper/component and test it.

## Architecture Proof Required

- Dependency proof that UI does not own routing decisions.
- Log masking test transcript.
- Projection source assertion for route and gate fields.

## Do Not Do

- Do not display raw command lines or full native filesystem secrets.
- Do not infer route decisions in Blazor from free-form message text.
- Do not add diagnostics that only exist in logs and cannot be queried.

## Acceptance Checklist

- Operators can see why a run was routed to repair, recheck, retry, manager, or blocked.
- Skipped branch-inapplicable receipt rules are visible as skipped, not failed.
- Failed acceptance criteria ids are visible for complex product flows.
- Logs include actionable state and mask sensitive values.

## Proof Required

- `bundle://proof/SB10/manifest.md` after execution.
- Projection and log test transcripts.
- UI/component smoke transcript where UI changes are made.
- Screenshot or markup assertion for blocked-run diagnostics.
- Anti-stub audit proving diagnostics come from runtime/projection data.

## Browser Validation Logging

- Browser or component validation is required if Blazor UI is changed.
- Record route, viewport, evidence, screenshots, and result in `reviews/01-execution-report.md`.

## Progression Gate

- SB11 final closure must include at least one operator diagnostic proof row for the incident shape.

## Suggested Agent Prompt

Implement SB10 by adding structured operator diagnostics for gate decisions and route outcomes. Keep routing policy out of Blazor components, mask sensitive data, and prove diagnostics through projection and UI tests.
