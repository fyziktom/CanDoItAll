# SB01 Proof Manifest

- Subbundle id: `SB01-manager-audio-auto-speak-parity`
- Status: `Completed`
- Owned requirements: R001, R002
- Raw notes: N001, N002, N003
- Semantic invariant contract: `bundle://proof/SB01/semantic-invariants.md`

## Changed File Manifest

| File | Before SHA-256 | After SHA-256 |
| --- | --- | --- |
| `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceShell.razor` | `9e3402ad9835b655c8120aa46ee0f2a502e1bf17d81807fd7cb85eecc8b56f57` | `630d417e9243ac19e701a9b298eee66a42fd7ae41918cae006ff1e1e1f683d5a` |
| `repo://tests/CanDoItAll.Tests.Components/ProcessWorkspaceShellTests.cs` | `4dec0917f0fdbd1180ce141b4e0de948b41b1b8f2df27cc23e4f7a2db6f355de` | `f6406621d78fdb7e143e7d5bdf27b0813f4aafce755229da3b11ee34ae04b5cd` |

## Command Transcripts

- Failing-first transcript: `bundle://proof/SB01/transcripts/failing-first-auto-speak.txt`
- Passing transcript: `bundle://proof/SB01/transcripts/passing-auto-speak-test.txt`
- Anti-stub audit transcript: `bundle://proof/SB01/transcripts/anti-stub-audit.txt`

## Source Assertions

- Source proof: `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceShell.razor` calls `SpeakManagerChatTextAsync(result.AssistantMessage.Content)` after a successful Manager response when voice mode is enabled.
- Test name: `Manager_chat_auto_speaks_assistant_response_when_voice_mode_is_enabled`
- Test proof: `bundle://proof/SB01/transcripts/passing-auto-speak-test.txt`
- Adversarial negative proof: `bundle://proof/SB01/transcripts/failing-first-auto-speak.txt`
- Anti-stub audit: No production stubs found in `bundle://proof/SB01/transcripts/anti-stub-audit.txt`.

## Browser Or Host Proof

- Downstream host proof: `bundle://proof/SB03/transcripts/passing-5032-health.txt`
