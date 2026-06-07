# SB025 Semantic Invariants

## Invariants

- Invariant ID: `SB025-INV-001`
- Source raw note: `Classify every remaining dispatcher wrapper as pure, application, infrastructure, or compatibility.`
- Expected behavior: The bundle owns a current wrapper inventory that distinguishes pure route/subprocess rules from application, infrastructure, and compatibility helpers.
- Disallowed shallow implementation: Treating DB queries, filesystem operations, transition writes, storage/workspace access, AgentFramework execution, mutable editor updates, or dispatcher alias adapters as pure-rule candidates.
- Failing-first test: `N/A - no production behavior change was intended; this subbundle creates and validates an inventory.`
- Passing test: `bundle://proof/SB025/transcripts/wrapper-inventory-build.txt`
- Changed source files: `repo://codex/bundles/process-core-pre-extraction-consolidation-driver-readiness-v1/analysis/04-static-wrapper-inventory.md`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteSnapshot.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessArtifactSourceResolver.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchTechnicalAgentBindingCoordinator.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRecoveryQueryHelper.cs`, `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`, `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- Production assertions: `bundle://proof/SB025/transcripts/wrapper-inventory-source-assertions.txt`
- Red-team negative case: Marking `ApplyProjectStructureReadAccess`, `LoadLatestManualRecoveryDirectiveAsync`, directory creation, transition writes, or route adapter conversion as pure movement candidates fails SB025 proof.
- Downstream dependency check: `SB026` may prove only the low-risk pure wrapper movement named in the inventory.

## Raw Note Closure

- Remaining dispatcher wrapper inventory: `Solved for SB025 with a current classification file and source assertions.`
- Preserve side-effect ownership: `Partially proved here; SB027 owns critical wrapper parity.`
- Do not rush Process Core: `Partially solved without creating Core; final decision remains owned by SB036.`
