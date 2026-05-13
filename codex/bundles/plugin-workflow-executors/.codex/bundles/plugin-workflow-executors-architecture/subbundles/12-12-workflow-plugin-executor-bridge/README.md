# Workflow Plugin Executor Bridge

## Status

- `Ready`

## Objective

- Bridge plugin executors into workflow catalog/canvas/invoker with connection selection.

## Success Criteria

- Enabled plugin executors appear in the workflow executor catalog with source/availability metadata.
- Workflow nodes can select plugin executor and plugin connection reference.
- Plugin node settings are validated by schema.
- Plugin executor invocation uses existing workflow timeout/retry/result semantics through bridge.
- No hard-coded plugin UI branches are introduced.

## Covered Inputs

- `R002`
- `R006`
- `R013`
- `R015`
- `R016`
- `R017`
- `R024`
- `R025`
- `R029`
- `R032`
- `F001`
- `F002`
- `F003`
- `F004`
- `F014`
- `F015`

## Prerequisites

- `SB10,SB11,SB02`

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowExecutorContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Workflows\WorkflowExecutorModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Workflows\WorkflowModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowDefinitionValidator.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Workflows\BuiltInWorkflowExecutorDescriptors.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Services\AgentFrameworkModuleServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\WorkflowsApi.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowCanvasEditor.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowCanvasEditor.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowExecutorCanvasCatalog.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj`

## Deliverables

- PluginWorkflowExecutorBridge implementing or contributing to IWorkflowExecutor.
- Plugin executor catalog provider integration.
- Workflow validator support for plugin executor availability, connection requirements, and node settings schema.
- Workflow UI catalog and node editor support for plugin executors.
- Tests for enabled/disabled/missing connection/invalid settings/plugin invocation.

## Dependency Impact

- Shop, OAuth2, final proof, and future SaaS plugin bundles depend on this MVP being coherent and bounded.

## Validation Depth

- `Plugin MVP implementation`

## Implementation Steps

1. Design bridge from IPluginWorkflowExecutor to IWorkflowExecutor while preserving WorkflowExecutorInvoker behavior.
2. Add plugin executor descriptors to the workflow executor catalog only when installed/enabled/compatible.
3. Add validation for required plugin connection and node-level settings.
4. Update workflow UI executor catalog display for plugin source/availability.
5. Update workflow node settings model only if needed, preserving saved workflow compatibility.
6. Render plugin executor node settings through the renderer host/schema fallback.
7. Add bridge tests for execution context, capability context, timeout/retry, disabled plugin, and invalid settings.
8. Capture browser proof for selecting a plugin executor in workflow editor.

## Scope Exceptions

- Only bundled/static plugin executors are required.
- Remote shop executors remain metadata-only until SB15/future bundles.

## Do Not Do

- Do not bypass IWorkflowExecutorInvoker.
- Do not add special-case plugin executor branches to WorkflowCanvasEditor.
- Do not store plugin connection secrets in WorkflowNodeSettings.

## Acceptance Checklist

- [ ] Enabled plugin executors appear in the workflow executor catalog with source/availability metadata.
- [ ] Workflow nodes can select plugin executor and plugin connection reference.
- [ ] Plugin node settings are validated by schema.
- [ ] Plugin executor invocation uses existing workflow timeout/retry/result semantics through bridge.
- [ ] No hard-coded plugin UI branches are introduced.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "PluginWorkflowExecutor|WorkflowExecutor"`
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "WorkflowPlugin"`
- Browser screenshot of workflow executor catalog showing plugin executor and node settings.

## Browser Validation Logging

- Required. Open workflow editor, add/select plugin executor, verify settings renderer and connection selector, capture screenshots.

## Progression Gate

- Passed only when plugin executors are first-class workflow executors without duplicating runtime or UI logic.

## Suggested Agent Prompt

```text
Implement SB12 only.

Work outcome-first:
- Read this subbundle README, the root README, and reviews/01-execution-report.md.
- Verify prerequisites and exact source references before editing.
- Preserve the listed scope boundaries.
- Make the smallest correct change set.
- Capture required proof.
- Update reviews/01-execution-report.md.
- Stop if the progression gate cannot honestly pass.
```
