# SB08 Source Assertions

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessAutomationExecutionRunSelection.cs` exists and owns blocking, stale, recoverable, competing-active, fresh-recovery, completion-skip, and busy-exception rules.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs` preserves wrapper method names while delegating to `ProcessAutomationExecutionRunSelection`.
- `TryAdoptConcurrentAutomationExecutionAsync` and `ResolveCompetingActiveAutomationExecutionAsync` still own execution-client listing/detail retrieval, polling, delay, and `ConcurrentAutomationExecution` construction.
- `ProcessAutomationExecutionRunSelection` contains no EF, storage, workflow, subprocess, agent execution, `Task.Delay`, execution-client, Process Core, or process driver API references.
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs` includes `SB08_INV_001` and `SB08_INV_002` Gate B architecture tests.
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs` includes focused selection/parity tests for blocking, recoverable, stale, competing, fresh recovery, completion skip, and busy exception semantics.
