# Source Impact Inventory

Primary production source references:

| File | Expected role |
| --- | --- |
| `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ImplementationProof.cs` | Main target seam. |
| `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ToolValidation.cs` | Consumer of implementation proof and missing required tools. |
| `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs` | Carries proof across execution attempts. |
| `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RecoveryPackets.cs` | Builds retry/rework packets from proof failures. |
| `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ProjectPaths.cs` | Path and external target mapping dependencies. |
| `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.WebHostProof.cs` | DotNet host shape proof dependencies. |
| `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.DotnetRunCleanup.cs` | Runtime cleanup; reference only unless needed for evidence map. |
| `tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs` | Main focused parity test surface. |
| `tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs` | Architecture guardrails. |

Codex must update this inventory with current exact line counts before SB05 production movement.
