# Voice Surface Inventory

| Surface | Source | Current voice wiring state | Required action |
| --- | --- | --- | --- |
| Shared chat composer | `repo://src/CanDoItAll.AgentFramework.Components/ChatWorkspacePanel.razor` | Correctly disables by `CanUseVoiceMode` and exposes voice callbacks. | No direct change expected. |
| Agents page chat | `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentChatPanel.razor.cs` | Reads `AgentVoiceAccessMetadata`, passes voice parameters, implements JS and `IAgentVoiceService` callbacks. | Use as source pattern. |
| Contextual floating chat | `repo://src/CanDoItAll.AgentFramework.Components/ContextualAgentWorkspaceWindows.razor` | Reads `AgentVoiceAccessMetadata`, passes voice parameters, implements JS and `IAgentVoiceService` callbacks. | Use as source pattern. |
| Processes Manager chat tab | `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceShell.razor` | Renders `ChatWorkspacePanel` but omits all voice parameters and callbacks. | Fix in SB02. |
| Voice service | `repo://src/CanDoItAll.AgentFramework.Voice/AgentVoiceService.cs` | Normalizes settings, requires configured provider, chunks synthesis, and uses driver factory. | Prove unchanged behavior. |
| Provider runtime voice driver | `repo://src/CanDoItAll.AgentFramework.Voice/ProviderRuntimeVoiceDriver.cs` | Dispatches typed STT/TTS requests through provider runtime pool. | Prove STT and TTS still resolve. |
| OpenAI provider voice driver | `repo://src/CanDoItAll.AgentFramework.Providers/Drivers/OpenAiProviderDriver.cs` | Implements speech-to-text and text-to-speech provider interfaces. | Prove request shape and capability registration. |
