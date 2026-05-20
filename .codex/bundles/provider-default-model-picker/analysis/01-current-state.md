# Current State

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentDetailsDialog.razor` renders Runtime provider as `InputSelect` and model as a plain `InputText` with `data-testid="agents-catalog-model"`.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentDetailsDialog.razor.cs` resolves the displayed runtime policy model with `editorModel.Model` falling back to `SelectedRuntimeProvider.DefaultModel`.
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Execution\AgentFrameworkWorkspaceExecutionService.Helpers.cs` calls `ManagedSeedProviderFallbacks.ResolveModel(agent, provider, ...)`.
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Providers\Seeds\ManagedSeedProviderFallbacks.cs` resolves empty `agent.Model` to `provider.DefaultModel`, so the runtime already supports provider-default linkage.
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Providers\ProviderModels.cs` exposes `ProviderProfile.DefaultModel` and `ProviderProfile.SuggestedModels`.
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Providers\ProviderServices.cs` normalizes provider suggested models and applies Ollama model creation results back into `SuggestedModels`.
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Providers\MafAgentRuntime.ProviderHealth.cs` fetches Ollama model tags and returns suggested models through health checks.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowCanvasEditor.razor` already has provider/model option UI for new workflow LLM components, but it is local markup and not a reusable provider selector.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\Components\CognitiveMemorySettingsTab.razor` lists default/allowed providers but currently does not expose a per-profile model picker in that settings tab.
- Components MCP guidance selected `FormField`, `FormRow`, `DropDown`, `CheckBox`, and `TextBox` as the shared BaseLib controls for the selector.
