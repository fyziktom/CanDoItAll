# Scope Inventory

## Production surfaces

| Surface | Current responsibility | Planned treatment |
| --- | --- | --- |
| `src/Modules/CanDoItAll.Modules.Workbench/Pages/Components/ProjectStructure/ProjectStructureGanttTaskEditCoordinator.cs` | Dialog orchestration plus duplicated assignment interpretation | Delegate assignment resolution and pass pricing context |
| `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureTaskDetailsService.cs` | Assignment replacement/compensation and task detail mutation | Preserve mixed-set guard; invoke authoritative pricing |
| `src/Modules/CanDoItAll.Modules.Workbench/Workbench/ProjectWorkbenchMetadata.cs` | JSON-backed task metadata and validation | Add explicit execution state with legacy `Unknown` compatibility |
| `src/Modules/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.ComponentAdapters.cs` | Canvas task dialogs, persistence, duplicated assignment interpretation | Delegate resolver/pricing; remove duplicate method |
| `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureTaskResourceCostService.cs` | All four pricing algorithms and selection | Thin exact-strategy dispatcher |
| `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureTaskCreationService.cs` | Gantt/agent task creation | Resolve authoritative new-task estimate before persistence |
| `src/Modules/CanDoItAll.Modules.Workbench/Services/WorkbenchModuleServiceCollectionExtensions.cs` | Workbench DI | Register Workbench strategies/policies |
| `src/Modules/CanDoItAll.Modules.AgentFramework/Services/Hr/HrAgentUsageAnalyticsService.cs` | Agent usage/cost history | Reuse through an Agent resource-cost strategy |
| `src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs` or actual registration owner | AgentFramework DI | Register the Agent strategy without a Workbench-to-AgentFramework reference |
| `src/App/CanDoItAll.Web/Program.cs` | UI composition | Only adjust if coordinator/strategy registration is not module-owned |

## Existing proof surfaces

- `tests/Components/CanDoItAll.Tests.Components/ProjectStructureTaskResourceCostServiceTests.cs`
- `tests/Components/CanDoItAll.Tests.Components/ProjectStructureTaskResourceCostEstimatorTests.cs`
- `tests/Components/CanDoItAll.Tests.Components/ProjectStructureTaskDetailsServiceTests.cs`
- `tests/Components/CanDoItAll.Tests.Components/ProjectStructureTaskCreationServiceTests.cs`
- `tests/Components/CanDoItAll.Tests.Components/ProjectStructureGanttTaskDialogTests.cs`
- `tests/Components/CanDoItAll.Tests.Components/ProjectStructureGanttPanelTests.cs`
- `tests/Components/CanDoItAll.Tests.Components/ProjectStructurePageTaskAssigneeCreationTests.cs`
- `tests/Components/CanDoItAll.Tests.Components/ProjectStructureWorkItemAssigneeServiceTests.cs`

## Explicitly outside this bundle

- Whole-page rewrite or decomposition of workflow/process responsibilities.
- New mobile/tablet layout work.
- CRM schema or migration changes.
- Repricing tasks that have already started.
- Replacing the single-choice assignee UI with a new multi-select editor.
