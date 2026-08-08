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

        - [x] Release build is clean.
- [x] All blocker tests pass.
- [x] No new Unit/Integration failure exists versus development.
- [x] Architecture and cutover guards pass.
- [x] Application smoke scenarios pass.
- [x] Final decision is explicit and evidence-backed.

        ## Proof requirements

        Create `proof/proof-manifest.json` and `SESSION-HANDOFF.md`. Record starting/ending SHA, changed
        files, commands, exit codes, test counts, architecture checks, bugs found, deviations, residual
        risk, and whether the next subbundle is unlocked.

## Execution contract

- **Owned finding:** MRG-011 and final verification of MRG-001 through MRG-010.
- **Proof tier:** Governed.
- **Progression gate:** Merge unlocks only on an explicit `MERGE READY` decision with clean durable evidence and matching source/worktree state.
- **Reopen trigger:** Any final build/test/guard/smoke failure, proof mismatch, stale SHA, dependency regression, or verifier rejection reopens the owning prerequisite.

## C# Architecture Impact

Independent final review only; production behavior changes require reopening the owning implementation subbundle.

## Boundary Ownership

Verify every implemented responsibility remains with the owner recorded in `architecture/09-csharp-execution-guard.md`.

## Dependency Direction

Run post-change CodeAnalytics inventory/dependency/cycle proof and repository architecture guards; reject new forbidden references.

## Pattern Decision

Verify selected patterns produced real seams and no hard-coded registry, ambient fallback, fixed-scope cleanup, instance-local shared lock, or reconstructed compensation remains.

## Testability Contract

Run focused blockers, affected projects, full Unit/Integration comparison, required smokes, and an independent proof-manifest verifier.

## Partial Class Policy

Assert no new production partial file and no existing broad owner absorbed the extracted behavior.

## Architecture Proof Required

Governed final transcripts, hashes, source assertions, architecture review gate, anti-stub audit, red-team/verifier artifact, exact SHA/worktree, and final decision.

## Gate result

- **Status:** Complete
- **Decision:** Pass / MERGE READY
- **Evidence:** `proof-manifest.json`, `SESSION-HANDOFF.md`, `../../proof/SB09`, and `../../reviews/FINAL-MERGE-DECISION.md`
- **Next subbundle:** None; bundle closure complete
