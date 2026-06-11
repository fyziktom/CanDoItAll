# Source Artifacts

## Preserved Inputs
- `bundle://inputs/00-original-request.md` preserves the original stabilization request.
- `repo://codex/bundles/process-runtime-stabilization-release-closure-v1/reviews/01-execution-report.md` is the latest prior execution evidence.
- `repo://codex/bundles/process-runtime-stabilization-release-closure-v1/reviews/02-release-decision.md` is the latest prior release decision.
- `repo://tests/CanDoItAll.Tests.Integration/LiveProcessRunOpenAiSmokeIntegrationTests.cs` owns the live OpenAI smoke.
- `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateExecutionE2ETests.cs` owns deterministic template automation.
- `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectScopedProcessLaunch.cs` owns large-screen UI process launch proof.

## User Correction
- Use model `5.4-mini` for the live OpenAI smoke instead of the prepared bundle's earlier `gpt-4.1-mini` wording.
- The live smoke token cap may be up to 10x larger than the original `10000` cap when needed for a real smoke run.
