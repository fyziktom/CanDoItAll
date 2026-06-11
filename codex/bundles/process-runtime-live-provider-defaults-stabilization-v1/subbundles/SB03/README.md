# SB03: Managed provider default model policy

## Status
Prepared.

## Objective
Managed provider default model policy

## Covered Inputs
- Original user stabilization request.
- Latest runtime-stable-live-blocked decision.
- Live provider model_not_found evidence for `5.4-mini`.

## Prerequisites
Follow dependency map in `plan/01-phase-plan.md`.

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
Downstream subbundles cannot claim stabilization until this subbundle's classification/proof is complete.

## Validation Depth
Critical. Require source-backed tests or command transcripts, plus source scans where applicable.

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

## Browser Validation Logging
Use N/A unless this subbundle affects UI. For UI proof use large desktop 1900x1200 and record route, assertions, and screenshot paths.

## Progression Gate
Proceed only after source/test/build/browser/live evidence is honestly classified.

## Suggested Agent Prompt
Implement SB03 as a stabilization task. Prefer real source/test fixes only when needed. Keep process runtime stable and do not begin runtime-core extraction.
