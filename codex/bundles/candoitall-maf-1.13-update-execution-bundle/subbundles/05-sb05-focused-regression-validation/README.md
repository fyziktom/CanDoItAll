# SB05 Focused Regression Validation

## Status

Ready after `SB04`.

## Objective

Prove that current app behavior still works after the package update, with focused tests first and broader validation second.

## Covered Inputs

- `bundle://inputs/original-prep/docs/05-validation-and-regression-plan.md`
- `bundle://architecture/04-csharp-testability-plan.md`
- `bundle://reviews/01-execution-report.md`

## Prerequisites

- `SB04` architecture checkpoint passed.
- Build succeeds or remaining blocker is accepted by user.
- Focused test list has been refreshed from current source.

## Exact Source References

- `repo://tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj`
- `repo://tests/Components/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj`
- `repo://tests/Playwright/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj`
- `bundle://architecture/04-csharp-testability-plan.md`

## Deliverables

- Focused unit test transcripts.
- Focused integration test transcripts.
- Broad unit/integration/component test transcripts or explicit skip reasons.
- Optional Playwright/service smoke transcripts or explicit environment skip reasons.
- Replacement-test mapping when exact names differ.

## Dependency Impact

- `SB06` depends on this proof to make evidence and merge-readiness claims.

## Validation Depth

- Focused tests are mandatory unless unavailable and replaced with current equivalents.
- Broad tests are recommended; skips require exact reason.
- Browser/host proof is conditional and must be honest.

## Implementation Steps

1. Run focused unit tests for MAF runtime, provider gates, finalizers, tool composition, workflow adapter, and process dispatch.
2. Run focused integration tests for AgentFramework execution, process, and project-structure bridge.
3. Run broad unit, integration, and component tests if feasible.
4. Run Playwright smoke if environment is ready.
5. Record replacements and skip reasons.
6. Update execution report gate rows and analytics.

## Scope Exceptions

- External services such as PostgreSQL, Qdrant, browsers, or provider credentials may block optional smokes; record exact reason instead of marking pass.
- Do not fix unrelated failing tests in this subbundle unless they block package-update proof and are caused by the update.

## Do Not Do

- Do not weaken tests to pass.
- Do not remove governance assertions.
- Do not treat skipped tests as passed.
- Do not manually seed production-only state for positive proof unless the test is explicitly a migration/backfill/validator fixture.

## Acceptance Checklist

- Focused test results are recorded.
- Replacement tests preserve validation intent.
- Broad tests are run or skipped with exact reason.
- Any failures are classified as package-induced, pre-existing, or blocked.
- Browser validation analytics are updated if browser proof ran.

## Proof Required

- Test transcripts under `proof/SB05/transcripts/`.
- Replacement-test mapping.
- Browser proof artifacts if applicable.
- Source assertions for behavior surfaces validated.
- Anti-stub audit transcript.

## Browser Validation Logging

- Route, viewport, actions, assertions, screenshot path, and result must be recorded if Playwright runs.

## Progression Gate

- `SB06` starts only when focused validation is meaningful and unresolved failures are documented as blockers or pre-existing issues.

## C# Architecture Impact

- Validates that architecture-preserving fixes still work behaviorally.

## Boundary Ownership

- Tests should exercise behavior through public/application seams, not by forcing product behavior into MAF implementation.

## Dependency Direction

- Test failures must not be solved by adding forbidden references.

## Pattern Decision

- No new production pattern should be introduced in validation.

## Testability Contract

- Tests must prove behavior, not only object construction or non-empty output.

## Partial Class Policy

- No partial class changes allowed during validation.

## Architecture Proof Required

- Test evidence must support architecture gate claims from `SB04`.

## Suggested Agent Prompt

Execute `SB05` only. Run focused regression validation, broaden where feasible, record exact transcripts and skip reasons, and update browser analytics if browser proof is used. Do not change production code unless a package-induced regression is proven; if that happens, reopen earlier subbundles.
