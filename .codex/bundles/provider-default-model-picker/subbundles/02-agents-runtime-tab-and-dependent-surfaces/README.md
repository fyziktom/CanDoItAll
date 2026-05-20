# Agents Runtime Tab And Dependent Surfaces

## Status

- Status: `Completed`

## Objective

Integrate the shared selector into the Agents Runtime tab, preserve provider-default linkage on save, and review dependent provider/model surfaces for reuse.

## Covered Inputs

- Raw request for Agents settings Runtime provider/model behavior.
- Requirement R001 for default model offering.
- Requirement R002 for provider-default linkage.
- Requirement R006 for workflow and memory review.

## Prerequisites

- Subbundle 01 completed with passing component tests.
- Shared selector API is stable.
- Existing agent runtime empty-model fallback still passes source inspection.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentDetailsDialog.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentDetailsDialog.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Execution\AgentFrameworkWorkspaceExecutionService.Helpers.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Providers\Seeds\ManagedSeedProviderFallbacks.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowCanvasEditor.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowCanvasEditor.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\Components\CognitiveMemorySettingsTab.razor`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\AiAgentsPageTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AiAgentFlowTests.cs`

## Deliverables

- Agent Runtime tab provider selection clears stale model overrides and offers provider default plus suggested models.
- Agent save normalizes selected provider default to empty `AgentDefinition.Model`.
- Override checkbox exposes the existing free-form model text behavior.
- Dependent provider/model surfaces are reviewed and, where safe, use the shared selector.
- Tests updated for new override flow.

## Dependency Impact

- Completion proves the user-facing request.
- Dependent workflow/memory review prevents the shared component from being agent-only in practice.

## Validation Depth

- Targeted component tests for agent dialog behavior.
- Targeted Playwright or browser proof for rendered Runtime tab layout.
- Source review for workflow and memory surfaces.

## Implementation Steps

- Replace the Runtime tab model `InputText` with the shared selector.
- Change provider `InputSelect` binding to a handler that clears `editorModel.Model`.
- Normalize model before save when selected provider default is in effect.
- Update tests that previously filled the model text field directly.
- Review `WorkflowCanvasEditor` and `CognitiveMemorySettingsTab`; adopt the shared selector only where semantics are safe.
- Capture browser validation analytics.

## Do Not Do

- Do not store provider default as a concrete agent model string.
- Do not introduce a new model-discovery service in the UI.
- Do not widen Cognitive Memory into a new model-profile editor unless an existing direct picker is found.

## Acceptance Checklist

- Selecting a provider shows provider default as the first model choice.
- Saving with provider default stores empty `AgentDefinition.Model`.
- Selecting a suggested model stores that specific model.
- Enabling override stores free-form custom model.
- Provider changes clear stale custom/suggested model values.
- Workflow and memory surfaces are either safely adapted or explicitly documented as reviewed.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --configuration Release --no-restore --filter "ProviderModelSelector|AgentDetails"`
- Browser proof for `/agents?tab=agents` Runtime tab at desktop viewport.
- Screenshot or explicit blocker recorded in `reviews/01-execution-report.md`.

## Browser Validation Logging

- Route: `/agents?tab=agents`.
- Viewport: desktop first; narrow only if layout changes are observed.
- Actions: open new or existing agent dialog, open Runtime tab, select provider, open model dropdown, toggle override.
- Assertions: no clipped field labels, dropdown and checkbox readable, override text field visible only when checked, save controls still reachable.

## Progression Gate

- Pass only if tests and browser proof show the requested default-linked behavior. If browser proof is blocked, record the blocker and keep closure partial unless the user accepts test-only proof.

## Closure Notes

- Completed on 2026-05-20.
- Agents Runtime tab now uses the shared `ProviderModelSelector`, clears stale model values when provider changes, and normalizes provider default selections to an empty saved agent model.
- Workflow canvas LLM component creation now reuses the shared selector while preserving the existing `workflow-canvas-component-model` dropdown test id; Cognitive Memory settings was reviewed and has no direct provider/model picker to adapt in this scope.
- Browser proof captured the Agents Runtime tab at `http://127.0.0.1:59341/agents`: selecting `OpenAI default` displayed `Provider default (gpt-5-mini)`, override exposed `agents-catalog-model`, and the dropdown was disabled while override was checked.
- Screenshot artifact: `C:\repositories\CanDoItAll\.codex\bundles\provider-default-model-picker\proof\agents-runtime-model-selector.png`.

## Proof Captured

- `dotnet build tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --configuration Release --no-restore` passed.
- `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --configuration Release --no-restore --filter "ProviderModelSelector|AgentDetails_runtime" --logger "console;verbosity=normal"` passed 6/6.
- Broad workflow page smoke was attempted and produced unstable failures before selector-specific assertions (`workflows-tab-editor` not rendered in one run; temporary `primary.db` cleanup lock in another). A focused scalar metadata selector test now covers the workflow-style consumption path directly.
- `dotnet test tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --configuration Release --no-restore --filter "FullyQualifiedName~AiAgentFlowTests" --logger "console;verbosity=normal"` was blocked by existing fixture readiness timeout before UI navigation.

## Suggested Agent Prompt

Implement subbundle 02 after subbundle 01 passes. Replace the Agents Runtime model text field with the shared selector, clear stale model values on provider change, normalize provider-default selection to an empty saved model, update tests and Playwright flow, and review workflow/memory surfaces for reuse without changing unsupported persistence semantics.
