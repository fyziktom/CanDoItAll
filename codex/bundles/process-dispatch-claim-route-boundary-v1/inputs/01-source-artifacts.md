# Source Artifacts

| Artifact | Type | Durable reference | Purpose |
| --- | --- | --- | --- |
| Original request | Raw prompt | `bundle://inputs/00-original-request.md` | Preserves the user request and hard constraints. |
| Branch review summary | Preparation summary | `bundle://inputs/01-branch-review-summary.md` | Captures observed source shape after the previous finalizer boundary bundle. |
| Structured input | Normalized request notes | `bundle://inputs/02-structured-input.md` | Captures primary problem, goals, and non-goals. |
| Dispatch orchestrator | Production source | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` | Main dispatch loop, claim lifecycle, route orchestration, and finalization entry. |
| Concurrency selection | Production source | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs` | Current execution-run selection rules and recovery semantics. |
| Step-completion finalizer | Production source | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` | Existing helper boundary that the new dispatch cut must preserve. |
| Architecture tests | Test source | `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs` | Guardrail surface for no-core, no-driver, no dependency drift checks. |
| Dispatch integration tests | Test source | `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs` | Runtime behavior proof for dispatch/concurrency parity. |
