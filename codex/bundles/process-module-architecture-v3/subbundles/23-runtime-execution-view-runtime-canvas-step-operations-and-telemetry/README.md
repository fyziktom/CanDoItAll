# SB23 Runtime Execution View, Runtime Canvas, Step Operations, And Telemetry

## Status

Future implementation package; prepared by architecture bundle v3; not executed in v3.

## Objective

Rebuild active execution views, runtime canvas, runtime step operations, subprocess run actions, provider/execution telemetry, and invariant diagnostics over runtime projections and typed operator commands.

## Covered Inputs

- REQ-002, REQ-003, REQ-012, REQ-013, REQ-026 to REQ-030, REQ-051, REQ-052.
- US-033 through US-035 and US-052.
- AC-005 to AC-009, AC-013, AC-021, AC-039, AC-040.

## Prerequisites

- SB22 selected run context complete.
- SB07 runtime/dispatcher state machines and SB09 subprocess manager contracts complete.

## Exact Source References

- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunsCanvasSection.razor`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Modules.Processes/Runtime`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/tests/CanDoItAll.Tests.Integration/ProcessRuntimeOperatorReadModelTests.cs`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/tests/CanDoItAll.Tests.Integration/ProcessSubprocessIntegrationTests.cs`

## Target Projects / Files

- `src/CanDoItAll.Modules.Processes`
- New Process application/projection projects introduced by upstream backend subbundles.
- `tests/CanDoItAll.Tests.Components`
- `tests/CanDoItAll.Tests.Playwright`
- Subbundle proof directory for screenshots, snapshots, and execution report artifacts.

## Deliverables

- Active execution and agent activity projections.
- Runtime canvas projection UI with step status and subprocess boundaries.
- Runtime step operation commands: open subprocess run, start, complete, block, wait approval, refuse, fail, and prepare artifact capture.
- Telemetry and invariant diagnostic projections.

## Dependency Impact

- SB24 operator console depends on runtime incidents, diagnostics, and telemetry summaries.
- SB28 subprocess regression depends on this proof.

## Validation Depth

- Runtime state machine tests for all exposed step operation commands.
- Projection tests for runtime canvas and telemetry.
- Playwright proof for runtime canvas action dialog and subprocess run navigation.

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

1. Bind active execution and runtime canvas UI to projections.
2. Implement runtime step operation command dispatch with receipts.
3. Render subprocess hierarchy and open-child-run actions.
4. Render telemetry and invariant diagnostics safely.
5. Add tests and Playwright proof.
6. Record story coverage for US-033 through US-035 and US-052.

## Do Not Do

- Do not mutate runtime state from UI without authorized commands.
- Do not infer runtime state from canvas colors or DOM state.
- Do not expose unrestricted raw diagnostics.

## Stop And Report Conditions

- Stop if required projection fields are missing and would force direct runtime or persistence access from UI.
- Stop if preserving the current UX requires reviving old dispatcher/runtime behavior.
- Stop if browser proof cannot be captured for an owned browser-facing story.
- Stop if a story appears to require removal or major UX replacement without explicit user approval.

## Acceptance Checklist

- [ ] Runtime canvas renders from projection DTOs.
- [ ] Step operations are typed and state-machine validated.
- [ ] Subprocess run actions preserve parent/child context.
- [ ] Telemetry and diagnostics are redacted as required.
- [ ] Browser proof exists.

## Proof Required

- Runtime/projection/component test output.
- Playwright runtime canvas screenshot evidence.
- Story coverage table for US-033 through US-035 and US-052.

## Browser Validation Logging

- Required. Capture selected run, runtime canvas action, subprocess action when available, screenshot, and console/network summary.

## Progression Gate

- SB24 may start after runtime incidents, telemetry, and action projections are stable.

## Suggested Agent Prompt

Execute SB23 from `codex/bundles/process-module-architecture-v3/subbundles/23-runtime-execution-view-runtime-canvas-step-operations-and-telemetry`. Rebuild active execution and runtime canvas over typed runtime projections.

## Handoff Notes For Next Bundle

Record incident, approval, dead-letter, and recovery projection fields needed by SB24.
