# SB02: Live OpenAI process-run smoke with explicit bounded env

## Status
- Current status: Completed

## Objective
Run the actual live OpenAI process-run smoke using the existing `OPENAI_API_KEY` and explicit bounded settings.

## Covered Inputs
- RN-002: If the process does not work, identify the broken refactoring and follow-up path.
- RN-003: Run a test with OpenAI using env and safe defaults.

## Prerequisites
- SB01 closure gate must classify release blockers.
- `OPENAI_API_KEY` presence may be checked, but the value must never be printed.

## Exact Source References
- `repo://tests/CanDoItAll.Tests.Integration/LiveProcessRunOpenAiSmokeIntegrationTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateExecutionE2ETests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateAutomationTestSupport.cs`

## Deliverables
- Live OpenAI smoke transcript with env presence redacted.
- Pass result or exact provider/config/process/finalizer/artifact/PostgreSQL/cleanup failure classification.
- Recorded model, timeout, and token cap.

## Dependency Impact
- SB03 can continue after SB02 passes or records a precise live-provider blocker.
- SB06 final decision depends on the SB02 classification and must not count skip as pass.

## Validation Depth
- Entry gate: confirm SB01 taxonomy exists and live smoke source references still match repo.
- Closure gate: transcript must prove the test ran, skipped, passed, or failed with exact classification.
- Semantic Adequacy Gate: record shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, and raw-note closure in `bundle://proof/SB02/semantic-invariants.md`.

## Implementation Steps
- Set required env vars for the command, not globally in repo config.
- Use model `5.4-mini`.
- Set timeout `180` and max tokens up to `100000`.
- Run the live test.
- If it fails, classify whether failure is provider config, API, process run start, dispatch/finalizer, artifact/readback, PostgreSQL, or cleanup.

## Scope Exceptions
- None planned. A skipped live test is a blocker or classified non-proof, not a pass.

## Do Not Do
- Do not extract dispatcher/process runtime core into a new package.
- Do not create execution-capable drivers.
- Do not add reflection discovery, fallback selector, or driver self-registration.
- Do not weaken Process Core genericity.
- Do not create proof-heavy churn.

## Acceptance Checklist
- Live test is run or failure is a real blocker, not skipped proof.
- API key value is never printed.
- Model, timeout and token cap are recorded.
- ProcessRunId/StepRunId, provider/model and usage observations are asserted when the live smoke passes.

## Proof Required
- Full live command transcript with redacted env classification.
- Test result.
- Failure classification if non-zero exit.
- `bundle://proof/SB02/manifest.md` with changed-file hashes and portable artifact references.
- `bundle://proof/SB02/semantic-invariants.md` with invariant IDs cited by transcripts.

## Browser Validation Logging
- N/A: SB02 has no browser-visible behavior.

## Progression Gate
- SB03 may start after live smoke passes or a precise live-provider blocker is recorded.

## Suggested Agent Prompt
- Run the live OpenAI smoke with `5.4-mini`, timeout `180`, and bounded max tokens up to `100000`. Never print the API key. Classify any failure exactly.
