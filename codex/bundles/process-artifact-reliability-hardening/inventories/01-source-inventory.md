# Source Inventory

| Area | Source | Why it matters |
| --- | --- | --- |
| Bundle skills | `repo://codex/skills/bundles/candoitall-bundle-preparation/SKILL.md` | Defines required bundle/subbundle structure. |
| Bundle workflow | `repo://codex/skills/bundles/candoitall-bundle-workflow/SKILL.md` | Defines execution and proof workflow. |
| Process module README | `repo://src/CanDoItAll.Modules.Processes/README.md` | States Processes module purpose and ownership. |
| Process module project | `repo://src/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj` | Shows dependencies on AgentFramework and related modules. |
| Process service | `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesService.cs` | Shows role/workflow references inside process definitions. |
| Dispatcher | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` | Main execution path, direct-agent path, workflow-backed path, transitions. |
| Recovery | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.CompletionArtifactRecovery.cs` | Missing artifact recovery, manager resolver, recovery directive. |
| Projection | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs` | Artifact projection paths and current completion signals. |
| Models | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Models.cs` | Dispatch candidate and artifact expectation state. |
| Integration tests | `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs` | Existing regression suite to extend. |
