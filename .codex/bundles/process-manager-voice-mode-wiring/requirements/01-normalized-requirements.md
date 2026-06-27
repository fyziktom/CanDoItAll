# Normalized Requirements

| ID | Requirement | Observable Acceptance |
| --- | --- | --- |
| R001 | The Processes page Manager chat tab must honor the selected manager agent's `AgentVoiceAccessSettings.CanUseVoiceMode`. | A component test renders Manager chat with a voice-enabled manager agent and finds `chat-voice-mode-button`, `chat-voice-record-button`, and `chat-voice-speak-button` not disabled. |
| R002 | Manager chat must use the same typed voice flow as normal agent chat: browser JS recording, `IAgentVoiceService.TranscribeAsync`, send prompt, and `IAgentVoiceService.SynthesizeChunksAsync` for speak/playback. | Tests or source assertions show Manager chat callbacks call JS interop and `IAgentVoiceService`; no direct provider endpoint calls are introduced in UI. |
| R003 | Voice-disabled agents must remain disabled explicitly. | A negative component test renders a manager agent with `CanUseVoiceMode=false` and finds the voice buttons disabled. |
| R004 | Provider runtime STT and TTS wiring must remain connected after the provider refactor. | Unit tests prove `ProviderRuntimeVoiceDriver` dispatches STT/TTS through `IProviderSpeechToTextDriver` and `IProviderTextToSpeechDriver` and fails explicitly for unsupported capabilities. |
| R005 | The final result must include real rendered-app proof. | Playwright opens `/processes`, activates Manager chat, verifies enabled controls for a voice-enabled manager agent, toggles audio mode, and records screenshot plus transcript artifacts. |
| R006 | The fix must be minimal and preserve existing component boundaries. | Changed-file manifest shows only focused manager chat voice wiring, tests, and proof/bundle files unless provider proof finds a separate driver defect. |
