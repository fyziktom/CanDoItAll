# Authoritative Task Pricing and Gantt Behavior

## Status

- `Completed`

## Objective

- Make mixed-assignee task editing safe and persist authoritative resource estimates on owned create/update paths when a task has not happened.

## Success Criteria

- Gantt and canvas dialogs open with person plus Agent assignments.
- Unchanged saves preserve all assignments; direct Person/Agent replacement is read-only for mixed sets.
- New/not-happened task create/update refreshes the selected resource quote.
- Missing pricing clears stale cost/currency with visible feedback.
- A task with explicit occurrence evidence preserves historical cost.

## Covered Inputs

- `N001`, `N002`, `N004`, behavior portions of `N003`; `R001`, `R002`, `R004`, `R005`, `R006`.

## Prerequisites

- `SB01` completed and architecture gate passed.
- A single strongly typed occurrence/pricing-state policy is selected from repository evidence; scheduled dates or scattered free-text heuristics are forbidden.

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.Workbench/Workbench/ProjectWorkbenchMetadata.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureTaskCreationService.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureTaskDetailsService.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/Pages/Components/ProjectStructure/ProjectStructureGanttTaskEditCoordinator.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/Pages/Components/ProjectStructure/ProjectStructureTaskResourceCostEstimator.razor`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.ComponentAdapters.cs`

## UI Composition Contract

- Primary surface: existing Gantt and canvas task dialogs; supporting content and stats unchanged.
- Existing scalar resource control stays in the existing wide modal; no textarea/sizing redesign.
- First viewport: `1600x1000`; modal body remains scroll owner and actions remain visible.
- Open-overlay proof: mixed-assignee task opens without conflict notification and unavailable-source feedback is readable.

## C# Architecture Impact

- Add one lifecycle-aware estimate refresh owner; remove pricing decisions from callers.

## Boundary Ownership

- Refresh service owns cost application; details service owns compensation; coordinators only orchestrate.

## Dependency Direction

- Use `SB01` contracts with no new references/service location.

## Pattern Decision

- Small application policy/service, not a State hierarchy or duplicated decorators.

## Testability Contract

- Direct occurrence/refresh tests plus service integration; no page construction for policy behavior.

## Partial Class Policy

- Existing partial loses duplicated resolution/pricing decisions; no new partial.

## Architecture Proof Required

- All owned persistence paths use the policy; historical tasks skip estimators.

## Deliverables

- Explicit lifecycle metadata, safe mixed-assignee editor state, authoritative estimate refresh/clear, updated UI feedback, and Behavioral tests.

## Dependency Impact

- `SB03` relies on these semantics; browser/regression contradiction reopens this phase and possibly `SB01`.

## Validation Depth

- Proof tier: `Behavioral`.
- Critical behavior phase: yes.

## Implementation Steps

1. Add failing mixed-open/preservation/read-only tests.
2. Add failing refresh, unavailable-clear, and historical-preservation tests.
3. Integrate shared resolver into Gantt/page/details.
4. Integrate estimate refresh at Gantt/agent services and canvas submission/persistence boundaries.
5. Update feedback without layout changes and run affected proof.

## Scope Exceptions

- The dialog remains scalar; multiselect editing and bulk historical repricing are separate.

## Do Not Do

- Do not infer occurrence from schedule alone, preserve stale/invented cost, rely only on UI preview, or weaken concurrency/compensation.

## Acceptance Checklist

- [x] mixed task opens and unchanged save retains both assignments.
- [x] mixed direct-assignee clear/change is unavailable while ordinary fields and additive attachments save.
- [x] latest CRM rate is persisted for a person.
- [x] Agent/process/workflow call their own strategies.
- [x] missing quote clears stale price with feedback.
- [x] happened task preserves price and skips estimator.
- [x] Gantt, canvas, and agent/API owned paths agree.

## Proof Required

- Exact targeted service/component tests, Behavioral positive/negative evidence, affected builds, and rendered dialog proof.

## Browser Validation Logging

- Route `/projects/{projectId}/structure`, Gantt tab; viewport `1600x1000`.
- Activate a mixed-assignee task; assert dialog visible and conflict notification absent.
- Target screenshot `proof/browser/project-structure-gantt-mixed-assignee-dialog.png`.
- Record modal scroll owner, clipping, layering, feedback readability, and action visibility.

## Progression Gate

- `SB03` unlocked after all Behavioral positives/negatives and affected builds passed. See `reviews/01-execution-report.md` for the exact result matrix.

## Reopen Triggers

- Reopen on stale cost after unavailable quote, historical repricing, assignment loss, or browser/test disagreement.

## Suggested Agent Prompt

```text
Implement mixed-assignee preservation and authoritative occurrence-aware repricing through `SB01` boundaries. Prove create/update, unavailable-source, historical-task, and compensation cases; preserve UI composition and stop on ambiguous lifecycle inference.
```
