# Side-effect-free validation and editor-normalization split

## Status

- `Completed`
- `2026-04-13`: validation no longer performs hidden normalization, save/workspace normalization now flows through explicit entry points, and targeted purity/idempotence plus regression tests passed on the live repository.

## Objective

- Make validation pure and move normalization into explicit, intentionally named entry points so correctness checks no longer mutate the thing being validated.

## Covered Inputs

- `U003` Stabilization, testability, and canonicality concerns.
- `BRQ-005` Pure validation.
- `F002` Validation mutates state.

## Prerequisites

- `02-canonical-dependency-model-and-compatibility-boundary` passed.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessCanvasBranching.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Support.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Persistence.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.Canvas.Persistence.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.DefinitionCrud.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessCanvasSurfaceFactory.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessWorkspaceTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessesServiceIntegrationTests.cs

## Deliverables

- Validation helpers that are side-effect free.
- Explicit normalization entry points with intentional names and clear call sites.
- Tests proving validation does not mutate state and normalization is idempotent.

## Dependency Impact

- Subbundles 05-10 depend on pure validation so they can reason about failure modes and transaction boundaries safely.
- If validation still mutates state, later persistence and concurrency fixes can mask defects.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Separate normalization from validation in the helper API and make the naming reflect the difference clearly.
2. Update save and UI entry points to call normalization intentionally where needed before validation or persistence.
3. Remove hidden normalization from validation paths.
4. Add focused tests proving validation does not change the editor model and normalization remains idempotent.

## Scope Exceptions

- This phase does not yet change transaction boundaries or graph persistence.
- This phase may introduce a small shared helper or state copier only if required to prove validation purity.

## Do Not Do

- Do not keep mutating behavior inside validation because it is convenient for callers.
- Do not scatter normalization calls blindly across many UI events.
- Do not change user-facing behavior beyond making the call flow explicit and stable.

## Acceptance Checklist

- Validation methods no longer mutate state.
- Normalization is called explicitly at clear entry points.
- Tests prove validation purity and normalization idempotence.
- No hidden dependency-sync side effects remain in validation.

## Proof Required

- Focused tests for validation purity.
- Regression tests for save and editor flows proving behavior still works after the split.
- Execution-report note listing the intentional normalization entry points.

## Browser Validation Logging

- N/A unless entry-point changes require a visible workspace proof during execution.
- If visible behavior changes, record the minimal browser proof but defer full UI closure to subbundle 13 or 16.

## Progression Gate

- Validation is pure, normalization is explicit and idempotent, and later persistence/concurrency work no longer depends on hidden helper side effects.

## Suggested Agent Prompt

```text
Implement only subbundle 03. Split validation from normalization so validation becomes side-effect free. Make normalization explicit at the correct entry points, add tests proving purity and idempotence, and stop before transaction or persistence refactors.
```
