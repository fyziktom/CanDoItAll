# SB07 — Lightweight LLM attempt usage accounting

        **Depends on:** SB06  
        **Required before merge:** Yes

        ## Goal

        Preserve usage from every provider attempt made by the bounded empty-response retry.

        ## Required work

        1. Add non-negative overflow-safe LlmUsage aggregation.
2. Accumulate usage before evaluating whether response text is usable.
3. Return aggregate usage when retry succeeds.
4. Attach known aggregate usage to typed EmptyResponse, ProviderFailure, and DeadlineExceeded failures.
5. Project typed failure usage into workflow failure observations.
6. Keep public messages sanitized and do not add provider-error retries.

        ## Primary files

        - `src/MAF/Common/CanDoItAll.AgentFramework.Llm.Abstractions/LlmInvocationContracts.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Llm.ProviderRuntime/ProviderBackedLlmInvocationAdapter.cs`
- `src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Runtime/WorkflowLlmComponentInvoker.cs`
- `tests/Unit/CanDoItAll.Tests.Unit/ProviderBackedLlmInvocationAdapterTests.cs`

        ## Acceptance

        - [x] Empty then success returns both attempts' usage.
- [x] Two empty attempts fail with both attempts' usage.
- [x] Empty then provider/deadline failure preserves known prior usage.
- [x] Negative counters fail at the owning boundary.
- [x] Workflow failure analytics no longer fabricate zero when usage is known.

        ## Proof requirements

        Create `proof/proof-manifest.json` and `SESSION-HANDOFF.md`. Record starting/ending SHA, changed
        files, commands, exit codes, test counts, architecture checks, bugs found, deviations, residual
        risk, and whether the next subbundle is unlocked.

## Execution contract

- **Owned finding:** MRG-009.
- **Proof tier:** Behavioral.
- **Progression gate:** SB08 unlocks only after success and typed failure usage cover every reported attempt and workflow projection preserves known usage.
- **Reopen trigger:** A reported attempt is discarded, arithmetic accepts negative/overflowing counters, cancellation is remapped, or a public error leaks provider detail.

## C# Architecture Impact

Extend the provider-neutral usage value and typed failure contract without coupling abstractions to workflow/provider SDKs.

## Boundary Ownership

Llm.Abstractions owns usage invariants; ProviderRuntime owns attempt accumulation; Workflows Runtime owns failure observation projection.

## Dependency Direction

ProviderRuntime and Workflows Runtime depend on LLM contracts; LLM Abstractions gains no reverse dependency.

## Pattern Decision

Use immutable checked aggregation; reject mutable shared counters and final-attempt-only projection.

## Testability Contract

Adapter tests vary attempt outcomes and counters; workflow tests consume typed failures with and without known usage.

## Partial Class Policy

No new partials or broad utility type; keep arithmetic on the strongly typed usage value.

## Architecture Proof Required

Realistic positive retry plus negative empty/failure/deadline/overflow/cancellation cases, exact source assertions, and affected-project builds.

## Gate result

- **Status:** Complete
- **Decision:** Pass
- **Evidence:** `proof-manifest.json`, `SESSION-HANDOFF.md`, and `../../proof/SB07`
- **Next subbundle:** SB08 unlocked
