# SB04: Live OpenAI process-run smoke rerun

## Status
- Status: Completed

## Objective
Live OpenAI process-run smoke rerun

## Covered Inputs
- Original user stabilization request.
- Latest runtime-stable-live-blocked decision.
- Live provider model_not_found evidence for `5.4-mini`.

## Prerequisites
- SB03 model-resolution policy tests must pass.
- SB02 must still prove the smoke uses the managed provider path, not direct OpenAI calls.

## Exact Source References
- repo://tests/CanDoItAll.Tests.Integration/LiveProcessRunOpenAiSmokeIntegrationTests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateExecutionE2ETests.cs
- repo://tests/CanDoItAll.Tests.Integration/BusinessPlanProcessPostgresIntegrationTests.cs
- repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectScopedProcessLaunch.cs
- repo://src/CanDoItAll.Modules.Processes
- repo://src/CanDoItAll.Processes.Core
- repo://src/CanDoItAll.Processes.Contracts

## Scope

Run a real live process-run smoke through managed provider default.

Acceptance:
- Set only necessary live enable flags and bounded timeout/token cap.
- Prefer managed provider default model unless a valid override is intentionally set.
- If using override, record why it was chosen.
- The smoke must go through `ProcessesService`, assignment, `IProcessRunAutomationDispatchService`, AgentFramework execution run, finalizer, and usage observations.
- Result must be classified as live-passed, provider-model-blocked, provider-auth-blocked, provider-quota-blocked, PostgreSQL-blocked, finalizer-blocked, artifact/readback-blocked, or runtime-failed.


## Dependency Impact
- SB05 and SB07 depend on this phase to distinguish live-provider status from deterministic runtime stability.
- If live proof is blocked, the blocker classification must be precise and carried into final decision.

## Validation Depth
- Critical foundation.
- Require a bounded live smoke attempt when credentials/configuration are available.
- Require `proof/SB04/manifest.md` and `proof/SB04/semantic-invariants.md`.

## Implementation Steps
- Run the live OpenAI process-run smoke with managed provider default model unless a valid explicit override is intentionally set.
- Keep timeout and token limits bounded.
- Capture provider id/name/kind/transport/purpose/model source and classification without secrets.
- Classify the result as live-passed, provider-model-blocked, provider-auth-blocked, provider-quota-blocked, PostgreSQL-blocked, finalizer-blocked, artifact/readback-blocked, or runtime-failed.

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
- Live smoke command transcript or explicit environment/configuration blocker transcript.
- Source assertion proving the smoke still travels through process dispatch, MAF, finalizer, and usage observation plumbing.
- Semantic adequacy proof that rejects skipped live tests as live proof.
- Anti-stub audit transcript for live-smoke diagnostics and classification strings.

## Browser Validation Logging
- N/A for SB04 unless live smoke repair changes browser-visible process behavior.
- If UI proof becomes necessary, use large desktop 1900x1200 and record route, assertions, and screenshot paths.

## Progression Gate
- Proceed to SB05 only after live proof passes or is precisely classified as a provider/configuration blocker rather than skipped.
- Reopen SB04 if later deterministic proof reveals the live blocker was masking a runtime failure.

## Suggested Agent Prompt
Implement SB04 as a stabilization task. Prefer real source/test fixes only when needed. Keep process runtime stable and do not begin runtime-core extraction.
