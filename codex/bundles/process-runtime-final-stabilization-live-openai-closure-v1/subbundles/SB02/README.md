# SB02: Live OpenAI process-run smoke with explicit bounded env

## Status
Prepared.

## Objective
Run the actual live OpenAI process-run smoke using the existing `OPENAI_API_KEY` and explicit bounded settings.

## Exact Source References
- `repo://tests/CanDoItAll.Tests.Integration/LiveProcessRunOpenAiSmokeIntegrationTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateExecutionE2ETests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateAutomationTestSupport.cs`

## Implementation Steps
- Set required env vars for the command, not globally in repo config.
- Use model `gpt-4.1-mini` unless local repo/provider config requires another explicit model.
- Set timeout `180` and max tokens `10000`.
- Run the live test.
- If it fails, classify whether failure is provider config, API, process run start, dispatch/finalizer, artifact/readback, PostgreSQL, or cleanup.

## Do Not Do
- Do not extract dispatcher/process runtime core into a new package.
- Do not create execution-capable drivers.
- Do not add reflection discovery, fallback selector, or driver self-registration.
- Do not weaken Process Core genericity.
- Do not create proof-heavy churn.

## Acceptance Checklist
- Live test is run or failure is a real blocker, not skipped.
- API key value is never printed.
- Model, timeout and token cap are recorded.
- ProcessRunId/StepRunId, provider/model and usage observations are asserted.

## Proof Required
- Full live command transcript with redacted env classification.
- Test result.
- Failure classification if non-zero exit.

## Browser Validation Logging
N/A.

## Progression Gate
SB03 may start after live smoke passes or a precise live-provider blocker is recorded.
