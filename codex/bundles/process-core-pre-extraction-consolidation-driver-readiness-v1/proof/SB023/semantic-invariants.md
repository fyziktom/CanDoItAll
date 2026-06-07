# SB023 Semantic Invariants

## Invariants

- Invariant ID: `SB023-INV-001`
- Source raw note: `Group pure matcher/satisfaction rules without moving storage/workspace/persistence.`
- Expected behavior: Validation, projection, satisfaction, matcher, and resolver paths use `ProcessArtifactExpectationSnapshot`; pure matcher/resolver rules remain separate from storage/workspace/persistence side effects.
- Disallowed shallow implementation: Leaving `ProcessProjectionArtifactExpectation`, `ProcessArtifactValidationExpectation`, projection converters, or dispatcher expectation aliases in active matcher/satisfaction paths.
- Failing-first test: `N/A - no production behavior change was intended; this subbundle validates existing shared artifact expectation snapshot usage.`
- Passing test: `bundle://proof/SB023/transcripts/artifact-satisfaction-snapshot-architecture-test.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactValidationSnapshot.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactValidationSnapshotBuilder.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessProjectionModels.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactSatisfactionSnapshot.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactExpectationMatcher.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactExpectationResolver.cs`, `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- Production assertions: `bundle://proof/SB023/transcripts/artifact-satisfaction-snapshot-source-assertions.txt`
- Red-team negative case: Restoring `ProcessProjectionArtifactExpectation` or changing `ProcessArtifactSatisfactionSnapshot` back to dispatcher expectations fails SB023 proof.
- Downstream dependency check: `SB024` may run artifact parity because shared expectation snapshots are proved across validation/projection/satisfaction paths.

## Raw Note Closure

- Pure matcher/satisfaction candidate map: `Solved for SB023 with shared expectation snapshots and pure matcher/resolver consumers.`
- Preserve artifact validation/projection behavior: `Partially proved here; SB024 owns critical artifact parity.`
- Do not rush Process Core: `Partially solved without creating Core; final decision remains owned by SB036.`
