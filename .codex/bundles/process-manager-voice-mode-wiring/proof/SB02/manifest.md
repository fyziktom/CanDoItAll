# SB02 Proof Manifest

- Subbundle: `SB02 Process manager chat voice wiring`
- Status: `Completed`
- Owned requirements: R001, R002, R003, R006
- Owned raw notes: N001, N003, N004
- Semantic invariant contract: `bundle://proof/SB02/semantic-invariants.md`

## Changed File Manifest

| File | Before SHA-256 | After SHA-256 | Notes |
| --- | --- | --- | --- |
| `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceShell.razor` | `pre-existing dirty worktree before SB02; exact pre-SB02 hash not captured` | `1DDCF834B7C33B3D8F14CB6EC500B5A766253D9B9663A59D653D735F3CA2E2B6` | Manager chat now passes voice state/callbacks to `ChatWorkspacePanel`. |
| `repo://tests/CanDoItAll.Tests.Components/ProcessWorkspaceShellTests.cs` | `pre-existing dirty worktree before SB02; exact pre-SB02 hash not captured` | `B32292E044B826BA216EE7C49ACC95CBA6B4DCE460290C61833CA5C16332BD0C` | Added voice-enabled and voice-denied Manager chat tests. |
| `bundle://proof/SB02/transcripts/failing-first-manager-chat-voice.txt` | `new` | `07289327BD1DA4A2470EC9BAFF0B1B7CBB2A154DCC4A732ED35861290B0F5536` | Failing-first component proof. |
| `bundle://proof/SB02/transcripts/passing-manager-chat-voice.txt` | `new` | `7F65712C7FC43DFE1D0015C2EBE4536BFCB8F512895E805B8DABA24C3A58CB77` | Passing component proof. |
| `bundle://proof/SB02/transcripts/source-assertions.txt` | `new` | `3CA11B43368FB71B9001FA6F2F7D34DD95E176C9B9AF5586E988BBAAA47692FB` | Source assertions. |
| `bundle://proof/SB02/transcripts/anti-stub-audit.txt` | `new` | `07A2779F31BFD58E0BDAB42CEC9E7EAE53F0FFEF727C6E519036D48EA5F7EFE8` | Anti-stub audit. |

## Command Transcripts

- Failing-first: `bundle://proof/SB02/transcripts/failing-first-manager-chat-voice.txt`
- Passing: `bundle://proof/SB02/transcripts/passing-manager-chat-voice.txt`
- Source assertions: `bundle://proof/SB02/transcripts/source-assertions.txt`
- Anti-stub audit: `bundle://proof/SB02/transcripts/anti-stub-audit.txt`

## Semantic Adequacy Evidence

- Raw note owned: N001 "manager in processes page manager tab, does not enable voice mode"; N003 "specific agent has allowed voice mode"; N004 "buttons are still disabled".
- Shipped behavior: Processes Manager chat now reads `AgentVoiceAccessMetadata` for the selected manager agent, passes `CanUseVoiceMode` and voice status flags to `ChatWorkspacePanel`, and wires mode, recording, transcription, and speaking callbacks through JS interop plus `IAgentVoiceService`.
- Source proof: `bundle://proof/SB02/transcripts/source-assertions.txt` cites `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceShell.razor` methods `CanUseManagerChatVoiceMode`, `HandleManagerChatVoiceModeChangedAsync`, `ToggleManagerChatVoiceRecordingAsync`, `SpeakLatestManagerChatAssistantMessageAsync`, `ResolveManagerChatVoiceService`, `TranscribeAsync`, and `SynthesizeChunksAsync`.
- Test proof: `bundle://proof/SB02/transcripts/passing-manager-chat-voice.txt`.
- Shallow-pass trap: only set `CanUseVoiceMode=true` on the panel without checking selected manager-agent metadata or preserving the voice-denied case.
- Adversarial negative proof: `CanDoItAll.Tests.Components.ProcessWorkspaceShellTests.Manager_chat_keeps_voice_controls_disabled_for_voice_denied_manager_agent` rejects the shallow all-agents-enabled implementation.
- Semantic positive proof: `CanDoItAll.Tests.Components.ProcessWorkspaceShellTests.Manager_chat_enables_voice_controls_for_voice_allowed_manager_agent`, covered by `bundle://proof/SB02/transcripts/passing-manager-chat-voice.txt`.
- Anti-stub audit: `bundle://proof/SB02/transcripts/anti-stub-audit.txt`.

## Test Names

- Test name: `CanDoItAll.Tests.Components.ProcessWorkspaceShellTests.Manager_chat_enables_voice_controls_for_voice_allowed_manager_agent`
- Test name: `CanDoItAll.Tests.Components.ProcessWorkspaceShellTests.Manager_chat_keeps_voice_controls_disabled_for_voice_denied_manager_agent`

## Proof Claim To Code Matrix

| Capability claim | Required production source proof | Required test proof | Required negative fixture | Result |
| --- | --- | --- | --- | --- |
| Manager chat enables voice controls for a voice-allowed manager agent | `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceShell.razor` in `bundle://proof/SB02/transcripts/source-assertions.txt` | `bundle://proof/SB02/transcripts/passing-manager-chat-voice.txt` | Failing-first transcript `bundle://proof/SB02/transcripts/failing-first-manager-chat-voice.txt` | `Passed` |
| Manager chat keeps controls disabled for a voice-denied manager agent | `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceShell.razor` in `bundle://proof/SB02/transcripts/source-assertions.txt` | `bundle://proof/SB02/transcripts/passing-manager-chat-voice.txt` | `Manager_chat_keeps_voice_controls_disabled_for_voice_denied_manager_agent` | `Passed` |

## Downstream Smoke

- `bundle://proof/SB02/transcripts/passing-manager-chat-voice.txt` also includes `Manager_chat_uses_distinct_thread_per_selected_process_run`, proving the run-scoped Manager chat session behavior still passes after voice wiring.
