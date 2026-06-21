# SB17 Definition Canvas, Toolbox, Selection, And Recomposition

## Status

- Completed

Completed on 2026-06-16 during architecture bundle v3 execution.

## Objective

Rebuild the definition canvas, toolbox, node/port rendering, selection model, layout/recomposition behavior, and canvas command adapter over definition canvas projections.

## Covered Inputs

- REQ-005, REQ-011, REQ-030, REQ-042, REQ-051, REQ-052.
- US-018 and US-019.
- AC-003, AC-021, AC-033, AC-035, AC-039, AC-040.

## Prerequisites

- SB16 role editor complete.
- Definition canvas projection contract available from SB10/SB13.

## Exact Source References

- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Modules.Processes/Canvas`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceStepsTab.razor`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/tests/CanDoItAll.Tests.Components/ProcessCanvasCatalogTests.cs`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/tests/CanDoItAll.Tests.Components/ProcessCanvasRecompositionServiceTests.cs`
- `repo://codex/bundles/process-module-architecture-v3/evidence/ui-current-state/processes-steps-tab-1600x1000.png`

## Target Projects / Files

- `src/CanDoItAll.Modules.Processes`
- New Process application/projection projects introduced by upstream backend subbundles.
- `tests/CanDoItAll.Tests.Components`
- `tests/CanDoItAll.Tests.Playwright`
- Subbundle proof directory for screenshots, snapshots, and execution report artifacts.

## Deliverables

- Definition canvas rendering from `DefinitionCanvasProjection`.
- Toolbox actions for creating canvas elements through typed commands.
- Selection panel behavior for nodes, routes, artifacts, roles, and subprocess boundary nodes.
- Canvas recomposition tests and Playwright canvas proof.

## Dependency Impact

- SB18 depends on stable canvas selection and command routing for step editing.
- SB23 later reuses visual concepts for runtime canvas projections.

## Validation Depth

- Component tests for canvas rendering, toolbox actions, selection, and recomposition.
- Projection-only dependency scan.
- Playwright proof for canvas load, selection, add action, and stable layout.

## Refactoring Review Checkpoint

- Keep component rendering separate from projection loading and command dispatch.
- Keep projection client code out of low-level visual components.
- Split large components or services before handoff if they combine unrelated workflow areas.
- Verify UI code does not reference runtime internals, EF runtime entities, or old observation services.

## Performance Antipattern Notes

- Read `architecture/19-dotnet-performance-guardrails.md` and `validation/05-dotnet-performance-antipattern-checklist.md` before creating or modifying C# hot-path code.
- Record exact performance scan counts in the execution report when this subbundle changes runtime, dispatcher, manager, projection, template, Git, adapter, persistence, or UI service code.
- Do not introduce sync-over-async, unbounded event/projector queues, per-call `HttpClient`, per-call `JsonSerializerOptions`, load-all UI queries, or LINQ-heavy hot paths without a recorded mitigation and proof.
## Implementation Steps

1. Render existing process definition graph from canvas projection DTOs.
2. Implement selection state as explicit UI state, not hidden component side effects.
3. Connect toolbox actions to typed canvas commands and command receipts.
4. Rebuild recomposition behavior with deterministic layout constraints.
5. Add component, visual, and Playwright proof.
6. Record story coverage for US-018 and US-019.

## Do Not Do

- Do not derive definition truth by parsing DOM/canvas state.
- Do not bypass application commands to mutate definitions.
- Do not implement detailed step forms in this bundle.

## Stop And Report Conditions

- Stop if required projection fields are missing and would force direct runtime or persistence access from UI.
- Stop if preserving the current UX requires reviving old dispatcher/runtime behavior.
- Stop if browser proof cannot be captured for an owned browser-facing story.
- Stop if a story appears to require removal or major UX replacement without explicit user approval.

## Acceptance Checklist

- [x] Canvas renders from projection DTOs.
- [x] Selection and toolbox actions are explicit and testable.
- [x] Layout remains stable after recomposition.
- [x] Playwright screenshot proof exists.

## Proof Required

- Component/canvas test output.
- Playwright canvas screenshot and action evidence.
- Dependency scan output.
- Story coverage table for US-018 and US-019.

## Browser Validation Logging

- Required. Capture steps tab route/state, viewport, canvas action, selected element assertion, screenshot, and console/network summary.

## Progression Gate

- SB18 may start after selection and canvas command routing are proven. SB17 proof is recorded under `bundle://proof/SB17/`.

## Suggested Agent Prompt

Execute SB17 from `codex/bundles/process-module-architecture-v3/subbundles/17-definition-canvas-toolbox-selection-and-recomposition`. Rebuild definition canvas behavior over projections and typed commands.

## Handoff Notes For Next Bundle

Selected element DTO shape and command receipts are available through `ProcessDefinitionCanvasSelectionProjection`, `ProcessDefinitionCanvasCommand`, `ProcessDefinitionCanvasCommandReceipt`, and `ProcessDefinitionCanvasCommandResult`. SB18 should consume those projections for step editor forms instead of querying runtime or persistence state from the UI.
