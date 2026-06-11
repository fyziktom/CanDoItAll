# SB05: Live OpenAI template smoke

## Status
- Status: `Completed`

## Objective
Add a bounded live provider proof for a representative template path, or skip honestly.

## Covered Inputs
- REQ-005: Add or run bounded live OpenAI template process smoke with explicit model, token budget, timeout, and no secret leakage.

## Prerequisites
- SB04 must be completed or honestly blocked without being counted as UI proof.
- Live provider environment variables must be explicit when live proof is attempted.

## Exact Source References
- repo://tests/CanDoItAll.Tests.Integration/LiveProcessRunOpenAiSmokeIntegrationTests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateExecutionE2ETests.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch

## Deliverables
- Opt-in live template smoke using `ProcessesService`, launch plan, dispatch, finalizer, and artifact/readback path.
- Required environment-variable gate for live execution.
- Honest skipped classification when the live environment is absent.

## Dependency Impact
- SB06 and SB07 must not treat skipped live proof as a live pass.
- SB08 release decision must classify live proof explicitly.

## Validation Depth
- Live-smoke test command showing pass or skip with explicit reason.
- Source proof that API keys are not logged and token/time budgets are bounded.
- Negative proof that absent env variables skip and cannot be reported as live pass.

## Implementation Steps
1. Add an opt-in live template smoke using a very small representative process path.
2. Require `CANDOITALL_RUN_LIVE_PROCESS_TEMPLATE_VALIDATION=true`, `CANDOITALL_ENABLE_LIVE_OPENAI_SMOKE=true`, explicit model, explicit timeout, and explicit max tokens.
3. Use `ProcessesService` / launch plan / dispatch path, not workspace-only chat.
4. Verify process run id, step id, execution run, provider/model, usage, finalizer, and artifact/readback.
5. If env is absent, skip and classify as skipped; do not count as live proof.

## Do Not Do
- Do not log API key values.
- Do not claim live proof from skipped tests.
- Do not replace deterministic process-mock proof with live provider proof.

## Acceptance Checklist
- No API key value logged.
- Token/time budget bounded.
- Skipped live test cannot be reported as live pass.
- Deterministic process-mock tests remain primary CI proof.

## Proof Required
- `bundle://proof/SB05/manifest.md`
- `bundle://proof/SB05/semantic-invariants.md`
- Transcript for live-smoke pass or explicit skip.
- Source assertion transcript for env gates, model/token/timeout budget, and secret masking.

## Browser Validation Logging
- No browser proof required for SB05; execution report should record `N/A` outside browser analytics.

## Progression Gate
- SB06 may start only after live proof is either passed with explicit env settings or classified as skipped without being counted as live proof.

## Suggested Agent Prompt
Implement only the bounded live OpenAI smoke or skip classification for SB05, record durable proof, then run the closure gate before SB06.
