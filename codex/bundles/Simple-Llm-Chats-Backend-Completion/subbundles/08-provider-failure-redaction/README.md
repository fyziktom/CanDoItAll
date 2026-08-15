# SB08 — Provider Failure Redaction

## Status

- `Ready`

## Objective

- Keep provider streaming failure logs actionable while preventing raw provider/body/exception secrets from entering logs, public exceptions, or durable evidence.

## Success Criteria

- Streaming preparation and attempt failures log no exception object/message/inner exception.
- Captured logs exclude sentinel body, credential, endpoint, path, prompt, and system instruction values.
- Logs retain only allowlisted provider kind/safe ID/model/correlation/ordinal/failure kind/partial-output facts.
- Cancellation/retry/public exception semantics remain unchanged.

## Covered Inputs

- BC-070 through BC-072.

## Prerequisites

- SB07 `Pass`; SB05's typed consumer-abort ProviderRuntime contract is frozen and current.

## Exact Source References

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Llm.ProviderRuntime/ProviderBackedLlmStreamingInvocationAdapter.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Providers/Drivers/ProviderDriverProtocol.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProviderBackedLlmStreamingInvocationAdapterTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentFramework/Providers/ConcreteProviderDriverTests.cs`

## UI Composition Contract

- N/A — shared provider runtime logging only.

## Deliverables

- Allowlisted structured logging at both current warning sites without exception attachment/text.
- Capturing logger test support reused or minimally added in the test project.
- Redaction regression cases for preparation and post-dispatch failure, plus retry/cancellation preservation.

## Dependency Impact

- Completes CP2 before SB09; shared ProviderRuntime consumers require regression proof even though the defect was found through LLM Chats.

## Validation Depth

- Proof tier: `Behavioral`.
- Test solution: `repo://tests/Solutions/CanDoItAll.Tests.Unit.slnx`.
- Filter: exact new methods in `CanDoItAll.Tests.Unit.AgentFramework.ProviderBackedLlmStreamingInvocationAdapterTests` plus current retry/cancellation cases in that class.
- Selection reason: direct log capture must inspect structured state and public exception while preserving adapter state machine.
- Expected named cases: `Preparation_failure_logs_only_allowlisted_context`, `Provider_attempt_failure_logs_only_allowlisted_context`, `Raw_provider_body_exception_endpoint_credential_path_and_prompts_never_enter_logs`, `Redaction_preserves_retry_before_first_delta`, and `Redaction_preserves_cancellation_semantics` (5 cases).
- Invalidation keys: provider adapter logging/catch/classification, driver exception construction, logger test sink, retry/cancellation state machine.
- Broad-gate decision: deferred to SB10 because ProviderRuntime is shared by workflows and chats.

## Implementation Steps

1. Add a capturing structured logger and inject distinct secret sentinels through driver body, exception chain, endpoint, credential, prompt, and system prompt.
2. Prove current logs fail by containing/attaching raw exception data.
3. Replace exception logging with typed allowlisted fields; do not parse or regex-redact arbitrary exception text.
4. Assert public `LlmInvocationException` remains safe and retry/cancellation/delta behavior is unchanged.
5. Build ProviderRuntime and dependent LLM Chat Persistence if affected; list/run exact adapter cases.
6. Scan transcripts/proof artifacts for the sentinels before publishing proof.

## C# Architecture Impact

- No boundary change; hardens the existing provider-driver-to-runtime exception boundary.

## Boundary Ownership

- Driver may retain raw diagnostic exception internally; ProviderRuntime decides safe operational log/public classification.

## Dependency Direction

- No LLM Chat dependency enters ProviderRuntime/Providers.

## Pattern Decision

- PSR-09 allowlist, not arbitrary text redaction.

## Testability Contract

- Inspect logger event ID/level/template/structured values/exception property and thrown public exception; a string-only log assertion is insufficient.

## Partial Class Policy

- No partials and no new sanitizer interface for two local log sites.

## Architecture Proof Required

- Source assertion that neither warning call passes an exception or message, plus shared dependency direction check.

## Scope Exceptions

- This unit does not globally rewrite logging or provider driver error construction; it closes the shared streaming adapter exposure path.

## Do Not Do

- Do not log exception type/name if it can encode provider detail, silently drop the warning, or swallow cancellation.
- Do not include sentinel values verbatim in committed proof output; record hashes/placeholders after asserting absence.

## Acceptance Checklist

- [ ] Five named cases discover and pass.
- [ ] All captured log/public/durable outputs exclude sentinels.
- [ ] Retry/cancellation behavior unchanged.
- [ ] ProviderRuntime Release build passes.

## Proof Required

- Failing-first and passing structured-log summaries, exact discovery, sentinel hash/absence scan, safe log template fields, public exception assertions, and builds under `proof/SB08`.

## Browser Validation Logging

- N/A — no rendered UI.

## Progression Gate

- SB09 cannot start until SB08 passes and its changes are integrated with the main lane.

## Reopen Triggers

- Any later provider adapter/driver exception/log template/retry/cancellation change reopens SB08-SB10.

## Suggested Agent Prompt

```text
Execute SB08 only after SB07 with the SB05 ProviderRuntime contract current. Add sentinel-based structured-log tests, remove raw exception logging, preserve retry/cancellation, and stop if safe observability would require logging arbitrary exception text.
```
