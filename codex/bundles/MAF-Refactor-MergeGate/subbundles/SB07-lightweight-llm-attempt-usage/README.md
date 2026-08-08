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

        - [ ] Empty then success returns both attempts' usage.
- [ ] Two empty attempts fail with both attempts' usage.
- [ ] Empty then provider/deadline failure preserves known prior usage.
- [ ] Negative counters fail at the owning boundary.
- [ ] Workflow failure analytics no longer fabricate zero when usage is known.

        ## Proof requirements

        Create `proof/proof-manifest.json` and `SESSION-HANDOFF.md`. Record starting/ending SHA, changed
        files, commands, exit codes, test counts, architecture checks, bugs found, deviations, residual
        risk, and whether the next subbundle is unlocked.
