# SB009 Semantic Invariants

## Invariants

- Invariant ID: `SB009-INV-001`
- Source raw note: `Preserve finalizer behavior while keeping finalizer intent and adapter boundaries ready for future Core extraction.`
- Expected behavior: Null finalizer results do not apply transitions; non-null finalizer results apply transitions for workflow, manager recovery, direct-agent, and subprocess paths with the same candidate, claim, executor kind, status, reason, ids, response text, recovery ids, projection flags, and artifact validation context as before the boundary cleanup.
- Disallowed shallow implementation: A boundary cleanup that compiles but drops finalizer application, applies null finalizer output, loses recovery/subprocess/workflow ids, changes direct-agent projection/recovery flags, or reintroduces dispatcher aliases into the application service.
- Failing-first test: `N/A - no production behavior change was intended; this critical gate validates SB007/SB008 behavior-preserving finalizer boundary refactors.`
- Passing test: `bundle://proof/SB009/transcripts/finalizer-parity-focused-integration-tests.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchFinalizerAdapter.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchFinalizerInputs.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteHandlers.cs`, `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`, `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- Production assertions: `bundle://proof/SB009/transcripts/source-assertions-and-scans.txt`
- Red-team negative case: Returning null from `FinalizeStepCompletionAsync` must leave `appliedTransitions` empty, and enabling non-null results must produce four applied transitions with workflow/recovery/direct/subprocess artifact validation context.
- Downstream dependency check: `SB010` hydration work may start only because Gate C proved finalizer parity and no Core/driver/UI drift.

## Raw Note Closure

- Preserve finalizer behavior: `Solved for Gate C finalizer parity; later hydration, subprocess, projection, execution, and artifact gates own their narrower parity areas.`
- Do not rush Process Core: `Partially solved by finalizer intent and adapter boundary proof without creating Core; final decision remains owned by SB036.`
- No production driver API: `Partially solved by Gate C source scans; final driver decision remains owned by SB033/SB036.`
