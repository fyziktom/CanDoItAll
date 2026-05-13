# Agent Workflow And Project Secret Reference Surfaces

## Status

- `Completed`

## Objective

- Add strongly typed secret references to agent settings, workflow HTTP executor settings, and project-structure metadata paths.

## Covered Inputs

- `N006`, `N007`, `N008`, `N009`, `N012`
- `R007`, `R008`, `R009`

## Prerequisites

- `SB02` closure gate passed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Agents\AgentModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Editors\EditorModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Workflows\WorkflowExecutorModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Workflows\BuiltInWorkflowExecutors.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowCanvasEditor.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowExecutorCanvasCatalog.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`

## Deliverables

- Agent model/editor fields for allowed secret references.
- Runtime enforcement that an agent cannot request a non-allowed secret.
- Workflow HTTP settings with selected secret id, destination header name, and value format.
- HTTP executor resolves the selected secret at execution time and applies it to the request.
- Project-structure node metadata can reference secrets by id/name/purpose only.

## Dependency Impact

- `SB04` depends on these typed surfaces to render pickers instead of ad hoc JSON fields.
- `SB05` final proof depends on workflow HTTP execution and agent permission tests.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Add typed secret reference models with id, name snapshot, purpose, and optional header behavior.
2. Update agent editor serialization/deserialization.
3. Extend workflow HTTP settings, create-dialog defaults, inspector UI, and executor behavior.
4. Add project-structure metadata/reference helper methods needed by the picker dialog.
5. Add unit tests for serialization, permissions, and HTTP header secret application.

## Scope Exceptions

- Deep process-template designer integration may be represented by shared secret-reference models if the current process editor has no explicit secret field.

## Do Not Do

- Do not store raw secret values in agent configuration or workflow settings.
- Do not hide missing-secret or unauthorized-secret errors.
- Do not add broad new authorization infrastructure beyond the typed allow-list needed here.

## Acceptance Checklist

- [x] Agent configuration can list allowed secret references.
- [x] Workflow HTTP fetch can select an API key secret and header behavior.
- [x] Executor applies the secret only during the request.
- [x] Project-structure metadata stores reference-only information.

## Proof Captured

- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "SecretVault|WorkflowExecutor|AgentSecret|ProjectStructureSecret"`: passed, 26/26.
- `dotnet build src\CanDoItAll.AgentFramework.Maf\CanDoItAll.AgentFramework.Maf.csproj`: passed.
- `/agents/workflows` browser pass opened the workflow editor and HTTP executor toolbox; HTTP secret selector controls are wired in the inspector source, but Playwright could not instantiate an HTTP node through the current canvas click/drag path.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "WorkflowExecutor|AgentSecret|ProjectStructure"`
- Browser analytics row for `/agents?tab=workflows` if UI is changed here.

## Browser Validation Logging

- Route: `/agents?tab=workflows`
- Viewport: `1600x900`, plus narrower width if settings layout changes.
- Evidence: open workflow HTTP executor settings, select a secret, save/validate workflow, screenshot open-state controls.

## Progression Gate

- Passed. Agent/workflow/project surfaces persist references only, and runtime proof blocks unauthorized or missing secret use.

## Suggested Agent Prompt

```text
Implement SB03 only. Add typed secret references to agent/workflow/project surfaces, prove HTTP secret resolution, and record browser analytics for workflow UI if rendered.
```
