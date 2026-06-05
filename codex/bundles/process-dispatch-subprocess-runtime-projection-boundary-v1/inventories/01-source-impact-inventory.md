# Source Impact Inventory

Primary files:

| File | Expected role |
| --- | --- |
| `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` | Main source; subprocess branch and artifact projection are the target seam. |
| `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.FinalizerContextFactory.cs` | Subprocess finalizer context factory must remain compatible. |
| `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRoutePlanner.cs` | Route order must remain unchanged. |
| `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateFactory.cs` | Candidate construction must remain compatible. |
| `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionLineageBuilder.cs` | May be referenced for subprocess projection lineage if useful, but do not change existing semantics without tests. |
| `tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs` | Main focused integration test surface. |
| `tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs` | Architecture guardrails. |

Codex must update this inventory with exact method names and line counts before SB05 production movement.
