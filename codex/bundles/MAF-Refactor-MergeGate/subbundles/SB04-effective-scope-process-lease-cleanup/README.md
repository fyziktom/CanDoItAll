# SB04 — Effective-scope process lease cleanup

        **Depends on:** SB03  
        **Required before merge:** Yes

        ## Goal

        Clean durable ExecutionRun process leases from the same effective scope in which they were created.

        ## Required work

        1. Make terminal cleanup resolve the trusted effective workspace scope from the persisted run.
2. Reject conflicting run metadata/governance scope before cleanup.
3. Replace the fixed-scope cleaner dependency with a scope-aware cleanup factory/coordinator.
4. Create only the minimum scope-bound command/process services required for cleanup and dispose them.
5. Keep persisted-terminal-run verification and durable cleanup claims.
6. Test organization execution storage with a project-scoped runtime lease.
7. Test approval continuation and failed terminal runs with project-scoped leases.
8. Do not move process-lease business semantics into MAF.

        ## Primary files

        - `src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ProcessLeases.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Process/WorkspaceExecutionRunProcessLeases.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Commands/WorkspaceCommandExecutionService.cs`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Workspace/AgentFrameworkWorkspaceFactory.cs`
- `tests/Unit/CanDoItAll.Tests.Unit/AgentFrameworkWorkspaceProcessLeaseCleanupTests.cs`

        ## Acceptance

        - [x] A project-scoped kept-alive process launched from floating chat is stopped at terminal completion.
- [x] Its project-scoped durable lease is removed.
- [x] Organization and sandbox runs still clean correctly.
- [x] Scope conflict fails closed and retains the lease for retry.
- [x] Concurrent cleanup remains idempotent.

        ## Proof requirements

        Create `proof/proof-manifest.json` and `SESSION-HANDOFF.md`. Record starting/ending SHA, changed
        files, commands, exit codes, test counts, architecture checks, bugs found, deviations, residual
        risk, and whether the next subbundle is unlocked.

## Execution contract

- **Owned finding:** MRG-004.
- **Proof tier:** Governed.
- **Progression gate:** SB05 unlocks only after cross-scope terminal cleanup, failure/continuation, scope-conflict retry, disposal, and idempotency proofs pass.
- **Reopen trigger:** Cleanup uses a fixed workspace scope, accepts conflicting scope evidence, or changes primary run outcome authority.

## C# Architecture Impact

Extract or inject only the minimum scope-aware construction needed for durable lease cleanup.

## Boundary Ownership

Core owns trusted cleanup coordination/contracts; Modules.AgentFramework composition creates scope-bound services; MAF owns no lease semantics.

## Dependency Direction

Composition depends on Core contracts. No Core-to-module dependency, service location, or product reference from MAF is permitted.

## Pattern Decision

Use a narrow scope-aware factory/coordinator; reject the fixed-scope cleaner and general `IServiceProvider` lookup.

## Testability Contract

Instantiate cleanup collaborators directly and prove an organization-stored run cleans a real project-scoped durable lease.

## Partial Class Policy

Existing execution partials may delegate only; no new production partial file or duplicated cleanup algorithm.

## Architecture Proof Required

Governed lifecycle transcripts, disposal/source assertions, real durable-lease matrix, idempotency/concurrency proof, and architecture review.

## Execution result

- **Status:** Complete
- **Decision:** Pass
- **Proof:** `../../proof/SB04` and `proof-manifest.json`
- **Next:** SB05 unlocked
