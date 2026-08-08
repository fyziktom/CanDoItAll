# SB00 — Independent baseline and blocker reproduction

        **Depends on:** None  
        **Required before merge:** Yes

        ## Goal

        Re-anchor current HEAD and prove every blocker before changing production code.

        ## Required work

        1. Record branch, HEAD, merge base, worktree, .NET SDK, available MCPs, and installed skills.
2. Run a clean Release build and the current targeted test groups.
3. Write failing characterization tests for MRG-001 and MRG-003 through MRG-009.
4. Prove MRG-002 with a dependency/ownership map and a registration test before moving implementations.
5. Prove MRG-004 with an organization workspace service plus a project-scoped per-run command service and real durable lease.
6. Prove MRG-010 has no current production consumer before deactivating registration.
7. Do not change production behavior in SB00.

        ## Primary files

        - `tests/Unit/CanDoItAll.Tests.Unit/ExecutionGovernanceEnforcementTests.cs`
- `tests/Unit/CanDoItAll.Tests.Unit/ToolGovernancePipelineAndApprovalLifecycleTests.cs`
- `tests/Unit/CanDoItAll.Tests.Unit/AgentFrameworkWorkspaceProcessLeaseCleanupTests.cs`
- `tests/Unit/CanDoItAll.Tests.Unit/FileLlmConversationStoreTests.cs`
- `tests/Unit/CanDoItAll.Tests.Unit/LlmConversationServiceTests.cs`
- `tests/Unit/CanDoItAll.Tests.Unit/ProviderBackedLlmInvocationAdapterTests.cs`

        ## Acceptance

        - [ ] Every code blocker has a deterministic failing test or executable architecture proof.
- [ ] Baseline build and test counts are recorded.
- [ ] No production file is changed.

        ## Proof requirements

        Create `proof/proof-manifest.json` and `SESSION-HANDOFF.md`. Record starting/ending SHA, changed
        files, commands, exit codes, test counts, architecture checks, bugs found, deviations, residual
        risk, and whether the next subbundle is unlocked.
