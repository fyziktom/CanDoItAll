# SB00 Incident And Template Regression Baseline

## Status

- `Completed`

## Objective

Create failing-first characterization coverage for the Tetris QA escalation and the broader accepted/repair validation branch pattern before production behavior changes.

## Covered Inputs

- GPTPro incident reconstruction and root causes.
- User requirement to treat Tetris as one example, not the entire scope.
- Test strategy from `bundle://06-test-strategy.md`.

## Prerequisites

- Bundle prepared-stage validation passes.
- Current unit test project can be restored or a validation blocker is recorded.
- No production behavior changes are made before failing-first or skipped characterization tests exist.

## Exact Source References

- `bundle://01-incident-reconstruction.md`
- `bundle://02-root-causes.md`
- `bundle://06-test-strategy.md`
- `bundle://codex-tasks/01-incident-regression-fixture.md`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeIntegrationAdapterTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRequiredRuntimeToolNamesTests.cs`

## Deliverables

- Incident fixture tests for accepted branch plus deterministic scaffold/content defect.
- Repair branch fixture tests proving acceptance-only browser receipts are skipped only when concrete defect evidence exists.
- Negative fixture proving repair branch without defect evidence is not accepted.
- Retry-budget fixture proving branch-routable defects do not consume same-step retry.
- Template-surface characterization table for every accepted/repair validation template found in `inventories/01-process-template-inventory.md`.

## Dependency Impact

- Unlocks SB01 through SB04 by freezing intended behavior before extraction and routing changes.
- Weak characterization invalidates all later proof because passing tests could reflect new assumptions rather than the incident.

## Validation Depth

- Critical foundation.
- Requires failing-first transcript or explicitly skipped failing-first tests with exact reason before implementation.
- Requires semantic positive and adversarial negative proof after SB04 completes.

## Implementation Steps

1. Add synthetic process assignment/output/receipt fixture matching the Tetris QA attempts without LLM calls.
2. Model `quality-accepted` with full browser/runtime receipts and scaffold content still present.
3. Model `repair-required` with deterministic content defect and missing acceptance-only browser proof.
4. Model `repair-required` without concrete defect evidence.
5. Add a retry-budget assertion around branch-routable defects.
6. Add template-surface characterization tests or data rows for templates listed in `inventories/01-process-template-inventory.md`.
7. Mark tests failing/skipped only where later subbundles intentionally change behavior, and cite the intended unblock subbundle.

## C# Architecture Impact

This subbundle must not refactor production architecture. It defines regression boundaries that later architecture work must satisfy.

## Boundary Ownership

- Tests may use software-delivery branch names as fixture data.
- Production generic runtime/application code must remain unchanged in this subbundle.

## Dependency Direction

- Unit tests can reference Modules.Processes and process projects as existing tests already do.
- No new production project reference is allowed.

## Pattern Decision

- Characterization fixture, not a new design pattern.
- Use builders/helpers only if they reduce repeated fixture setup in tests.

## Testability Contract

- Fixture must not call an LLM.
- Fixture must not require a real browser or actual generated Tetris app.
- Receipt records and content checks should be synthetic and deterministic.

## Partial Class Policy

- No production partial class edits.
- Test helper partials are allowed only if existing test style uses them and they remain cohesive.

## Architecture Proof Required

- Source assertion that production files are not changed except test-only additions.
- Test transcript showing failing-first or intentionally skipped target tests.

## Do Not Do

- Do not implement branch routing yet.
- Do not change templates to make tests pass in this phase.
- Do not use Tetris terms in production code.

## Acceptance Checklist

- Incident fixture names the original run and step ids in comments or test data.
- Tests cover accepted+defect, repair+defect, repair+no-defect, and retry-budget cases.
- Similar templates are represented in the characterization table.
- Existing legacy tests still run or the blocker is recorded.

## Proof Required

- `bundle://proof/SB00/manifest.md` after execution.
- `bundle://proof/SB00/semantic-invariants.md` after execution.
- Failing-first transcript for new behavior tests.
- Source assertion transcript proving production runtime files were not changed in SB00.
- Anti-stub audit transcript for new tests.

## Browser Validation Logging

- N/A for SB00; fixture is unit-level and browser-free.

## Progression Gate

- SB01 may start only after SB00 records failing-first characterization or a justified skipped-test blocker for each incident behavior.

## Suggested Agent Prompt

Implement SB00 only. Add deterministic unit characterization for the Tetris branch/receipt/content failure combination and the broader template branch pattern. Do not change production behavior.
