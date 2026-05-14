# 04-validation-and-closure

## Status

- `Completed`

## Objective

Run final validation, close raw notes, update bundle proof, and ensure implementation, tests, browser evidence, and bundle status agree.

## Covered Inputs

- `N001`, `N008`, and closure for all notes.
- Requirements: `R001`, `R015`, final proof for `R002` through `R014`

## Prerequisites

- SB01, SB02, and SB03 closure gates passed or are honestly blocked with follow-up work.

## Exact Source References

- `C:\repositories\CanDoItAll\codex\bundles\plugin-runtime-package-install\reviews\01-execution-report.md`
- `C:\repositories\CanDoItAll\codex\bundles\plugin-runtime-package-install\requirements\01-normalized-requirements.md`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\PluginsPageTests.cs`
- `C:\repositories\CanDoItAll\CanDoItAll.slnx`

## Deliverables

- Targeted build/test proof.
- Browser validation artifacts and analytics rows.
- Raw note closure table marked `Solved`, `Partially solved`, or `Not solved`.
- Completed-stage bundle validation.
- Final residual risk list.

## Dependency Impact

- This is the final closure phase.
- Any failed proof must reopen the owning subbundle.

## Validation Depth

- `End-to-end regression and closure`

## Implementation Steps

1. Run targeted build.
2. Run package, catalog, and component tests.
3. Run browser proof for `/plugins`.
4. Reopen original raw notes and close each note against proof.
5. Run completed-stage bundle validator.
6. Update root README and execution report.

## Scope Exceptions

- Full live Docker proof may remain opt-in if the existing environment does not set the live Docker proof flag. Existing non-live Docker/plugin workflow tests still need to pass.

## Do Not Do

- Do not mark raw notes solved without matching proof.
- Do not bury missing browser proof as residual risk if UI behavior is unvalidated.

## Acceptance Checklist

- Build passes or blocker is explicit.
- Targeted tests pass.
- Browser proof captured for `/plugins`.
- Bundle validators pass.
- Raw note closure complete.

## Proof Required

- Build command output.
- `dotnet test` targeted outputs.
- Browser screenshot paths.
- Prepared/completed validator outputs.

## Browser Validation Logging

- Route: `/plugins`
- Viewport: final desktop proof; narrower proof if SB03 changed responsive layout.
- Record screenshot path and visual review in execution report.

## Progression Gate

- Bundle can close only when all proof rows agree with the final raw-note closure.

## Suggested Agent Prompt

```text
Implement SB04 only. Run final build/tests/browser proof, update execution report and raw-note closure, run completed-stage bundle validation, and reopen any subbundle with weak evidence.
```
