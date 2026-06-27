# SB01 Semantic Invariants

## Invariant SB01-VOICE-ELIGIBILITY-INVENTORY

- Invariant ID: `SB01-VOICE-ELIGIBILITY-INVENTORY`
- Source raw note: "manager in processes page manager tab, does not enable voice mode ... specific agent has allowed voice mode ... buttons are still disabled ... provider refactor including voice drivers."
- Expected behavior: Before implementation starts, the bundle must identify the exact source owner for Manager chat voice eligibility and the provider runtime contracts that need proof.
- Disallowed shallow implementation: Assume the provider refactor is the cause and change driver selection without proving whether the Manager chat surface passes `CanUseVoiceMode`.
- Failing-first test: N/A, process/non-production source inventory phase; SB02 must provide the failing-first UI test.
- Passing test: `bundle://proof/SB01/transcripts/source-assertions.txt` with invariant `SB01-VOICE-ELIGIBILITY-INVENTORY`.
- Changed source files: No production source files changed in SB01.
- Production assertions: `repo://src/CanDoItAll.AgentFramework.Components/ChatWorkspacePanel.razor` disables controls from `CanUseVoiceMode`; `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceShell.razor` renders `ChatWorkspacePanel` without voice inputs; provider voice dispatch uses typed provider driver interfaces in `repo://src/CanDoItAll.AgentFramework.Voice/ProviderRuntimeVoiceDriver.cs`.
- Red-team negative case: A provider-only repair would not change `ChatWorkspacePanel.CanUseVoiceMode` in Manager chat and would leave buttons disabled.
- Downstream dependency check: SB02 must wire Manager chat voice inputs/callbacks; SB03 must prove STT/TTS provider runtime dispatch; SB04 must prove browser behavior.
