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

## Execution contract

- **Owned findings:** MRG-001 through MRG-010 reproduction and MRG-011 baseline evidence.
- **Proof tier:** Governed.
- **Progression gate:** SB01 unlocks only when every blocker has deterministic failing-first proof or an executable architecture proof and production source is unchanged.
- **Reopen trigger:** Any blocker cannot be reproduced, an expected source owner is stale, or later evidence contradicts the baseline.

## C# Architecture Impact

Characterization and architecture inventory only; no production architecture changes are permitted.

## Boundary Ownership

Confirm current and target owners in `architecture/09-csharp-execution-guard.md`.

## Dependency Direction

Capture the CodeAnalytics project graph and reject existing assumptions that do not match it.

## Pattern Decision

No production pattern is introduced in SB00; record the planned typed seams for later work.

## Testability Contract

Each blocker must fail for the real shallow implementation and identify the later positive case.

## Partial Class Policy

No production partial file or production behavior changes are allowed.

## Architecture Proof Required

Governed failing-first transcripts, source ownership assertions, snapshot health, dependency/cycle evidence, and a no-production-diff assertion.
