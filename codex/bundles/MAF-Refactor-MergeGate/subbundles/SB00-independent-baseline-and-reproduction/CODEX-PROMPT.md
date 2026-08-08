You are Codex GPT-5.6 Sol with xHigh reasoning, acting as a senior C#/.NET architect.

        Execute `SB00 — Independent baseline and blocker reproduction` on branch `maf-refactor`.

        Read the bundle root, this README, relevant architecture documents, current callers through
        CodeAnalysis MCP, and the installed C#/.NET architecture skills.

        Goal:

        Re-anchor current HEAD and prove every blocker before changing production code.

        Required work:

        1. Record branch, HEAD, merge base, worktree, .NET SDK, available MCPs, and installed skills.
2. Run a clean Release build and the current targeted test groups.
3. Write failing characterization tests for MRG-001 and MRG-003 through MRG-009.
4. Prove MRG-002 with a dependency/ownership map and a registration test before moving implementations.
5. Prove MRG-004 with an organization workspace service plus a project-scoped per-run command service and real durable lease.
6. Prove MRG-010 has no current production consumer before deactivating registration.
7. Do not change production behavior in SB00.

        Acceptance:

        - [ ] Every code blocker has a deterministic failing test or executable architecture proof.
- [ ] Baseline build and test counts are recorded.
- [ ] No production file is changed.

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
