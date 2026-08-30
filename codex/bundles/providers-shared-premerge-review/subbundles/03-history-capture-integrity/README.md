# SB03 — History capture redaction and outcome integrity

## Status

- State: `Ready`
- Proof tier: Behavioral
- Execution: not started; this file is a plan, not proof.

## Objective

Detailed capture redacts its documented credential syntax, and recorded outcomes distinguish explicit timeout evidence from caller cancellation.

## Covered Inputs

- R03/R10; N03/N04/N06; H01/H02

## Prerequisites

- Source baseline reconciled. Preserve existing application-visible attempt/canonical-owner semantics.
- Read root constraints, analysis evidence and plan/02-validation-strategy.md before edits.

## Exact Source References

- `repo://src/MAF/ProviderHistory/CanDoItAll.AgentFramework.ProviderHistory.Application/HistoryTextCapture.cs`
- `repo://src/MAF/ProviderHistory/CanDoItAll.AgentFramework.ProviderHistory.Persistence/HistoryTextProtector.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/History/ProviderHistoryObservation.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Providers/MafProviderTransportBoundaryChatClient.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/History/ProviderHistoryChatClient.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/History/HistoryStreamingChatDriver.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProviderHistoryLifecycleTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/ProviderHistoryCaptureIntegrationTests.cs`

repo:// paths resolve from the product repository; bundle:// paths resolve from this bundle. Absolute SharedInfo references identify the inspected sibling checkout; resolve its actual root with the shared-standards skill when executing elsewhere. Planned new tests below are not claimed to exist.

## Deliverables

- Add quoted password/api_key/client_secret/authorization fixtures for request and response, including escaping and boundary truncation; keep finite regex work, known-secret snapshot and Unicode limits.
- Repair allowlisted-key parsing/redaction before encryption without promising universal DLP; never print real retained content or credentials.
- Classify explicit HttpClient timeout/deadline causes separately from supplied caller-token cancellation, generic failure and other independent cancellation. Preserve already-observed terminal usage/success.
- Review existing Detailed retention implications; document an explicit operator cleanup option for previously captured sensitive syntax, without automatically deleting user history.

## Dependency Impact

- Critical privacy/history foundation; unlocks final docs and capture host proof. Schema changes, if any, reopen SB06/SB08.
- Reopen on changes to: redaction regex/parser, known-secret freeze, outcome classifier, capture modes, runtime decorators, timeout semantics.

## Validation Depth

- Proof tier: Behavioral.
- Test project/check selection: Unit ProviderHistoryLifecycleTests and ProviderHistoryCaptureTests; Integration ProviderHistoryCaptureIntegrationTests.
- Selection reason: tests own the changed behavior and concrete regression; no unrelated suite substitutes for missing cases.
- Expected discovery: existing selected classes must be nonzero; enumerate and freeze their exact current FQNs/data-row counts before execution. The following exact named/scenario cases are required, with planned new-case counts where stated:
- QuotedCredentialKey_IsRedacted (password/api_key/client_secret/authorization = 4)
- QuotedCredentialAtCaptureBoundary_DoesNotLeak (1)
- ExplicitTimeout_IsRecordedAsTimedOut (buffered/streaming = 2)
- KnownSdkTimeoutWrapper_IsRecordedAsTimedOut (1)
- HttpClientDeadline_WithActiveCallerToken_ProducesRecognizedTimeout (1 fast fake-handler integration)
- CallerCancellation_IsRecordedAsCancelled (1)
- IndependentCancellationWithoutTimeoutEvidence_IsNotTimedOut (active caller token, no timeout cause/deadline = 1)
- LateCancellation_PreservesObservedTerminalEvidence (1)
- Invalidation keys: redaction regex/parser, known-secret freeze, outcome classifier, capture modes, runtime decorators, timeout semantics.
- Broad-gate decision: Focused capture tests only here; final shared runtime checkpoint SB09.

## Acceptance Checklist

- [ ] Quoted documented keys and known configured secrets are absent from decrypted synthetic persisted details; Light mode stays body-free.
- [ ] Explicit timeout is TimedOut, cancelled caller remains Cancelled, ordinary failure remains Failed, late cancellation does not erase success/usage.
- [ ] No automatic provider replay on history-finalization failure; canonical content remains linked without duplicate body.
- [ ] Keep strong identifiers/enums, explicit errors, safe logs, Egyptian braces and one statement per line.
- [ ] No production XML comments, unrelated refactor, silent fallback or inferred permission expansion.

## Proof Required

- Follow plan/02-validation-strategy.md for exact Release build/discovery/test command form; record commands, exit codes, expected/actual cases, source hashes and dependency mode.
- Checking only flags or encrypting an unredacted string is not proof. Decrypt synthetic capture and assert forbidden fixture bytes absent; use explicit timeout cause with active caller token.
- Record realistic positive and adversarial negative proof, source producer/consumer/lifecycle assertions where applicable, and anti-stub review. Failing-first proof must exercise the reported defect.
- Record evidence in reviews/01-execution-report.md; separate governed manifests are not required for this unit.

## C# Architecture Impact

Pure capture rules in History.Application; crypto/persistence in History.Persistence; runtime exception/terminal observation in owning MAF decorators. Any helper is a cohesive top-level type, not a generic fallback.

## Boundary Ownership

- Keep the responsibility in the named current owner. Any extraction must be independently testable and remove moved logic from the old class.

## Dependency Direction

- Preserve architecture/02-csharp-dependency-direction.md; no new project/reference is assumed. If needed, stop that edit and amend the boundary/checkpoint before proceeding.

## Pattern Decision

- Follow architecture/03-csharp-pattern-selection-records.md. Prefer current adapters/decorators and small functions; avoid abstractions without a concrete boundary.

## Testability Contract

- Pure policies use direct isolated tests; persistence/network behavior uses the selected integration seam and a real production consumer. Do not construct the full runtime for a pure rule.

## Partial Class Policy

- No new runtime partial. Existing generated code and cohesive UI code-behind are allowed; no nested service used to hide responsibility.

## Architecture Proof Required

- Relevant checkpoint: plan/architecture-checkpoints.md. Review .csproj diff, policy placement, production registration, independent tests and no-new-partial proof.
- If behavior is extracted, show old-owner shrink/thin facade and a negative test rejecting delegation back to the monolith. No extraction is required solely for this metric.

## Progression Gate

- Pass only after acceptance and required proof agree; otherwise record precise failed/blocked cases.
- Critical privacy/history foundation; unlocks final docs and capture host proof. Schema changes, if any, reopen SB06/SB08.
- Scope beyond the listed repair, new wire support, database destruction, hosted authority or installed-path permission must be handled explicitly; finish all unaffected authorized work first.

## Non-goals

- No merge/push/deployment, paid upstream call, unrelated sibling refactor, invented remote history API or broad UI redesign.
