# Current State

## Voice Pipeline

- Normal agent chat calls `IAgentVoiceService.SynthesizeAsync` from `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentChatPanel.razor.cs` with the full assistant message content.
- Floating contextual chat calls the same service from `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\ContextualAgentWorkspaceWindows.razor`.
- `AgentVoiceService` resolves TTS settings, provider, driver, and effective voice, then passes raw request text to `TextToSpeechDriverRequest`.
- `OpenAiVoiceDriver` is provider-specific and should not own generic spoken-text policy.

## Current Gap

The visible assistant answer is appropriate for text because IDs are actionable there, but the exact same answer is also sent to TTS. This makes spoken audio unnecessarily long and irritating when answers contain GUIDs, project IDs, run IDs, provider IDs, or truncated identifiers.

## Existing Test Surface

- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\AgentVoiceTests.cs` already covers voice access metadata, OpenAI driver request shaping, content type handling, provider validation, and confirmation classification.
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ChatWorkspacePanelTests.cs` covers shared chat voice controls, but the concrete TTS request text is owned by parent chat components.

## Implementation Implication

Add a provider-neutral TTS preprocessor in `CanDoItAll.AgentFramework.Voice`, wire it into `AgentVoiceService`, and extend synthesis request/result metadata enough for callers to suppress the "IDs skipped" notice after the first notice in a chat session.
