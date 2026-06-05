# Source Impact Inventory

Primary files:

| File | Role | Expected movement |
| --- | --- | --- |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` | Main dispatch orchestration, durable claim lifecycle, heartbeat setup, pre-execution routing, subprocess/workflow/direct-agent route entry, finalizer context construction | Extract local route facts, claim/session wrappers, route planning decisions, start-transition builders, and finalizer context factory only |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs` | Execution-run concurrency selection, recovery, stale/busy/competing run decisions | Extract pure selection rules and preserve wrapper entry points |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` | Finalization orchestration | Touch only context construction if needed; do not move lifecycle finalization |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchLeaseHeartbeat.cs` | Existing heartbeat loop and claim-lost cancellation source | Reuse from claim/session boundary; do not hide claim-lost exceptions |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs` | Agent execution loop | Do not expand scope |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ToolValidation.cs` | Required tool and completion rules | Keep stable; only smoke tests |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs` | Projection side effects | Keep stable |
