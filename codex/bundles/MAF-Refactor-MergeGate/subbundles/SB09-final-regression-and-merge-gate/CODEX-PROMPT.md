You are Codex GPT-5.6 Sol with xHigh reasoning, acting as a senior C#/.NET architect.

        Execute `SB09 — Final regression and merge gate` on branch `maf-refactor`.

        Read the bundle root, this README, relevant architecture documents, current callers through
        CodeAnalysis MCP, and the installed C#/.NET architecture skills.

        Goal:

        Produce an independent, evidence-backed merge decision.

        Required work:

        1. Run both original MAF bundle guards and this bundle's guards.
2. Run a clean Release build.
3. Run all focused blocker tests.
4. Run the full Unit suite and compare exact failures with development.
5. Run the full Integration suite or independently prove any environment-gated exclusion.
6. Run floating Canvas-to-Gantt, mixed approval, workflow LLM, profile-switch, runtime-state, and process-lease smoke scenarios.
7. Review dependency direction and forbidden source-kind ownership.
8. Record exact final SHA, worktree status, and merge base.
9. Create FINAL-MERGE-DECISION.md.

        Acceptance:

        - [ ] Release build is clean.
- [ ] All blocker tests pass.
- [ ] No new Unit/Integration failure exists versus development.
- [ ] Architecture and cutover guards pass.
- [ ] Application smoke scenarios pass.
- [ ] Final decision is explicit and evidence-backed.

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
