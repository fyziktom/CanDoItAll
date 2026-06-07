# SB011 Semantic Invariants

## Invariants

- Invariant ID: `SB011-INV-001`
- Source raw note: `Hydration split may lose direct-agent binding defaults, recovery execution ids, or cooperation metadata.`
- Expected behavior: Direct-agent candidate assembly preserves resolved technical agent id, chat session id, recovery execution id, manual recovery directive, project-structure access mutation, and cooperation metadata while confining those responsibilities to explicit collaborators.
- Disallowed shallow implementation: Creating collaborator files but leaving direct-agent binding, recovery id lookup, manual recovery directive lookup, or cooperation metadata resolution inside hydration service or generic hydrated candidate assembly.
- Failing-first test: `N/A - no production behavior change was intended; this subbundle validates an existing behavior-preserving collaborator split.`
- Passing test: `bundle://proof/SB011/transcripts/direct-agent-collaborator-architecture-test.txt` and `bundle://proof/SB011/transcripts/direct-agent-collaborator-focused-integration-tests.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateHydrationService.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchHydratedCandidateAssembler.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchDirectAgentCandidateAssembler.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchTechnicalAgentBindingCoordinator.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRecoveryQueryHelper.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCooperationMetadataResolver.cs`, `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`, `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- Production assertions: `bundle://proof/SB011/transcripts/direct-agent-collaborator-source-assertions.txt`
- Red-team negative case: Moving `ProcessDispatchTechnicalAgentBindingCoordinator.ResolveAsync`, `ProcessDispatchRecoveryQueryHelper.ResolveRecoverableExecutionRunId`, or `ProcessDispatchCooperationMetadataResolver.ResolveProcessCooperationMetadata` into `ProcessDispatchCandidateHydrationService` or `ProcessDispatchHydratedCandidateAssembler` fails the SB011 guard.
- Downstream dependency check: `SB012` may run hydration parity because binding, recovery, and cooperation responsibilities are explicit.

## Raw Note Closure

- Binding/recovery/cooperation split: `Solved for SB011 with explicit collaborators; SB012 owns critical parity proof.`
- Preserve direct-agent facts: `Proved through focused direct-agent candidate factory tests.`
- Do not rush Process Core: `Partially solved without creating Core; final decision remains owned by SB036.`
