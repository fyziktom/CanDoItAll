# SB027 Semantic Invariants

## Invariants

- Invariant ID: `SB027-INV-001`
- Source raw note: `Prove no facade resurrection, no side-effect movement into pure rules, and all tests green.`
- Expected behavior: Dispatcher static facades for route eligibility and subprocess artifact source mapping remain removed, owning pure rule/resolver classes preserve behavior, and side-effectful helpers remain application-local.
- Disallowed shallow implementation: Moving DB, filesystem, transition, workspace, storage, AgentFramework, mutable editor, or adapter compatibility behavior into pure rule classes while claiming facade burn-down.
- Failing-first test: `N/A - no production behavior change was intended; this critical gate validates wrapper parity after SB025/SB026.`
- Passing test: `bundle://proof/SB027/transcripts/gate-i-architecture-test.txt` and `bundle://proof/SB027/transcripts/wrapper-parity-focused-integration-tests.txt`
- Changed source files: `repo://codex/bundles/process-core-pre-extraction-consolidation-driver-readiness-v1/analysis/04-static-wrapper-inventory.md`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteSnapshot.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessArtifactSourceResolver.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/WorkflowSubprocessArtifactMapper.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjectionUtilities.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteServices.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchTechnicalAgentBindingCoordinator.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRecoveryQueryHelper.cs`, `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`, `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- Production assertions: `bundle://proof/SB027/transcripts/source-assertions-and-scans.txt`
- Red-team negative case: Reintroducing dispatcher route eligibility facades, moving manual recovery EF lookup into a pure rule, moving directory creation into a future Core candidate, or adding route adapter calls to route services fails SB027 proof.
- Downstream dependency check: `SB028` may start Core candidate contract rehearsal because wrapper/facade parity is proved.

## Raw Note Closure

- Wrapper/facade parity: `Solved for SB027 with build, architecture, focused integration, and source proof.`
- Preserve side-effect ownership: `Solved for Gate I; later gates own Core rehearsal and driver readiness.`
- Do not rush Process Core: `Partially solved without creating Core; final decision remains owned by SB036.`
