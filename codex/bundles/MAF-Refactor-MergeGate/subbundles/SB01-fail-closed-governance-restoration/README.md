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

## Execution contract

- **Owned finding:** MRG-001.
- **Proof tier:** Governed.
- **Progression gate:** SB02 unlocks only after all initial and continuation restoration paths share the fail-closed validation and positive-evidence legacy remains green.
- **Reopen trigger:** Any parser/restoration path converts malformed or identity-conflicting authority to absent/legacy or constructs a runtime before rejection.

## C# Architecture Impact

Isolate persisted-governance trust classification without widening the broad execution owner.

## Boundary Ownership

Core owns the typed read result and restoration validation; Models changes only if a stable shared contract is required.

## Dependency Direction

Core continues to depend inward on Models; no module or MAF dependency is permitted.

## Pattern Decision

Use a strongly typed tri-state result; nullable projection and exception-as-state are rejected.

## Testability Contract

Parser and execution-admission tests must prove rejection before runtime/provider construction plus a real compatible legacy case.

## Partial Class Policy

Edit the existing execution partial minimally; do not add another partial file or duplicate validation.

## Architecture Proof Required

Before/after symbol evidence, governed negative/positive transcripts, source assertion for the single restoration gate, and architecture review.
