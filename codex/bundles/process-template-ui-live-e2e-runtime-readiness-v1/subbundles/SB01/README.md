# SB01: Baseline + code-first gate

## Status
Prepared.

## Objective
Establish the current source/test baseline after the previous bundle and prevent another bundle-heavy closure.

## Exact Source References
- repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeHostCodeFirstGuardTests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateExecutionE2ETests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateAutomationTestSupport.cs
- repo://codex/bundles/process-template-automation-e2e-multiteam-host-readiness-v1/reviews/01-execution-report.md

## Deliverables
- Refresh code-first guard so it checks final diff from the correct start SHA.
- Ensure `src + tests` are counted separately from docs and bundle/proof files.
- Add guard that any newly added long-running E2E proof must cite a production path, not only helper-level simulation.

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
N/A.

## Progression Gate
Downstream work is blocked if source/test ratio cannot be preserved.
