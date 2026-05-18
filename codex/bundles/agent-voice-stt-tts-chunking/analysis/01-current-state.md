# Current State

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Voice\VoiceContracts.cs` exposes single-result `IAgentVoiceService.SynthesizeAsync` and single-blob `AgentVoiceTranscriptionRequest`.
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Voice\AgentVoiceService.cs` validates provider access, preprocesses TTS speech text, then calls exactly one driver synthesis request and returns exactly one audio payload.
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Voice\OpenAiVoiceDriver.cs` enforces the 25 MB OpenAI audio transcription limit and sends one `/audio/speech` request per TTS call.
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\wwwroot\js\agent-framework-voice.js` records one media blob, converts it to one base64 value, and plays one audio payload at a time.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentChatPanel.razor.cs`, `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\ContextualAgentWorkspaceWindows.razor`, and `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\CognitiveMemoryPage.razor.cs` each wait for a full `SynthesizeAsync` result before invoking browser playback.
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\AgentVoiceTests.cs` already covers OpenAI request construction, browser-playable content types, PCM wrapping, provider validation, and speech text preprocessing.
- The previous `agent-framework-voice-driver-and-cognitive-memory-audio` bundle completed the base voice driver and UI wiring; the previous `agent-voice-tts-readable-speech-filter` bundle completed TTS-only text sanitization. This bundle extends that shared voice layer.
