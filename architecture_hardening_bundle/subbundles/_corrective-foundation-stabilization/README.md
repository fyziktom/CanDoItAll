# Corrective playbook — foundation stabilization

## Status

- `Completed`
- `2026-04-13`: not triggered because Gate A passed without corrective work.

## Objective

- Repair any Gate A failure where canonical dependency meaning, validation purity, or compatibility boundaries are still unstable enough to make later refactors untrustworthy.

## Covered Inputs

- `BRQ-003` Canonical dependency model.
- `BRQ-005` Pure validation.
- `BRQ-015` Regression and proof discipline.
- `BRQ-016` Repeated architecture review gates.
- `BRQ-017` Corrective-first continuation.

## Prerequisites

- Gate A or an equivalent early proof step has failed.
- Subbundles `01-03` were the most recent implemented phases being reviewed.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDefinitionEnums.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDefinitionEntities.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDefinitionEntityConfigurations.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDefinitionEditorModels.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessCanvasBranching.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Support.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessesServiceIntegrationTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessWorkspaceTests.cs
- C:\repositories\CanDoItAll\architecture_hardening_bundle\reviews\01-execution-report.md
- C:\repositories\CanDoItAll\architecture_hardening_bundle\reviews\02-architecture-gate-memo-log.md

## Deliverables

- The smallest corrective implementation that restores canonical dependency meaning or validation purity.
- Repaired baseline or downstream tests that prove the corrected foundation.
- Updated execution-report and gate-memo entries documenting the corrective closure.

## Dependency Impact

- All later subbundles depend on Gate A being trustworthy.
- A weak correction here would invalidate the mutation, concurrency, and UI proof that follows.

## Validation Depth

- `Corrective critical foundation`

## Implementation Steps

1. Confirm which Gate A proof failed and capture the failing evidence.
2. Narrow the defect to canonical model drift, validation side effects, compatibility leakage, or weak baseline characterization.
3. Apply the smallest correction across the core definition and support surfaces.
4. Rerun the prepared-stage validator and focused baseline proof.
5. Rerun Gate A and update the gate memo before resuming downstream work.

## Do Not Do

- Do not widen scope into transaction or runtime refactors that belong to later phases.
- Do not keep a split dependency meaning just because tests still compile.
- Do not close this corrective path without re-establishing pure validation behavior.

## Acceptance Checklist

- The failing Gate A defect is corrected at the real ownership boundary.
- Canonical dependency meaning is explicit and no longer split across incompatible models.
- Validation no longer mutates state as part of the reviewed flow.
- Gate A is rerun and recorded with fresh evidence.

## Proof Required

- Prepared-stage validator rerun.
- Focused integration tests covering definition and validation behavior.
- Focused component tests covering workspace or canvas dependency behavior when affected.
- Updated `reviews/01-execution-report.md` and `reviews/02-architecture-gate-memo-log.md`.

## Browser Validation Logging

- If the correction changes workspace or canvas behavior, capture `/processes` browser proof with route, viewport, Playwright actions, screenshots, and review answers.
- If no UI surface changed, record `N/A` and rely on the non-UI proof listed above.

## Progression Gate

- Gate A passes with explicit evidence that the dependency model is canonical, validation is pure, and the repaired foundation is trustworthy for subbundle `05`.

## Suggested Agent Prompt

```text
Execute only the foundation-stabilization corrective subbundle for a failed Gate A. Repair the canonical model or validation purity defect, rerun the baseline proof and Gate A, and keep all downstream work blocked until the gate passes.
```
