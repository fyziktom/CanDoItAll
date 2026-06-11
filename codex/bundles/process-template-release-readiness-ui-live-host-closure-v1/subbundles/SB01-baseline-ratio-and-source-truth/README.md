# SB01: Baseline and code-first closure

## Objective
Establish the real current state after the previous bundle and prevent another closure that is mostly bundle/proof churn.

## Exact source references
- repo://codex/bundles/process-template-ui-live-e2e-runtime-readiness-v1/reviews/01-execution-report.md
- repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeHostCodeFirstGuardTests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateExecutionE2ETests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateAutomationTestSupport.cs
- repo://tests/CanDoItAll.Tests.Integration/BusinessPlanProcessPostgresIntegrationTests.cs

## Implementation steps
1. Capture explicit bundle start SHA before any code changes.
2. Re-run or update the code-first guard so final ratio uses this SHA.
3. Add a guard that manual-transition tests cannot be listed as representative E2E proof.
4. Add a source scan that final closure fails if `codex/bundles` changes dominate `src/tests`.

## Acceptance checklist
- Explicit start SHA recorded in execution report.
- Guard tests reject branch-name or stale baseline ratio.
- Guard tests distinguish automation E2E from manual transition contract tests.
- `src + tests >= 5 × codex/bundles` is enforced at SB08.

## Do not do
- Do not generate large proof trees.
- Do not change Process Core.
