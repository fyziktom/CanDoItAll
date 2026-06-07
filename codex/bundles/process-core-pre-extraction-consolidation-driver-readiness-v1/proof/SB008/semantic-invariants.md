# SB008 Semantic Invariants

## Invariants

- Invariant ID: `SB008-INV-001`
- Source raw note: `Preserve finalizer behavior while keeping dispatcher-owned finalizer context conversion at the application edge.`
- Expected behavior: Finalizer context conversion remains inside `ProcessDispatchFinalizerAdapter`, but its public surface is route/application input records rather than dispatcher aliases or duplicate overloads.
- Disallowed shallow implementation: Keeping public dispatcher-alias overloads or moving dispatcher aliases into `ProcessDispatchFinalizerApplicationService`.
- Failing-first test: `N/A - behavior-preserving boundary refactor; architecture and focused finalizer parity tests validate the edge after overload removal.`
- Passing test: `bundle://proof/SB008/transcripts/finalizer-adapter-focused-integration-tests.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchFinalizerAdapter.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteHandlers.cs`, `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- Production assertions: `bundle://proof/SB008/transcripts/finalizer-adapter-source-assertions.txt`
- Red-team negative case: A new `public async Task FinalizeDirectAgentCompletionAsync(DispatchCandidate ...` overload fails the source assertion.
- Downstream dependency check: `SB009` must prove finalizer parity after this adapter confinement.

## Raw Note Closure

- Preserve finalizer behavior: `Partially solved by input-based adapter confinement and focused parity tests; critical parity gate remains owned by SB009.`
- Keep future Core boundary clean: `Partially solved by removing public dispatcher-alias overloads without creating Core.`
