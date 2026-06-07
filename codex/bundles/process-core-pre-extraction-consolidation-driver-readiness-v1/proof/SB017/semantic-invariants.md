# SB017 Semantic Invariants

## Invariants

- Invariant ID: `SB017-INV-001`
- Source raw note: `Separate child-artifact query, gap journal, parent artifact write, and save changes.`
- Expected behavior: Completed subprocess projection persists parent artifacts through `ProcessSubprocessProjectionPersistenceService`, with gap journaling and artifact writing delegated to coordinators, while runtime only decides when completed projection should run.
- Disallowed shallow implementation: Extracting a service name while leaving EF query/save, projection plan building, or writer calls in subprocess runtime.
- Failing-first test: `N/A - no production behavior change was intended; this subbundle validates an existing behavior-preserving projection persistence boundary.`
- Passing test: `bundle://proof/SB017/transcripts/subprocess-projection-persistence-architecture-test.txt` and `bundle://proof/SB017/transcripts/subprocess-projection-focused-integration-tests.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchSubprocessRuntimeService.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessProjectionPersistenceService.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessProjectionPlanBuilder.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessProjectionWriterCoordinator.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessProjectionGapJournalCoordinator.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteHandlers.cs`, `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`, `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- Production assertions: `bundle://proof/SB017/transcripts/subprocess-projection-persistence-source-assertions.txt`
- Red-team negative case: Reintroducing `ProcessSubprocessProjectionPlanBuilder.Build`, `projectionWriterCoordinator.WriteAsync`, `CreateDbContextAsync`, or `SaveChangesAsync` into `ProcessDispatchSubprocessRuntimeService` fails SB017 guards.
- Downstream dependency check: `SB018` may run subprocess parity because runtime input and projection persistence ownership are proved.

## Raw Note Closure

- Projection persistence boundary: `Solved for SB017 with persistence service and helper coordinators.`
- Preserve subprocess behavior: `Partially proved here; SB018 owns critical subprocess parity.`
- Do not rush Process Core: `Partially solved without creating Core; final decision remains owned by SB036.`
