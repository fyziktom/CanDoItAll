# SB012 Semantic Invariants

## Invariants

- Invariant ID: `SB012-INV-001`
- Source raw note: `Hydration split may lose direct-agent binding defaults, recovery execution ids, or cooperation metadata.`
- Expected behavior: Hydration preserves subprocess and workflow defaults, direct-agent technical agent and recovery facts, project-structure read access mutation, recoverable execution id selection, manual recovery directive readback, and cooperation metadata/workspace profile selection while keeping EF readback, artifact-input preparation, generic assembly, and direct-agent side effects in explicit owners.
- Disallowed shallow implementation: Passing build by moving code into helper files while silently changing candidate defaults, skipping project-structure access mutation, dropping recoverable execution ids, or weakening cooperation metadata.
- Failing-first test: `N/A - no production behavior change was intended; this critical gate validates SB010/SB011 behavior-preserving hydration refactors.`
- Passing test: `bundle://proof/SB012/transcripts/hydration-parity-focused-integration-tests.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateHydrationService.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateHydrationLoader.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateArtifactInputPreparationService.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchHydratedCandidateAssembler.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchDirectAgentCandidateAssembler.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchTechnicalAgentBindingCoordinator.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRecoveryQueryHelper.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCooperationMetadataResolver.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateFactory.cs`, `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`, `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- Production assertions: `bundle://proof/SB012/transcripts/source-assertions-and-scans.txt`
- Red-team negative case: Reintroducing side effects into hydration service, changing factory defaults, removing project access mutation, weakening recoverable execution id selection, or changing cooperation profile selection fails Gate D tests or source assertions.
- Downstream dependency check: `SB013` may start pre-execution purity work because hydration parity and side-effect ownership are proved.

## Raw Note Closure

- Preserve hydration behavior: `Solved for Gate D; later gates own pre-execution, subprocess, projection, execution, and artifact parity.`
- Do not rush Process Core: `Partially solved by explicit application-local hydration owners without creating Core; final decision remains owned by SB036.`
- No production driver API: `Partially solved by Gate D source scans; final driver decision remains owned by SB033/SB036.`
