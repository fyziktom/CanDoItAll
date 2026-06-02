# SB07 - UI Editor And Observability Hardening

## Status

Passed. Classification: **Non-critical / dependent hardening**. Proof recorded under `proof/SB07/`.

## Objective

Refactor and harden browser-visible workflow/process/provider/capability UI so it displays canonical state, executor availability, proof status, runtime host identity, and usage/cost state without duplicating runtime logic.

## Covered Inputs

Covers WorkflowCanvasEditor size, LiveProcessesDashboard size, provider/capability setup UI, process graphs, numeric enum shape, executor availability display, token/cost display, and proof observability.

## Prerequisites

SB01 completed. SB02-SB05 completed for runtime contracts. SB03 completed before cost UI changes. SB06 completed before skill/template UI claims active behavior.

## Exact Source References

- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasEditor.razor`
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasEditor.razor.cs`
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/CapabilitySetupWizardDialog.razor`
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/CapabilityDetailsDialog.razor`
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentProviderProfilesPanel.razor`
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentCapabilitiesPanel.razor`
- `repo://src/CanDoItAll.Modules.Processes/Components/LiveProcessesDashboard.razor`
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessObservationGraphsPanel.razor`
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Graphs.cs`

## Deliverables

- Extracted UI view models/display adapters where appropriate.
- Canonical status/proof/cost display adapters.
- Executor availability/side-effect warning UI.
- Usage known/unknown UI.
- Component tests and Playwright screenshots.
- UI observability notes in execution report.

## Dependency Impact

SB08 benefits from clearer dashboard/process state; SB09 validates UI does not mislead.

## Validation Depth

Moderate-to-deep validation. UI changes require Playwright proof and screenshots, but semantic critical proof is inherited from SB01-SB05.

## Implementation Steps

1. Identify UI logic that duplicates runtime policy or parses raw numeric enum values.
2. Extract display adapters/view models consuming canonical DTOs.
3. Split large `.razor.cs` code-behind where safe.
4. Add executor availability and side-effect warning display.
5. Add known/unknown usage display.
6. Add process proof/runtime host identity display where available.
7. Run component tests and Playwright checks.

## Scope Exceptions

Do not change runtime behavior in this subbundle except small DTO/display adapter additions required for UI.

## Do Not Do

- Do not parse raw numeric enums directly in UI.
- Do not show precise cost if usage is incomplete.
- Do not hide unavailable executor state.
- Do not accept screenshots without inspecting layout/readability.

## Acceptance Checklist

- [x] UI compiles.
- [x] Component tests pass.
- [x] Playwright proof exists for changed routes.
- [x] Cost/proof/executor statuses are clear.
- [x] No new duplicated runtime policy in UI.
- [x] Execution report browser analytics rows are updated.

## Proof Required

- Command transcripts for build/tests.
- Playwright screenshots.
- Source assertions showing display adapters consume canonical state.
- Anti-stub audit for UI placeholder statuses.
- Raw-note closure for UI/observability requirements.

## Browser Validation Logging

Required. Use a large desktop viewport first, then one narrower responsive pass if layout changes. Record route, viewport, Playwright actions, screenshots, console evidence, visual review notes, and result.

## Progression Gate

SB07 passes when UI displays canonical state accurately and browser proof confirms readability/interactions.

## Suggested Agent Prompt

Implement SB07 only. Refactor UI around canonical display adapters and prove visible behavior with Playwright screenshots.
