# SB09 — Final regression and merge gate

        **Depends on:** SB08  
        **Required before merge:** Yes

        ## Goal

        Produce an independent, evidence-backed merge decision.

        ## Required work

        1. Run both original MAF bundle guards and this bundle's guards.
2. Run a clean Release build.
3. Run all focused blocker tests.
4. Run the full Unit suite and compare exact failures with development.
5. Run the full Integration suite or independently prove any environment-gated exclusion.
6. Run floating Canvas-to-Gantt, mixed approval, workflow LLM, profile-switch, runtime-state, and process-lease smoke scenarios.
7. Review dependency direction and forbidden source-kind ownership.
8. Record exact final SHA, worktree status, and merge base.
9. Create FINAL-MERGE-DECISION.md.

        ## Primary files

        - `CanDoItAll.slnx`
- `codex/bundles/MAF-Refactor/scripts/`
- `codex/bundles/MAF-Refactor-Followup/scripts/`
- `tests/Unit/CanDoItAll.Tests.Unit/`
- `tests/Integration/CanDoItAll.Tests.Integration/`

        ## Acceptance

        - [ ] Release build is clean.
- [ ] All blocker tests pass.
- [ ] No new Unit/Integration failure exists versus development.
- [ ] Architecture and cutover guards pass.
- [ ] Application smoke scenarios pass.
- [ ] Final decision is explicit and evidence-backed.

        ## Proof requirements

        Create `proof/proof-manifest.json` and `SESSION-HANDOFF.md`. Record starting/ending SHA, changed
        files, commands, exit codes, test counts, architecture checks, bugs found, deviations, residual
        risk, and whether the next subbundle is unlocked.
