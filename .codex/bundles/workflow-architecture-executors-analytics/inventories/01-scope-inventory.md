# Scope Inventory

## Production Areas

- `src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Abstractions`
- `src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Core`
- `src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Runtime`
- `src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter`
- `src/MAF/WorkflowExecutors/CanDoItAll.AgentFramework.WorkflowExecutors.Abstractions`
- `src/MAF/WorkflowExecutors/CanDoItAll.AgentFramework.WorkflowExecutors.Core`
- `src/MAF/WorkflowExecutors/CanDoItAll.AgentFramework.WorkflowExecutors.Plugins`
- `src/MAF/WorkflowExecutors/Standard/*`
- `src/MAF/Common/CanDoItAll.AgentFramework.Core`
- `src/MAF/Common/CanDoItAll.AgentFramework.Models`
- `src/MAF/Tools/CanDoItAll.Tools.Documents`
- `src/plugins/Abstractions/CanDoItAll.Plugins.Abstractions`
- bundled Gmail, Office365, and Docker plugin projects/manifests
- `src/Modules/CanDoItAll.Modules.AgentFramework`
- `src/Modules/CanDoItAll.Modules.Workspace`
- `src/Modules/CanDoItAll.Modules.Workbench`
- `src/Modules/CanDoItAll.Modules.SchedulerPlanner`
- `src/Modules/CanDoItAll.Modules.Processes` and Processes application/runtime projects
- `src/App/CanDoItAll.Web/Api/WorkflowsApi.cs`

## Tests

- Unit: workflow foundation/core/runtime/executor/policy/plugin/manifest/tool/converter tests.
- Components: workflows page, canvas catalog/editor, settings renderer, analytics panel.
- Integration: API, persistence, plugin catalog, scheduler/project/process lifecycle.
- Playwright: `WorkflowShellSmokeTests` at 1600×1000.

## Compatibility Assets

- Persisted workflow executor IDs and settings JSON.
- Plugin manifest schema/capability/renderer keys.
- Workflow run/event database entities and migrations.
- Process executor-kind contracts and editor settings.
- Existing API responses; additive version-safe analytics fields preferred.

## Explicit Non-Goals

- Small/medium/responsive UI design.
- Separate workflow nodes for every filesystem or spreadsheet tool function.
- Arbitrary PowerShell/Python/shell execution.
- Replacing ManagedCode.MarkItDown or the existing executor policy pipeline.
- Repairing unrelated baseline cycles unless touched by this work.
