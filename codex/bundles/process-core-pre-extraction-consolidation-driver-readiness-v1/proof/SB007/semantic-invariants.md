# SB007 Semantic Invariants

## Invariants

- Invariant ID: `SB007-INV-001`
- Source raw note: `Preserve finalizer behavior while moving closer to a future Process Core boundary.`
- Expected behavior: Workflow, recovery, direct-agent, and subprocess finalizer paths have explicit route/application intent DTOs while existing finalizer input call sites and adapter behavior remain compatible.
- Disallowed shallow implementation: Renaming input records or adding empty marker DTOs without preserving context fields, renew-lease delegates, dispatch claims, or adapter conversion behavior.
- Failing-first test: `N/A - behavior-preserving DTO refinement; focused finalizer parity tests validate the existing behavior after the split.`
- Passing test: `bundle://proof/SB007/transcripts/finalizer-intent-focused-integration-test.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchFinalizerInputs.cs`, `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- Production assertions: `bundle://proof/SB007/transcripts/finalizer-intent-source-assertions.txt`
- Red-team negative case: Removing an intent record or moving dispatcher aliases into `ProcessDispatchFinalizerApplicationService` fails the architecture test/source scan.
- Downstream dependency check: `SB008` depends on these explicit intent records before constraining the finalizer adapter edge further.

## Raw Note Closure

- Preserve finalizer behavior: `Partially solved by intent DTOs plus finalizer parity proof; full finalizer parity gate remains owned by SB009.`
- Move closer to Process Core: `Partially solved by explicit route/application finalizer intents without creating Core.`
