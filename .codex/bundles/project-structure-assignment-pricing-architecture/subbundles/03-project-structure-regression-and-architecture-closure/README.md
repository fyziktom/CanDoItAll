# Project Structure Regression and Architecture Closure

## Status

- `Completed`

Independent final C# architecture gate passed with non-blocking follow-up.

## Objective

- Prove that the focused extraction preserves Project Structure behavior, validate the mixed-assignee Gantt flow in the rendered application, and close the architecture and bundle gates.

## Success Criteria

- The targeted assignment, pricing, creation, details, projection, Gantt, and canvas component suites pass.
- The affected Workbench, Web, component, unit, and integration project build slice passes without new warnings attributable to this work.
- Project Structure gains no partial class; the canvas task slice is delegated to a top-level coordinator and the partial cluster loses the owned orchestration.
- A maximized desktop Gantt smoke opens and inspects a mixed-assignee task without the conflict notification; direct application-service proof establishes save/preserve-both behavior without mutating developer browser data.
- Final C# architecture and bundle validators pass with no open critical findings.

## Covered Inputs

- `N005`, `N006`; closure of `R007` and `R008`; regression proof for `R001` through `R006`.

## Prerequisites

- `SB01` and `SB02` are completed with their evidence recorded.
- The C# architecture review is `Pass` or `Pass with explicit non-critical follow-up`.
- A runnable local Web host and seedable mixed-assignment fixture are available for browser proof; otherwise the exact environment blocker and closest rendered proof are recorded.

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.ComponentAdapters.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/Pages/Components/ProjectStructure/ProjectStructureGanttPanel.razor.cs`
- `repo://tests/Components/CanDoItAll.Tests.Components/ProjectStructureGanttPanelTests.cs`
- `repo://tests/Components/CanDoItAll.Tests.Components/ProjectStructureGanttTaskDialogTests.cs`
- `repo://tests/Components/CanDoItAll.Tests.Components/ProjectStructurePageTaskAssigneeCreationTests.cs`
- `repo://tests/Playwright/CanDoItAll.Tests.Playwright`

## UI Composition Contract

- Primary surface and supporting-content placement: existing Project Structure Gantt tab and existing task-details dialog; no composition change.
- Stats treatment and reason: unchanged because this bundle repairs behavior, not information hierarchy.
- List/editor organization, including dialog, tab, inline, or split decision: unchanged wide modal; mixed-assignment safety appears beside the existing resource editor.
- Textarea sizing and dialog size rationale: unchanged.
- First-viewport target and intended scroll owner: maximized `1600x1000`; the modal body remains the scroll owner and footer actions remain visible.

## Deliverables

- Extracted canvas task-dialog coordinator with thin page delegation.
- Targeted unit/component/integration test completion.
- Build and browser evidence.
- Final architecture review, raw-input closure, and completed bundle validation.

## Dependency Impact

- This is the terminal phase. Any failure reopens `SB01` for strategy/assignment-boundary defects or `SB02` for lifecycle/persistence defects.

## Validation Depth

- Proof tier: `Behavioral`.
- Critical foundation: no; terminal user-visible and architecture closure across Project Structure task editing.

## Implementation Steps

1. Extract the canvas task-dialog orchestration and remove its dependencies/methods from the page partial.
2. Run focused unit/component/integration suites and repair only owned regressions.
3. Build the affected Workbench, Web, component, unit, and integration project slice.
4. Start the local Web host and execute the named large-screen Gantt smoke.
5. Review normal/open-dialog screenshots; close the browser dialog without save and use direct application-service proof to verify both canonical assignments survive a safe save.
6. Run the architecture review gate, close traceability/raw notes, and run completed bundle validation.

## Scope Exceptions

- Whole-page process/workflow decomposition remains outside this focused bundle.
- Responsive/mobile redesign remains outside scope.

## Do Not Do

- Do not broaden into unrelated Project Structure partials, introduce new UI composition, weaken assertions to make tests pass, or claim browser proof without an actual rendered interaction.

## Acceptance Checklist

- [x] canvas task orchestration no longer lives in `ProjectStructurePage.ComponentAdapters.cs`.
- [x] Project Structure partial count does not increase and the owned page slice shrinks.
- [x] targeted tests and builds pass.
- [x] page-preservation suites pass `35/35` across four split invocations after a test-only canonical `DialogHost` repair; the exact modal regression passes `1/1`.
- [x] mixed Gantt task opens; ordinary safe-save semantics are proven in the application service tests.
- [x] person and agent assignments remain after application-service save proof; browser dialog was closed without save to protect developer data.
- [x] latest eligible price behavior and missing-source feedback match service proof.
- [x] independent architecture gate passed with non-blocking follow-up; narrow bridge growth and future bulk delete/move changes require the documented focused assertions.

## Proof Required

- Exact targeted `dotnet test` commands from the execution report.
- `dotnet build` output for the affected Workbench, Web, component, unit, and integration project slice.
- Source/line-count assertion for the partial cluster and extracted coordinator.
- Maximized `1600x1000` browser pass with normal Gantt and open task-dialog screenshots.
- DOM assertions for dialog visibility, absence of the assignment-conflict notification, read-only mixed direct-assignee controls, and visible save action. Safe post-save preservation is covered by the direct application-service test, not by mutating developer browser data.

## Browser Validation Logging

- Target route: `/projects/{projectId}/structure?tab=gantt`.
- Viewport: `1600x1000`.
- Actions/assertions: load Gantt, activate a task with primary plus secondary direct assignees, assert the dialog opens, inspect enabled non-assignment fields and protected direct-assignment controls, then close without save. `ProjectStructureTaskApplicationServiceTests` provides the save/revision/compensation preservation proof.
- Evidence: `proof/browser/project-structure-gantt-normal.png` and `proof/browser/project-structure-gantt-mixed-assignee-dialog.png`.
- Review: no clipping/layering regression; mixed warning readable; direct assignment locked; additive Workflow/Process actions and footer save visible; modal body owns scrolling.

## Progression Gate

- The bundle closes after tests/builds, browser proof, final architecture review, full traceability, and completed validator agree. All closure gates now agree; the architecture follow-ups are non-blocking future-change triggers.

## Reopen Triggers

- Any assignment loss, stale NotStarted price, historical/Unknown repricing, strategy registration error, browser contradiction, or unrelated Project Structure regression reopens the owning earlier subbundle and this closure phase.
- A future modal-harness change that bypasses canonical `DialogHost`, or a failure in task-assignee creation, simple mutation, move, or database-switch preservation suites, reopens SB03.

## Suggested Agent Prompt

```text
Close the Project Structure assignment/pricing bundle only after extracting the canvas task slice, running the named regression/build suite, performing the rendered mixed-assignee Gantt smoke, and passing the final architecture and bundle gates. Reopen the owning phase on any contradiction.
```
