# Source Artifacts

## Local Inputs

| Artifact | Role |
| --- | --- |
| `C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-probing-workbench-repair` | Prior completed probing repair. This bundle is a dependency signal only; do not modify it for new voice scope. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Agents\Access\AgentImageGenerationAccessModels.cs` | Existing per-agent configuration metadata pattern for optional modality access. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Editors\EditorModels.cs` | Agent editor shape that reads per-agent access metadata from `ConfigurationJson`. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Catalog\AgentFrameworkWorkspaceCatalogService.Agents.cs` | Agent save path that writes per-agent access metadata into `ConfigurationJson`. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Services\AgentFrameworkModuleServiceCollectionExtensions.cs` | Agent module DI registration and MAF runtime registration. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Providers\Credentials\SecretStoreAgentProviderCredentialResolver.cs` | Existing secure provider credential resolution through stored secrets or environment/config keys. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\ChatWorkspacePanel.razor` | Shared chat UI used by normal and contextual/floating agent chat. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentChatPanel.razor.cs` | Normal agent chat orchestration. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\ContextualAgentWorkspaceWindows.razor` | Floating project/process contextual chat orchestration. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentDetailsDialog.razor` | Per-agent settings UI. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\CognitiveMemoryPage.razor` | Cognitive Memory probe workbench UI. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\CognitiveMemoryPage.razor.cs` | Probe ask/feedback orchestration. |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Advanced\CognitiveMemoryAdvancedContracts.cs` | Probe feedback action and calibration contract surface. |

## External Documentation

| Source | Role |
| --- | --- |
| `https://platform.openai.com/docs/guides/speech-to-text` | Official OpenAI STT guide. Confirms `audio/transcriptions`, supported models, and file constraints. |
| `https://platform.openai.com/docs/guides/text-to-speech` | Official OpenAI TTS guide. Confirms `audio/speech`, current TTS model, voice ids, disclosure requirement, and output formats. |
| `https://platform.openai.com/docs/api-reference/audio` | Official OpenAI Audio API reference for endpoint-level request/response behavior. |
