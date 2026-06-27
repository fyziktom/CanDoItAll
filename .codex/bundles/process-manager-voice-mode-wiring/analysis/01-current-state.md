# Current State

## Source Observations

- `repo://src/CanDoItAll.AgentFramework.Components/ChatWorkspacePanel.razor` owns the visible voice buttons. It disables record/speak/mode when `CanUseVoiceMode` is false and exposes `VoiceModeChanged`, `VoiceRecordingToggled`, and `SpeakLatestAssistantMessageRequested` callbacks.
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentChatPanel.razor.cs` reads `AgentVoiceAccessMetadata.Read(selectedAgent.ConfigurationJson)` into `CanUseSelectedAgentVoiceMode`, passes it to `ChatWorkspacePanel`, and implements recording, transcription, and speech playback through `IAgentVoiceService` plus JS interop.
- `repo://src/CanDoItAll.AgentFramework.Components/ContextualAgentWorkspaceWindows.razor` follows the same shared pattern for contextual agent windows.
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceShell.razor` renders Manager chat with `ChatWorkspacePanel` but currently passes no voice parameters or callbacks. Because `CanUseVoiceMode` defaults to false, the shared component disables the buttons even when the selected manager agent allows voice mode.
- `repo://src/CanDoItAll.AgentFramework.Voice/ProviderRuntimeVoiceDriver.cs` adapts `AgentVoiceService` to the provider runtime pool and dispatches STT/TTS through typed provider driver interfaces.
- `repo://src/CanDoItAll.AgentFramework.Providers/Drivers/OpenAiProviderDriver.cs` implements `IProviderSpeechToTextDriver` and `IProviderTextToSpeechDriver`; existing tests cover OpenAI request construction and runtime dispatch.

## Existing Test Signals

- `repo://tests/CanDoItAll.Tests.Components/ChatWorkspacePanelTests.cs` verifies shared voice controls render when `CanUseVoiceMode=true`, but it does not cover the Processes Manager chat owner.
- `repo://tests/CanDoItAll.Tests.Components/ProcessWorkspaceShellTests.cs` covers Manager chat loading and process workspace behavior, but the inspected section does not include voice mode assertions.
- `repo://tests/CanDoItAll.Tests.Unit/AgentVoiceTests.cs` already covers voice access metadata, sample synthesis, runtime driver dispatch, unsupported provider capability failure, chunking, and transcription chunks.

## Dirty Worktree Notice

- The worktree had pre-existing modifications before this task, including `ProcessWorkspaceShell.razor`, `LiveProcessesDashboard.razor`, application services, projections, and tests. Execution must avoid reverting unrelated user changes and must review any touched modified file before editing.
