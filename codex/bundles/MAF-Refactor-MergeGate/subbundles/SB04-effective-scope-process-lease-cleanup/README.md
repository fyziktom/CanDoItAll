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

        - [ ] A project-scoped kept-alive process launched from floating chat is stopped at terminal completion.
- [ ] Its project-scoped durable lease is removed.
- [ ] Organization and sandbox runs still clean correctly.
- [ ] Scope conflict fails closed and retains the lease for retry.
- [ ] Concurrent cleanup remains idempotent.

        ## Proof requirements

        Create `proof/proof-manifest.json` and `SESSION-HANDOFF.md`. Record starting/ending SHA, changed
        files, commands, exit codes, test counts, architecture checks, bugs found, deviations, residual
        risk, and whether the next subbundle is unlocked.
