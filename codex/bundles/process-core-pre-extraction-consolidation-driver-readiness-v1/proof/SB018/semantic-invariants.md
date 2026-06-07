# SB018 Semantic Invariants

## Invariants

- Invariant ID: `SB018-INV-001`
- Source raw note: `Prove capability gap, observing state, terminal mirror, completed projection, parent finalizer, lineage.`
- Expected behavior: Subprocess runtime starts or observes child runs, blocks capability gaps, mirrors terminal non-completed child states, projects completed child artifacts, finalizes parent completion through subprocess finalizer input, and preserves subprocess artifact lineage validation.
- Disallowed shallow implementation: Boundary cleanup that compiles but skips capability-gap blocking, treats active child runs as terminal, bypasses projection persistence, skips parent finalizer, or weakens child lineage validation.
- Failing-first test: `N/A - no production behavior change was intended; this critical gate validates SB016/SB017 behavior-preserving subprocess refactors.`
- Passing test: `bundle://proof/SB018/transcripts/subprocess-parity-focused-integration-tests.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchSubprocessRuntimeModels.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchSubprocessRuntimeService.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessLifecycleRules.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessCapabilityGapInspector.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessProjectionPersistenceService.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessProjectionPlanBuilder.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessProjectionWriterCoordinator.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessProjectionGapJournalCoordinator.cs`, `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`, `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- Production assertions: `bundle://proof/SB018/transcripts/source-assertions-and-scans.txt`
- Red-team negative case: Removing projection persistence delegation, altering lifecycle transition fields, changing capability-gap summary, dropping subprocess finalizer context, or weakening child lineage validation fails Gate F tests.
- Downstream dependency check: `SB019` may start direct-agent execution DTO hardening because subprocess parity is proved.

## Raw Note Closure

- Preserve subprocess behavior: `Solved for Gate F; later gates own direct-agent execution, artifact, wrapper, Core rehearsal, and driver readiness parity.`
- Do not rush Process Core: `Partially solved by explicit subprocess owners without creating Core; final decision remains owned by SB036.`
- No production driver API: `Partially solved by Gate F source scans; final driver decision remains owned by SB033/SB036.`
