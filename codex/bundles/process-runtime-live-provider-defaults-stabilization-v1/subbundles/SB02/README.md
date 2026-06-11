# SB02: Provider binding audit

## Status
- Status: Completed

## Objective
Provider binding audit

## Covered Inputs
- Original user stabilization request.
- Latest runtime-stable-live-blocked decision.
- Live provider model_not_found evidence for `5.4-mini`.

## Prerequisites
- SB01 closure gate must classify the current blocker and confirm no premature extraction.
- The exact source references must still exist in the repo.

## Exact Source References
- repo://tests/CanDoItAll.Tests.Integration/LiveProcessRunOpenAiSmokeIntegrationTests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateExecutionE2ETests.cs
- repo://tests/CanDoItAll.Tests.Integration/BusinessPlanProcessPostgresIntegrationTests.cs
- repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectScopedProcessLaunch.cs
- repo://src/CanDoItAll.Modules.Processes
- repo://src/CanDoItAll.Processes.Core
- repo://src/CanDoItAll.Processes.Contracts

## Scope

Audit live process-run provider path.

Acceptance:
- Prove live process-run smoke uses `IAgentFrameworkWorkspaceService`, managed provider profile, `ProviderProfileId`, and MAF/AgentFramework runtime.
- Add/repair tests or source scans that reject direct OpenAI/raw HTTP provider bypass in process live smoke and process runtime services.
- Confirm provider name/kind/transport/purpose/model are captured in live diagnostics.
- Confirm `OPENAI_API_KEY` is never printed.


## Dependency Impact
- SB03 and SB04 depend on this phase proving the managed provider/MAF path is still the live route.
- If direct OpenAI/raw HTTP bypass exists, live smoke proof and release classification must be blocked.

## Validation Depth
- Critical foundation.
- Require source-backed provider binding scans plus focused test or diagnostic proof where available.
- Require `proof/SB02/manifest.md` and `proof/SB02/semantic-invariants.md`.

## Implementation Steps
- Audit the live process-run smoke and process runtime services for provider routing.
- Confirm `ProviderProfileId`, `IAgentFrameworkWorkspaceService`, managed provider profile, and MAF/AgentFramework execution are used.
- Add or repair source scans/tests that reject direct OpenAI/raw HTTP bypass in the relevant process paths.
- Record provider diagnostic fields and secret-masking assertions.

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
- Source assertion artifact proving the managed provider path and absence of direct OpenAI/raw HTTP calls.
- Command transcript for provider-bypass scans.
- Semantic adequacy proof that rejects a shallow scan limited to one filename.
- Anti-stub audit transcript covering process live smoke and runtime services.

## Browser Validation Logging
- N/A for SB02 unless provider-path repair changes browser-visible behavior.
- If UI proof becomes necessary, use large desktop 1900x1200 and record route, assertions, and screenshot paths.

## Progression Gate
- Proceed to SB03 only after provider routing is proven to use managed CanDoItAll/MAF providers with no secret leakage.
- Reopen SB02 if live diagnostics or tests later show a direct-provider bypass.

## Suggested Agent Prompt
Implement SB02 as a stabilization task. Prefer real source/test fixes only when needed. Keep process runtime stable and do not begin runtime-core extraction.
