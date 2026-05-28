# Source Artifacts

- Live run detail: `GET http://localhost:5032/api/processes/runs/801f259d-8a52-41b8-a99f-cc96a2fc1947`
  - Run: `Main app / Blazor app delivery`, status `Completed`, project `7330105d-8450-4c80-923b-5c27d8e63d6c`.
  - Contract artifact resolved product/output root to `output/process-runs/801f259d-8a52-41b8-a99f-cc96a2fc1947`, proving the external project structure output folder was not grounded for the run.
- PostgreSQL project structure rows from `candoitall_development`.
  - User-authored output path node: `custom:0e6475a1f98b484d90670671e73cbe76`, title and notes `C:\programovani\dotnet-demo\output`.
  - The output path is under `Main architecture` (`custom:daefe485465a48bda33ab947bfc5e6aa`), while the process run was linked to nested delivery node `Main app` (`custom:7404d4fd10624f468c2524ba618d747b`).
- Source files inspected:
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.ProjectPaths.cs`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.Grounding.cs`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.ExecutionPrompt.cs`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.ManagerChat.cs`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\Observation\ProcessManagerChatService.cs`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureAssemblyService.cs`
- Existing tests inspected:
  - `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessRunAutomationDispatchServiceTests.cs`
  - `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProjectWorkbenchServiceIntegrationTests.cs`
