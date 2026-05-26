# SB13: Expose runtime invariant violations and actionable diagnostics.

## Objective

Expose runtime invariant violations and actionable diagnostics.

## Why This Matters

This subbundle closes a concrete runtime correctness gap observed after phase5. The process runtime must avoid both false completion and unnecessary blocking while staying generic.

## Implementation Tasks

- Add view models and service methods for alias conflicts, weak artifact records, blocked recovery state, duplicate lineage identity, and manual transition validation failures.
- Display process health and recommended action.
- Ensure UI remains generic across process types.
- Add component tests if UI changes.
- Add journal events for invariant audit results.

## Required Tests

- Add failing-first or red-team tests before the production fix where practical.
- Add positive tests proving the fixed behavior.
- Include at least one generic/non-software case if this subbundle changes generic process semantics.

## Closure Criteria

- Production code implements the behavior; no prompt-only fix.
- Proof manifest is updated.
- Focused tests pass.
- No SQLite runtime/migration dependency is introduced.

## Status

- Completed

## Covered Inputs

- RQ10 executable recovery router and process health invariant audit.
- RN06 infer wrong block/recovery classification from broad reason text.

## Prerequisites

- SB12 closure gate passes.
- SB10/SB11 recovery and health services remain trusted.

## Exact Source References

- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.cs
- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.Support.cs
- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs
- repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunsOperatorConsoleSection.razor
- repo://tests/CanDoItAll.Tests.Components/ProcessWorkspaceTests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeOperatorReadModelTests.cs

## Deliverables

- View models and service methods for alias conflicts, weak artifact records, blocked recovery state, duplicate lineage identity, and manual transition validation failures.
- Generic process health display and recommended action.
- Journal events for invariant audit results.
- Component tests if rendered UI changes.

## Dependency Impact

- SB14 final red-team closure uses diagnostics to prove runtime invariants are observable and actionable.

## Validation Depth

- Service/read-model tests for invariant diagnostics.
- Component tests or Playwright proof if UI rendering changes.
- Source assertion for journal event emission.

## Implementation Steps

- Add invariant diagnostics to runtime/read-model service boundaries.
- Surface recommended action without process-domain-specific wording.
- Add UI/component updates only if needed by the existing process workspace patterns.
- Record journal events for invariant audit results.
- Record proof under `bundle://proof/SB13/`.

## Do Not Do

- Do not add a marketing-style dashboard or domain-specific process copy.
- Do not make diagnostics display-only if they are not backed by runtime state.
- Do not skip browser/component proof if rendered UI changes.

## Acceptance Checklist

- Runtime invariant issues are visible through service/read-model outputs.
- Recommended recovery action is generic and actionable.
- Journal events are emitted for manual transition validation failures.
- Focused integration tests, component UI proof, and regression filters pass.

## Proof Required

- `bundle://proof/SB13/manifest.md`
- `bundle://proof/SB13/semantic-invariants.md`
- Failing-first or red-team transcript.
- Passing focused test transcript.
- Changed-file SHA-256 transcript.
- Anti-stub audit transcript.
- Browser/component evidence if UI rendering changes.

## Browser Validation Logging

- If UI changes: record route, viewport, Playwright MCP actions, screenshot paths, and readability/layout result in `bundle://reviews/01-execution-report.md`.
- If no UI changes: record N/A with service-level evidence.

## Progression Gate

- SB14 may start only after health invariant diagnostics are observable and actionable.

## Suggested Agent Prompt

- Implement SB13 diagnostics with existing component patterns, update `proof/SB13`, run service/component tests and browser proof if applicable, and record gate closure.
