# SB04: Live OpenAI process-run smoke rerun

## Status
Prepared.

## Objective
Live OpenAI process-run smoke rerun

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

Run a real live process-run smoke through managed provider default.

Acceptance:
- Set only necessary live enable flags and bounded timeout/token cap.
- Prefer managed provider default model unless a valid override is intentionally set.
- If using override, record why it was chosen.
- The smoke must go through `ProcessesService`, assignment, `IProcessRunAutomationDispatchService`, AgentFramework execution run, finalizer, and usage observations.
- Result must be classified as live-passed, provider-model-blocked, provider-auth-blocked, provider-quota-blocked, PostgreSQL-blocked, finalizer-blocked, artifact/readback-blocked, or runtime-failed.


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
Implement SB04 as a stabilization task. Prefer real source/test fixes only when needed. Keep process runtime stable and do not begin runtime-core extraction.
