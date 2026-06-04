# Source Impact Inventory

Primary production files expected to change:

- `src/CanDoItAll.Processes.Contracts/Automation/ProcessAutomationExecutionContracts.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessAutomationExecutionClient.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Costing.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.CompletionArtifactRecovery.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ToolValidation.cs`
- New helper file(s) under `src/CanDoItAll.Modules.Processes/Automation/Dispatch/`.

Files that should not change unless explicitly justified:

- MAF provider composition files.
- Runtime tool provider files.
- Razor/UI/component/CSS/JS files.
- EF migrations and entity configuration.
