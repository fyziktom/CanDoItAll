# SB03: Managed provider default model policy

## Status
- Status: Completed

## Objective
Managed provider default model policy

## Covered Inputs
- Original user stabilization request.
- Latest runtime-stable-live-blocked decision.
- Live provider model_not_found evidence for `5.4-mini`.

## Prerequisites
- SB01 blocker taxonomy must be complete.
- SB02 provider binding audit must prove the managed provider path remains intact.

## Exact Source References
- repo://tests/CanDoItAll.Tests.Integration/LiveProcessRunOpenAiSmokeIntegrationTests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateExecutionE2ETests.cs
- repo://tests/CanDoItAll.Tests.Integration/BusinessPlanProcessPostgresIntegrationTests.cs
- repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectScopedProcessLaunch.cs
- repo://src/CanDoItAll.Modules.Processes
- repo://src/CanDoItAll.Processes.Core
- repo://src/CanDoItAll.Processes.Contracts

## Scope

Repair live smoke model selection.

Acceptance:
- `CANDOITALL_LIVE_PROCESS_RUN_OPENAI_MODEL` becomes an optional explicit override, not mandatory.
- If the override is absent, use the managed OpenAI provider's configured `DefaultModel`.
- If `DefaultModel` is empty, use a provider `SuggestedModels` fallback.
- If no provider model exists, fail with `provider-default-missing`.
- If an explicit override is rejected by provider, classify as `provider-model-override-invalid`.
- Add focused tests for model-resolution policy.


## Dependency Impact
- SB04 depends on this phase to choose the live model correctly.
- SB05 through SB07 depend on this phase to separate runtime failures from provider/model configuration failures.

## Validation Depth
- Critical foundation.
- Require focused tests for explicit override, managed default, suggested model fallback, and missing default failure.
- Require `proof/SB03/manifest.md` and `proof/SB03/semantic-invariants.md`.

## Implementation Steps
- Find the live OpenAI process-run smoke model-selection code.
- Extract the smallest strongly typed model-resolution policy needed by tests.
- Preserve explicit env override behavior while making it optional.
- Add focused policy tests and diagnostic assertions without printing secrets.

## Do Not Do
- Do not extract dispatcher/runtime core into a new library.
- Do not add execution-capable drivers.
- Do not add fallback provider/driver selectors.
- Do not bypass managed providers with raw OpenAI calls.
- Do not count skipped live tests as live proof.
- Do not leak secrets.

## Acceptance Checklist
- Functional behavior is verified or blocker is precisely classified.
- No Process Core leakage.
- No hidden runtime extraction.
- No direct provider bypass.
- Proof is concise and source-backed.

## Proof Required
- Failing-first or adversarial test transcript for missing/invalid shallow model policy.
- Passing focused test transcript for override/default/suggested/missing model behavior.
- Changed-file hash artifact for all edited source and test files.
- Anti-stub audit transcript covering production and test code changed by this phase.

## Browser Validation Logging
- N/A for SB03 because the planned work is backend/test policy only.
- If implementation unexpectedly changes UI, add large desktop 1900x1200 proof before closure.

## Progression Gate
- Proceed to SB04 only after model-resolution tests pass and diagnostics distinguish default, suggested fallback, explicit override, and missing provider default.
- Reopen SB03 if live smoke proves the selected model source is ambiguous or misclassified.

## Suggested Agent Prompt
Implement SB03 as a stabilization task. Prefer real source/test fixes only when needed. Keep process runtime stable and do not begin runtime-core extraction.
