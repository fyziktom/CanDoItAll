# SB026 Semantic Invariants

## Invariants

- Invariant ID: `SB026-INV-001`
- Source raw note: `Remove callers from dispatcher static facades where pure owners already exist.`
- Expected behavior: Low-risk pure route eligibility and subprocess artifact mapping logic is consumed through owning module-local rules/resolvers, while side-effectful helpers remain application-local.
- Disallowed shallow implementation: Removing dispatcher wrappers while moving DB, filesystem, transition, AgentFramework, mutable editor, or compatibility adapter behavior into pure-rule classes.
- Failing-first test: `N/A - no production behavior change was intended; this subbundle validates existing low-risk pure wrapper ownership.`
- Passing test: `bundle://proof/SB026/transcripts/pure-wrapper-focused-integration-tests.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteSnapshot.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessArtifactSourceResolver.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/WorkflowSubprocessArtifactMapper.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjectionUtilities.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchTechnicalAgentBindingCoordinator.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRecoveryQueryHelper.cs`, `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- Production assertions: `bundle://proof/SB026/transcripts/pure-wrapper-source-assertions.txt`
- Red-team negative case: Reintroducing dispatcher static route eligibility wrappers or moving `Directory.CreateDirectory`, `TransitionStepWithClaimAsync`, or manual recovery EF lookup into pure rules fails SB026 proof.
- Downstream dependency check: `SB027` may run wrapper parity because the only approved low-risk pure wrapper movement is proved.

## Raw Note Closure

- Low-risk pure wrapper movement: `Solved for SB026 with route eligibility and subprocess artifact mapping ownership proof.`
- Preserve side-effect ownership: `Partially proved here; SB027 owns critical wrapper parity.`
- Do not rush Process Core: `Partially solved without creating Core; final decision remains owned by SB036.`
