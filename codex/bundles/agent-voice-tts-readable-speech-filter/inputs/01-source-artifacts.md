# Source Artifacts

| Artifact | Location | Notes |
| --- | --- | --- |
| User raw request | `inputs/00-original-request.md` | Preserves the follow-up request and the example response with full and truncated IDs. |
| Existing voice bundle | `C:\repositories\CanDoItAll\codex\bundles\agent-framework-voice-driver-and-cognitive-memory-audio` | Prior implementation package for voice driver, settings, chat audio, and Cognitive Memory audio. |
| AgentFramework voice contracts | `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Voice\VoiceContracts.cs` | Current synthesis request/result contracts. |
| AgentFramework voice service | `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Voice\AgentVoiceService.cs` | Provider-neutral service layer where TTS text can be normalized before driver invocation. |
| OpenAI voice driver | `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Voice\OpenAiVoiceDriver.cs` | Driver should receive already-prepared spoken text, while remaining provider-specific request code. |
| Normal agent chat | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentChatPanel.razor.cs` | Current voice mode speaks assistant content directly after send and via speak-latest. |
| Floating contextual chat | `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\ContextualAgentWorkspaceWindows.razor` | Current contextual voice mode also speaks assistant content directly. |
| Voice tests | `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\AgentVoiceTests.cs` | Target for sanitizer and service contract regression tests. |
| Chat component tests | `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ChatWorkspacePanelTests.cs` | Target for visible voice-state proof; does not replace browser smoke. |
