You are Codex GPT-5.6 Sol with xHigh reasoning, acting as a senior C#/.NET architect.

        Execute `SB07 — Lightweight LLM attempt usage accounting` on branch `maf-refactor`.

        Read the bundle root, this README, relevant architecture documents, current callers through
        CodeAnalysis MCP, and the installed C#/.NET architecture skills.

        Goal:

        Preserve usage from every provider attempt made by the bounded empty-response retry.

        Required work:

        1. Add non-negative overflow-safe LlmUsage aggregation.
2. Accumulate usage before evaluating whether response text is usable.
3. Return aggregate usage when retry succeeds.
4. Attach known aggregate usage to typed EmptyResponse, ProviderFailure, and DeadlineExceeded failures.
5. Project typed failure usage into workflow failure observations.
6. Keep public messages sanitized and do not add provider-error retries.

        Acceptance:

        - [ ] Empty then success returns both attempts' usage.
- [ ] Two empty attempts fail with both attempts' usage.
- [ ] Empty then provider/deadline failure preserves known prior usage.
- [ ] Negative counters fail at the owning boundary.
- [ ] Workflow failure analytics no longer fabricate zero when usage is known.

        Constraints:

        - Add a failing characterization test before production changes.
        - Preserve completed MAF boundaries.
        - Make the smallest cohesive owner-boundary fix.
        - Keep source comments in English.
        - Do not add ordinary-chat product features.
        - Do not weaken security, process, approval, workspace, or regression tests.
        - Stop on a failed gate.
        - Run focused tests, neighboring tests, Release build, and relevant guards.
        - Write proof and session handoff before closure.
