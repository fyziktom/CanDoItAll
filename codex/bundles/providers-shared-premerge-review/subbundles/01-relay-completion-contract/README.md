# SB01 — Relay completion and failure contract

## Status

- State: `Ready`
- Proof tier: Behavioral
- Execution: not started; this file is a plan, not proof.

## Objective

Valid completed Responses succeed, and buffered/streamed failures remain safe and observable to the public client.

## Covered Inputs

- R01/R10; N01/N02/N04/N06; findings SP-01/SP-03/SP-04

## Prerequisites

- Reviewed baseline unchanged or diff reconciled; no other entry prerequisite.
- Read root constraints, analysis evidence and plan/02-validation-strategy.md before edits.

## Exact Source References

- `repo://src/Integration/CanDoItAll.SharedProviders.Http/SharedProviderHttpRelayClient.cs`
- `repo://src/Integration/CanDoItAll.SharedProviders.Http/SharedProviderSseRelayStream.cs`
- `repo://src/App/CanDoItAll.Web/Api/SharedProviderInferenceApi.cs`
- `repo://src/App/CanDoItAll.Web/Api/SharedProviderOpenAiServerSentEventWriter.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/SharedProviderOpenAiCompatibilityIntegrationTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/SharedProviderStreamingIntegrationTests.cs`

repo:// paths resolve from the product repository; bundle:// paths resolve from this bundle. Absolute SharedInfo references identify the inspected sibling checkout; resolve its actual root with the shared-standards skill when executing elsewhere. Planned new tests below are not claimed to exist.

## Deliverables

- Add realistic failing-first Responses envelope with error:null; retain non-null/failed-envelope rejection and model/usage rewriting.
- Map non-success status and Retry-After before optional bounded diagnostics; oversized/chunked bodies must not change 429/504 into 502.
- After headers, emit a sanitized operation-appropriate failure event or abort response. Never forward raw provider errors or fabricate a success terminal marker.
- Prove pinned OpenAI SDK consumer failure after partial text, failed/incomplete events, timeout, malformed SSE and disconnect; retain audit/finalizer/disposal semantics.

## Dependency Impact

- Critical foundation; unlocks SB05/SB06. Reopen both plus exported schemas if terminal semantics change.
- Reopen on changes to: response rewrite, SSE lifecycle/writer, failure mapping, public endpoint, pinned OpenAI package, associated tests.

## Validation Depth

- Proof tier: Behavioral.
- Test project/check selection: Integration selectors SharedProviderOpenAiCompatibilityIntegrationTests and SharedProviderStreamingIntegrationTests; Unit SharedProviderRelayPolicyTests.
- Selection reason: tests own the changed behavior and concrete regression; no unrelated suite substitutes for missing cases.
- Expected discovery: existing selected classes must be nonzero; enumerate and freeze their exact current FQNs/data-row counts before execution. The following exact named/scenario cases are required, with planned new-case counts where stated:
- BufferedResponses_NullError_Succeeds (1)
- BufferedResponses_NonNullError_Fails (1)
- BufferedResponses_UnsuccessfulOrInvalidStatus_IsNotSucceeded (failed/incomplete/missing/malformed status with error:null = 4)
- OversizedUpstreamError_PreservesStatusAndRetryAfter (429/504 × Content-Length/chunked = 4)
- StreamingFailure_IsObservedByPinnedSdk (Responses failed/incomplete = 2; timeout/malformed/disconnect × Chat/Responses = 6; total 8)
- Invalidation keys: response rewrite, SSE lifecycle/writer, failure mapping, public endpoint, pinned OpenAI package, associated tests.
- Broad-gate decision: No broad gate here; CP-MERGE-FROZEN in SB09 covers named shared-contract triggers.

## Acceptance Checklist

- [ ] Completed error:null envelope returns 200 with correct public model/usage; non-null errors remain failures.
- [ ] Oversized error diagnostics remain bounded, preserve category/Retry-After and disclose no raw upstream text.
- [ ] External SDK cannot accept a failed stream as successful; internal audit and external outcome agree.
- [ ] Caller disconnect, first-chunk flushing and once-only finalization still work.
- [ ] Keep strong identifiers/enums, explicit errors, safe logs, Egyptian braces and one statement per line.
- [ ] No production XML comments, unrelated refactor, silent fallback or inferred permission expansion.

## Proof Required

- Follow plan/02-validation-strategy.md for exact Release build/discovery/test command form; record commands, exit codes, expected/actual cases, source hashes and dependency mode.
- A test that checks only stream.Completion fails to establish client-visible failure; the pinned public SDK test must reject that shallow pass.
- Record realistic positive and adversarial negative proof, source producer/consumer/lifecycle assertions where applicable, and anti-stub review. Failing-first proof must exercise the reported defect.
- Record evidence in reviews/01-execution-report.md; separate governed manifests are not required for this unit.

## C# Architecture Impact

HTTP policy/SSE resource lifecycle remains in SharedProviders.Http; public response termination remains in Web. No new project. Keep existing adapter pattern.

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
- Critical foundation; unlocks SB05/SB06. Reopen both plus exported schemas if terminal semantics change.
- Scope beyond the listed repair, new wire support, database destruction, hosted authority or installed-path permission must be handled explicitly; finish all unaffected authorized work first.

## Non-goals

- No merge/push/deployment, paid upstream call, unrelated sibling refactor, invented remote history API or broad UI redesign.
