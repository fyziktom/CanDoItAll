You are Codex GPT-5.6 Sol with xHigh reasoning, acting as a senior C#/.NET architect.

        Execute `SB01 — Fail-closed governance restoration` on branch `maf-refactor`.

        Read the bundle root, this README, relevant architecture documents, current callers through
        CodeAnalysis MCP, and the installed C#/.NET architecture skills.

        Goal:

        Distinguish absent legacy authority from malformed current authority and reject unsafe restoration.

        Required work:

        1. Introduce an explicit Absent/Valid/Malformed authority projection read result.
2. Treat a present but malformed authority key as Malformed, never Absent.
3. Require valid authority whenever turn-context or transient-context metadata proves a context-admitted turn.
4. Validate agent id, profile id, profile generation, workspace scope, policy version, and fingerprint.
5. Use the same validated restoration for initial execution and approval continuation.
6. Retain a bounded positive-evidence legacy/detached path.

        Acceptance:

        - [ ] Malformed current authority fails before runtime/provider construction.
- [ ] Missing authority for a context-admitted turn fails closed.
- [ ] Agent/profile/generation/scope mismatch fails closed.
- [ ] Recognized detached and legacy runs remain compatible.
- [ ] Continuation never recaptures or drops original authority.

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
