# SB02 Semantic Invariants

## Invariant SB02-MANAGER-CHAT-VOICE-ENABLED

- Invariant ID: `SB02-MANAGER-CHAT-VOICE-ENABLED`
- Source raw note: "the specific agent has allowed voice mode in its setting. the buttons are still disabled."
- Expected behavior: When the selected Processes Manager chat agent has `AgentVoiceAccessSettings.CanUseVoiceMode=true`, the shared `ChatWorkspacePanel` voice mode, record, and speak buttons render enabled and audio mode can be toggled.
- Disallowed shallow implementation: Hard-code enabled buttons for Manager chat or bypass the selected agent's voice-access metadata.
- Failing-first test: `bundle://proof/SB02/transcripts/failing-first-manager-chat-voice.txt`.
- Passing test: `bundle://proof/SB02/transcripts/passing-manager-chat-voice.txt`.
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceShell.razor` SHA-256 `1DDCF834B7C33B3D8F14CB6EC500B5A766253D9B9663A59D653D735F3CA2E2B6`; `repo://tests/CanDoItAll.Tests.Components/ProcessWorkspaceShellTests.cs` SHA-256 `B32292E044B826BA216EE7C49ACC95CBA6B4DCE460290C61833CA5C16332BD0C`.
- Production assertions: Manager chat uses `AgentVoiceAccessMetadata.Read(managerChatAgent.ConfigurationJson)`, passes `CanUseVoiceMode` to `ChatWorkspacePanel`, and wires voice callbacks to JS interop plus `IAgentVoiceService`.
- Red-team negative case: A constant `CanUseVoiceMode=true` would enable voice for denied agents; `Manager_chat_keeps_voice_controls_disabled_for_voice_denied_manager_agent` rejects that.
- Downstream dependency check: SB04 must prove the same enabled controls in a rendered browser.

## Invariant SB02-MANAGER-CHAT-VOICE-DENIED

- Invariant ID: `SB02-MANAGER-CHAT-VOICE-DENIED`
- Source raw note: The raw note requires the specific allowed agent to work, not all manager agents to bypass access controls.
- Expected behavior: Manager chat leaves voice controls disabled when the selected manager agent does not allow voice mode.
- Disallowed shallow implementation: Ignore `AgentVoiceAccessSettings.CanUseVoiceMode` and enable voice for every Manager chat agent.
- Failing-first test: N/A, behavior was already denied by the previous default; this is an adversarial negative regression test.
- Passing test: `bundle://proof/SB02/transcripts/passing-manager-chat-voice.txt`.
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceShell.razor` SHA-256 `1DDCF834B7C33B3D8F14CB6EC500B5A766253D9B9663A59D653D735F3CA2E2B6`; `repo://tests/CanDoItAll.Tests.Components/ProcessWorkspaceShellTests.cs` SHA-256 `B32292E044B826BA216EE7C49ACC95CBA6B4DCE460290C61833CA5C16332BD0C`.
- Production assertions: `ResetManagerChatVoiceIfUnavailable` clears active voice state when the selected manager agent cannot use voice mode.
- Red-team negative case: Voice-denied manager-agent fixture renders disabled voice controls.
- Downstream dependency check: SB04 browser proof must use a voice-enabled manager agent and must not weaken the denied-agent component proof.
