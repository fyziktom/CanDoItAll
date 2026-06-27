# Target Solution

## Target Shape

- `ProcessWorkspaceShell` should mirror the proven ownership pattern from `AgentChatPanel` and `ContextualAgentWorkspaceWindows`: it owns selected-agent voice state, passes `CanUseVoiceMode`, mode/record/speaking flags, status text/tone, and callbacks into `ChatWorkspacePanel`.
- Shared rendering remains in `ChatWorkspacePanel`; no Manager-chat-only voice button markup should be introduced.
- Manager chat callbacks should call `IAgentVoiceService` and JS interop through typed `BrowserVoiceRecording` and `AgentVoiceSynthesisRequest`, just like existing agent chat surfaces.
- Provider runtime voice remains behind `AgentVoiceService -> IAgentVoiceDriverFactory -> ProviderRuntimeVoiceDriver -> IProvider*VoiceDriver`.

## Boundary Rules

- UI components do not know OpenAI URLs, audio request JSON, or provider capability names beyond typed service calls.
- Provider-specific audio behavior stays in `CanDoItAll.AgentFramework.Providers` and `CanDoItAll.AgentFramework.Voice`.
- Process runtime context building remains in the manager chat prompt helpers; voice wiring should not alter process prompt semantics.

## Expected Minimal Edit

- Add manager-chat voice fields/callbacks to `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceShell.razor`.
- Pass those fields/callbacks into the Manager chat `ChatWorkspacePanel`.
- Add focused component tests in `repo://tests/CanDoItAll.Tests.Components/ProcessWorkspaceShellTests.cs`.
- Add or confirm provider runtime tests in `repo://tests/CanDoItAll.Tests.Unit/AgentVoiceTests.cs` only if existing coverage is insufficient.
