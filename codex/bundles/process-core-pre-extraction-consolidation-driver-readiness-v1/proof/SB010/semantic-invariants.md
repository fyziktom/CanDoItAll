# SB010 Semantic Invariants

## Invariants

- Invariant ID: `SB010-INV-001`
- Source raw note: `Hydration is still application-heavy; separate pure read-model outputs and side-effect collaborators without rushing Process Core.`
- Expected behavior: Candidate hydration loads a no-tracking EF snapshot, prepares artifact inputs through a dedicated service, assembles subprocess/workflow candidates in the hydrated assembler, and keeps direct-agent side-effect work delegated while preserving candidate factory defaults.
- Disallowed shallow implementation: A nominal file split that leaves dispatchable-step candidate assembly or artifact-input preparation inside `ProcessDispatchCandidateHydrationService`, allows loader mutations, or hides direct-agent binding/recovery side effects behind pure assembly names.
- Failing-first test: `N/A - no production behavior change was intended; this subbundle validates an existing behavior-preserving hydration ownership split.`
- Passing test: `bundle://proof/SB010/transcripts/hydration-split-architecture-test.txt` and `bundle://proof/SB010/transcripts/candidate-factory-focused-integration-tests.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateHydrationService.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateHydrationLoader.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateArtifactInputPreparationService.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchHydratedCandidateAssembler.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchDirectAgentCandidateAssembler.cs`, `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`, `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- Production assertions: `bundle://proof/SB010/transcripts/hydration-split-source-assertions.txt`
- Red-team negative case: Reintroducing `snapshot.DispatchableSteps` or `ProcessDispatchArtifactInputAssembler.BuildResolvedArtifactInputs` into `ProcessDispatchCandidateHydrationService` fails the SB010 architecture/source assertions.
- Downstream dependency check: `SB011` may focus on direct-agent binding, recovery, and cooperation ownership because EF readback and artifact-input preparation are already separated.

## Raw Note Closure

- Hydration split: `Partially solved by explicit loader, artifact-input preparation service, and hydrated candidate assembler; SB011 owns direct-agent side-effect collaborator split and SB012 owns critical parity.`
- Preserve candidate defaults: `Proved through focused candidate factory integration tests.`
- Do not rush Process Core: `Partially solved without creating Core; final decision remains owned by SB036.`
