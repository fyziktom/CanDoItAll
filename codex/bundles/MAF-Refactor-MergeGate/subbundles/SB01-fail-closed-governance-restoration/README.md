# SB01 — Fail-closed governance restoration

        **Depends on:** SB00  
        **Required before merge:** Yes

        ## Goal

        Distinguish absent legacy authority from malformed current authority and reject unsafe restoration.

        ## Required work

        1. Introduce an explicit Absent/Valid/Malformed authority projection read result.
2. Treat a present but malformed authority key as Malformed, never Absent.
3. Require valid authority whenever turn-context or transient-context metadata proves a context-admitted turn.
4. Validate agent id, profile id, profile generation, workspace scope, policy version, and fingerprint.
5. Use the same validated restoration for initial execution and approval continuation.
6. Retain a bounded positive-evidence legacy/detached path.

        ## Primary files

        - `src/MAF/Common/CanDoItAll.AgentFramework.Core/Context/AgentTurnContextMetadata.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Models/Execution/AgentExecutionGovernanceSnapshot.cs`

        ## Acceptance

        - [ ] Malformed current authority fails before runtime/provider construction.
- [ ] Missing authority for a context-admitted turn fails closed.
- [ ] Agent/profile/generation/scope mismatch fails closed.
- [ ] Recognized detached and legacy runs remain compatible.
- [ ] Continuation never recaptures or drops original authority.

        ## Proof requirements

        Create `proof/proof-manifest.json` and `SESSION-HANDOFF.md`. Record starting/ending SHA, changed
        files, commands, exit codes, test counts, architecture checks, bugs found, deviations, residual
        risk, and whether the next subbundle is unlocked.
