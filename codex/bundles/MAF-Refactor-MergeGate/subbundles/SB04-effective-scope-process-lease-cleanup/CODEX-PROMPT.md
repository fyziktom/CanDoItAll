You are Codex GPT-5.6 Sol with xHigh reasoning, acting as a senior C#/.NET architect.

        Execute `SB04 — Effective-scope process lease cleanup` on branch `maf-refactor`.

        Read the bundle root, this README, relevant architecture documents, current callers through
        CodeAnalysis MCP, and the installed C#/.NET architecture skills.

        Goal:

        Clean durable ExecutionRun process leases from the same effective scope in which they were created.

        Required work:

        1. Make terminal cleanup resolve the trusted effective workspace scope from the persisted run.
2. Reject conflicting run metadata/governance scope before cleanup.
3. Replace the fixed-scope cleaner dependency with a scope-aware cleanup factory/coordinator.
4. Create only the minimum scope-bound command/process services required for cleanup and dispose them.
5. Keep persisted-terminal-run verification and durable cleanup claims.
6. Test organization execution storage with a project-scoped runtime lease.
7. Test approval continuation and failed terminal runs with project-scoped leases.
8. Do not move process-lease business semantics into MAF.

        Acceptance:

        - [ ] A project-scoped kept-alive process launched from floating chat is stopped at terminal completion.
- [ ] Its project-scoped durable lease is removed.
- [ ] Organization and sandbox runs still clean correctly.
- [ ] Scope conflict fails closed and retains the lease for retry.
- [ ] Concurrent cleanup remains idempotent.

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
