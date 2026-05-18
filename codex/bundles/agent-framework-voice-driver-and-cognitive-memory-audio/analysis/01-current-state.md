# Current State

## AgentFramework Runtime

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\CanDoItAll.AgentFramework.Maf.csproj` owns the Microsoft Agent Framework wrapper and already references OpenAI/Azure/Ollama packages for chat/runtime work.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Services\AgentFrameworkModuleServiceCollectionExtensions.cs` registers `MafAgentRuntime`, provider credential resolution, workspace services, workflow services, and built-in workflow executors.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Providers\Credentials\SecretStoreAgentProviderCredentialResolver.cs` resolves provider API credentials from secret records, environment variables, or configuration keys and can be reused by the voice driver service.

## Agent Metadata And Settings

- Per-agent optional capability settings already live in `AgentDefinition.ConfigurationJson`.
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Agents\Access\AgentImageGenerationAccessModels.cs` is the closest metadata pattern: typed settings, JSON root, normalization, read/write methods, and default removal.
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Editors\EditorModels.cs` builds `AgentEditorModel` from `AgentDefinition`.
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Catalog\AgentFrameworkWorkspaceCatalogService.Agents.cs` writes access metadata during agent save.
- Existing general AgentFramework workflow settings are persisted as JSON in `AgentFramework_WorkflowSettings`, so general voice settings can be added without a new settings table.

## Chat UI

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\ChatWorkspacePanel.razor` is the shared chat surface for normal agent chat and contextual/floating chat.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentChatPanel.razor.cs` sends normal chat through `IAgentFrameworkWorkspaceService.SendMessageAsync`.
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\ContextualAgentWorkspaceWindows.razor` uses the same chat panel and sends contextual chat through `ExecuteRunAsync`.
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\wwwroot\js\agent-framework-download.js` shows the project already ships small AgentFramework component JS assets; voice recording/playback can follow this pattern.

## Cognitive Memory Probing

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\CognitiveMemoryPage.razor` has a Probe workbench tab with session controls, question text area, ask action, answer evidence, and feedback controls.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\CognitiveMemoryPage.razor.cs` owns probe state and calls `ICognitiveMemoryProbeService.StartAsync`, `AskAsync`, and `RecordFeedbackAsync`.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Advanced\CognitiveMemoryAdvancedContracts.cs` already defines feedback actions such as `AddCorrection`, `RequestReview`, and `CreateRegression`.
- The completed repair bundle made probe feedback review-gated. Voice storage must reuse that path and must not directly mutate canonical memory.

## OpenAI Audio Documentation Signals

- Official OpenAI STT docs identify `audio/transcriptions` and current transcription models including `gpt-4o-mini-transcribe`, `gpt-4o-transcribe`, and `gpt-4o-transcribe-diarize`.
- Official OpenAI TTS docs identify `audio/speech`, `gpt-4o-mini-tts`, built-in voices, configurable output formats, and a requirement to disclose AI-generated speech.
