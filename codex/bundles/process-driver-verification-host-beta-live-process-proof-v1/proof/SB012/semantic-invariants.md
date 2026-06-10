# SB012 Semantic Invariants

## Gate D Invariants
- Deterministic runtime proof must execute focused process runtime tests, not only inspect template catalog entries.
- The .NET safety net must cover a software-delivery process run with deterministic assignments, transitions, artifacts, branch outcomes, and blocked state assertions.
- The business-analysis safety net must cover a non-software process run with business artifacts, business role assignments, expected completed/skipped step statuses, and no software-template vocabulary bleed.
- Gate D must not claim live-provider behavior from deterministic tests.
- Gate D must not require `OPENAI_API_KEY`, `CANDOITALL_ENABLE_LIVE_OPENAI_SMOKE`, or `CANDOITALL_RUN_LIVE_PROCESS_RUN_VALIDATION`.
- Deterministic seed logic must create process runtime state through typed Process module services, not through report-only fixtures or bundle-local hardcoding.
- No transient `codex/bundles` path or current bundle name may be embedded in production or deterministic test code.
- P04 must not add generic object payload dispatch, fallback selector behavior, driver-host process mutation, or new Process Core dependencies on modules/infrastructure/drivers.

## Shallow-Pass Rejections
- Reject a proof package that cites only `/api/processes/templates` or baseline scenario catalog exposure.
- Reject a proof package that cites a skipped live OpenAI test as deterministic process proof.
- Reject a proof package that does not include focused `dotnet test` transcripts for both SB010 and SB011.
- Reject a proof package that does not assert source locations for `SeedBaselineAsync`, `EnsureScenarioRuntimeStateAsync`, and the two focused integration tests.
- Reject a proof package that marks the execution report passed without manifest, semantic invariants, anti-stub audit, and red-team rejection.

## Positive Proof Shape
- SB010 runs `ProcessesServiceIntegrationTests.SeedBaselineAsync_supports_global_then_project_scoped_baselines_without_slug_collisions`.
- SB010 asserts the software-delivery baseline run, QA accepted branch, blocked security review, required artifacts, conformance observations, and release approval input counts.
- SB011 runs `BusinessPlanProcessPostgresIntegrationTests.Business_plan_process_runs_with_business_artifacts_evidence_and_statuses`.
- SB011 asserts the business-plan run completes expected steps, skips the blocked-correction branch, records six business artifacts, verifies AI-agent business assignments, and reads managed business-plan artifact content.
- Gate D source audit confirms deterministic scenario tests do not use live OpenAI flags, do not embed bundle paths, and do not contain stub/report-only implementation markers.

## Gate Result
Gate D is semantically adequate for P04. The deterministic safety net passed for both required process scenarios and remains separated from live OpenAI proof.
