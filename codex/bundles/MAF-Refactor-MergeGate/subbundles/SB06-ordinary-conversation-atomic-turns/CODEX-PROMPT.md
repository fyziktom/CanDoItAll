You are Codex GPT-5.6 Sol with xHigh reasoning, acting as a senior C#/.NET architect.

        Execute `SB06 — Ordinary conversation atomic turns` on branch `maf-refactor`.

        Read the bundle root, this README, relevant architecture documents, current callers through
        CodeAnalysis MCP, and the installed C#/.NET architecture skills.

        Goal:

        Make provider adoption, transcript, acceleration, and active-turn state one recoverable transaction.

        Required work:

        1. Persist pre-turn provider and acceleration compensation data in ActiveTurn when Adopt changes them.
2. Restore pre-turn provider and acceleration on provider failure, cancellation, explicit abandonment, and crash recovery.
3. Reject RenameAsync while ActiveTurn exists.
4. Reserve capacity for both user and assistant entries before provider invocation.
5. Validate unique entry ids and exact ActiveTurn user entry/turn identity.
6. Keep revision monotonic during rollback.
7. Do not add ordinary-chat UI, API, streaming, summarization, or branches.

        Acceptance:

        - [ ] Failed or abandoned Adopt restores the original provider and acceleration.
- [ ] Successful Adopt remains unchanged.
- [ ] Rename during active turn fails typed without changing state.
- [ ] Near-capacity turn fails before ILlmInvocationPort is called.
- [ ] Corrupted ActiveTurn metadata fails typed on load.
- [ ] No ordinary failure leaves an orphaned active turn.

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
