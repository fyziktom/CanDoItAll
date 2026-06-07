# SB024 Semantic Invariants

## Invariants

- Invariant ID: `SB024-INV-001`
- Source raw note: `Prove projection order, lineage, keys, satisfaction, provider-native browser evidence, validation behavior.`
- Expected behavior: Artifact projection keeps source-family order stable, shared expectation snapshots flow through validation/projection/satisfaction, lineage keys remain deterministic, provider-native browser artifacts are matched correctly, stale satisfaction is reset per execution, and validation enforces current lineage.
- Disallowed shallow implementation: Passing DTO shape tests while reordering projection families, weakening lineage keys, matching browser snapshots to broad evidence packs, retaining stale satisfaction, or accepting wrong-run artifacts.
- Failing-first test: `N/A - no production behavior change was intended; this critical gate validates artifact parity after SB022/SB023 boundary tightening.`
- Passing test: `bundle://proof/SB024/transcripts/artifact-parity-architecture-test.txt` and `bundle://proof/SB024/transcripts/artifact-parity-focused-integration-tests.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionOrchestrator.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionSourceAdapters.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionLineageBuilder.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessProviderNativeBrowserArtifactProjectionCoordinator.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactSatisfactionSnapshot.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactValidationSnapshot.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessProjectionModels.cs`, `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`, `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- Production assertions: `bundle://proof/SB024/transcripts/source-assertions-and-scans.txt`
- Red-team negative case: Reordering projection families, accepting wrong-run workspace artifacts, binding provider-native snapshots to broad evidence packs, or removing recovery key hashing fails SB024 proof.
- Downstream dependency check: `SB025` may start wrapper inventory because artifact DTO and parity boundaries are proved.

## Raw Note Closure

- Artifact parity: `Solved for SB024 with architecture, focused integration, and source proof.`
- Preserve provider-native browser and validation behavior: `Solved for SB024 with focused provider-native, lineage, satisfaction, and validation tests.`
- Do not rush Process Core: `Partially solved without creating Core; final decision remains owned by SB036.`
