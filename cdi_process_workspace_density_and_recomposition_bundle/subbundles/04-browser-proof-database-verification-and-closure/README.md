# Browser proof, database verification, and closure

## Status

- `Completed`

## Objective

- Consolidate the final proof that the workspace density, width usage, recomposition commands, persisted canvas layout, and managed SQLite application all worked together end to end.

## Covered Inputs

- `R10` Managed SQLite proof.
- `R11` Closure proof depth.
- All raw notes depend on this closure phase for trustworthy evidence.

## Prerequisites

- `subbundles/01-workspace-density-and-viewport-width-foundation` must be `Completed`.
- `subbundles/02-shared-canvaslib-recomposition-engine-and-menu-contract` must be `Completed`.
- `subbundles/03-process-canvas-integration-and-managed-sqlite-application` must be `Completed`.

## Exact Source References

- `C:\repositories\CanDoItAll\cdi_process_workspace_density_and_recomposition_bundle\reviews\01-execution-report.md`
- `C:\repositories\CanDoItAll\cdi_process_workspace_density_and_recomposition_bundle\traceability\01-requirement-traceability.md`
- `C:\Users\lucys\AppData\Local\CanDoItAll\control-plane\database-profiles\managed-sqlite\529c12060808489fad29feb5bc60dda1\db\candoitall.db`
- `C:\repositories\CanDoItAll\output\playwright`

## Deliverables

- Final browser evidence for density, toolbar menu, and recomposed process canvas.
- Database verification notes tied to the real managed SQLite workspace.
- Updated execution report with gate results, commands, screenshots, and residual risks.
- Final bundle-readiness and closure decision.

## Dependency Impact

- This is the closure phase. Weak proof here means the bundle cannot honestly be marked complete.

## Validation Depth

- `End-to-end regression and closure`

## Implementation Steps

1. Rerun all targeted automated tests that support the completed subbundles.
2. Capture the final `/processes` screenshots required by earlier subbundles.
3. Verify the managed SQLite database reflects the product-persisted recomposition result.
4. Update `reviews/01-execution-report.md` with commands, screenshots, gates, analytics, raw-note closure, and residual risks.
5. Run the final bundle validator and closure checks.

## Scope Exceptions

- None. This phase exists to close the remaining proof obligations.

## Do Not Do

- Do not skip the database verification because the browser looks correct.
- Do not mark raw notes complete without linking them to concrete proof.

## Acceptance Checklist

- The final screenshots show the denser workspace and the recomposed real process definition.
- The database verification confirms persisted coordinate changes for the exercised definition.
- The execution report is complete enough for a skeptical reader to audit the delivery.
- No critical proof gap remains open.

## Proof Required

- Final targeted test commands and outcomes.
- Final screenshot set referenced from `reviews/01-execution-report.md`.
- Database verification command and output summary.
- Final closure note stating whether any residual risk remains.

## Browser Validation Logging

- Route: `/processes`
- Viewports:
  - `1600x900`
  - constrained-height follow-up used in `subbundles/01`
- Required Playwright actions:
  - replay the final density checks
  - replay the recomposition-menu checks
  - capture final process-canvas screenshots
- Expected evidence paths:
  - `C:\repositories\CanDoItAll\output\playwright\process-workspace-density\*.png`
  - `C:\repositories\CanDoItAll\output\playwright\process-recomposition\*.png`
- Screenshot review questions:
  - Are the final visuals clearly better than the starting state?
  - Is any remaining overlap, dead width, or wasted height still visible?

## Progression Gate

- This is the terminal subbundle. The bundle may close only after all proof is recorded and no critical gap remains.

## Suggested Agent Prompt

```text
Implement this subbundle only. Do not add new feature scope. Re-run the targeted proof for the completed work, capture the final /processes screenshots, verify the managed SQLite persistence artifact, update the execution report completely, and only then make the closure recommendation.
```
