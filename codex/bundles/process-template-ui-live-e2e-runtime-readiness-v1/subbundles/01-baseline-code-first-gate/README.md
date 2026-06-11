# SB01: Baseline + code-first gate

## Status
- Status: Completed

## Objective
Establish the current source/test baseline after the previous bundle and prevent another bundle-heavy closure.

## Covered Inputs
- Raw request: review real code and tests after the previous bundle, not only the bundle report.
- REQ-001: re-check current source/test delta and prevent bundle-heavy closure.

## Prerequisites
- Prepared-stage bundle validator must pass after structural repair.
- Previous bundle report is available at `repo://codex/bundles/process-template-automation-e2e-multiteam-host-readiness-v1/reviews/01-execution-report.md`.

## Exact Source References
- repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeHostCodeFirstGuardTests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateExecutionE2ETests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateAutomationTestSupport.cs
- repo://codex/bundles/process-template-automation-e2e-multiteam-host-readiness-v1/reviews/01-execution-report.md

## Deliverables
- Refresh code-first guard so it checks final diff from the correct start SHA.
- Ensure `src + tests` are counted separately from docs and bundle/proof files.
- Add guard that any newly added long-running E2E proof must cite a production path, not only helper-level simulation.

## Dependency Impact
- SB02-SB08 cannot close if the final ratio proves bundle/proof files dominate source and test changes.
- Downstream proof manifests must cite this baseline when they claim code-first closure.

## Validation Depth
- Run focused guard tests and source scans.
- Capture `git diff --numstat` grouped by implementation, tests, bundle/proof, and docs.
- Include semantic adequacy proof, manifest, and anti-stub audit because this is a critical subbundle.

## Implementation Steps
- Identify the correct start SHA for this bundle and update the guard test if stale.
- Verify the guard separates `src`, `tests`, `codex/bundles`, docs, and proof artifacts.
- Add or strengthen the production-path citation rule for long-running E2E evidence.
- Record before/after hashes, transcripts, scans, and closure status under `proof/SB01/`.

## Do Not Do
- Do not add large proof trees.
- Do not count docs as implementation.
- Do not weaken the 5x source/test-to-bundle ratio.

## Acceptance Checklist
- Final `git diff --numstat` grouped totals are recorded.
- `src + tests >= 5 × codex/bundles` passes.
- Existing process-template automation E2E tests still pass.

## Proof Required
- Focused `ProcessRuntimeHostCodeFirstGuardTests`.
- Source scan for bundle-path coupling in `src` and `tests`.
- Anti-stub scan.

## Browser Validation Logging
- N/A: this subbundle has no browser-visible behavior.

## Progression Gate
- SB02 may proceed because focused guard tests, source assertions, bundle-path scan, anti-stub audit, and artifact-backed proof passed.
- Final SB08 ratio must use an explicit current bundle start SHA.

## Completed Proof
- Manifest: `bundle://proof/SB01/manifest.md`
- Semantic invariants: `bundle://proof/SB01/semantic-invariants.md`
- Focused test: `bundle://proof/SB01/transcripts/focused-test.txt`

## Suggested Agent Prompt
- Implement SB01 for the process-template UI/live E2E runtime readiness bundle. Keep the change code-first, update the guard tests, capture transcripts under `proof/SB01/`, and do not proceed to SB02 until the entry and closure gates pass.
