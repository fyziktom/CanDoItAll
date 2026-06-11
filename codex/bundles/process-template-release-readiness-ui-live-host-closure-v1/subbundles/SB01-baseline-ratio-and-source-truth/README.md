# SB01: Baseline and code-first closure

## Status
- Status: `Blocked`

## Objective
Establish the real current state after the previous bundle and prevent another closure that is mostly bundle/proof churn.

## Covered Inputs
- REQ-001: Reconcile current real code, execution report, and code-first ratio using an explicit start SHA.

## Prerequisites
- Bundle prepared-stage validator must pass after structural repair.
- Worktree state and explicit start SHA must be recorded before production code changes.

## Exact Source References
- repo://codex/bundles/process-template-ui-live-e2e-runtime-readiness-v1/reviews/01-execution-report.md
- repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeHostCodeFirstGuardTests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateExecutionE2ETests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateAutomationTestSupport.cs
- repo://tests/CanDoItAll.Tests.Integration/BusinessPlanProcessPostgresIntegrationTests.cs

## Deliverables
- Explicit start SHA in `reviews/01-execution-report.md`.
- Guard coverage that rejects stale or branch-derived ratio baselines.
- Guard coverage that separates manual-transition contract tests from representative automation E2E proof.
- Final ratio source scan contract used by SB08.

## Dependency Impact
- SB02 through SB08 depend on this baseline and classification.
- Any later bundle edits must stay visible to the SB08 ratio calculation.

## Validation Depth
- Integration guard tests for ratio and proof-classification behavior.
- Source scan transcript covering bundle/code changed-line grouping.
- Anti-stub audit for production `TODO`, `NotImplemented`, template-only output, and fixture-specific branching in touched paths.

## Implementation Steps
1. Capture explicit bundle start SHA before production code changes.
2. Re-run or update the code-first guard so final ratio uses this SHA.
3. Add a guard that manual-transition tests cannot be listed as representative E2E proof.
4. Add a source scan that final closure fails if `codex/bundles` changes dominate `src/tests`.

## Do Not Do
- Do not generate large proof trees.
- Do not change Process Core.

## Acceptance Checklist
- Explicit start SHA recorded in execution report.
- Guard tests reject branch-name or stale baseline ratio.
- Guard tests distinguish automation E2E from manual transition contract tests.
- `src + tests >= 5 * codex/bundles` is enforced at SB08.

## Proof Required
- `bundle://proof/SB01/manifest.md`
- `bundle://proof/SB01/semantic-invariants.md`
- Failing-first and passing transcript for the ratio/classification guard.
- Changed-file SHA-256 hashes and source assertion transcript.

## Browser Validation Logging
- No browser proof required for SB01; execution report should record `N/A` outside browser analytics.

## Progression Gate
- SB02 may start only after SB01 closure proof records the start SHA and confirms final ratio enforcement exists.

## Suggested Agent Prompt
Implement only the baseline/source-truth guard work for SB01, create artifact-backed proof, update the execution report, then run the closure gate before SB02.
