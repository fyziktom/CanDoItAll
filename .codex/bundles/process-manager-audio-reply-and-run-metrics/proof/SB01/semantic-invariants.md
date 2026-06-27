# SB01 Semantic Invariants

## Invariant SB01-AUTO-SPEAK

- Invariant ID: `SB01-AUTO-SPEAK`
- Source raw note: N001, N002, N003
- Expected behavior: A successful Manager chat send with Manager voice mode enabled must pass the assistant response text into the existing speech synthesis path without requiring the user to press the read button.
- Disallowed shallow implementation: Enabling the microphone or leaving only the manual read button wired is insufficient.
- Failing-first test: `bundle://proof/SB01/transcripts/failing-first-auto-speak.txt`
- Passing test: `bundle://proof/SB01/transcripts/passing-auto-speak-test.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceShell.razor` before `9e3402ad9835b655c8120aa46ee0f2a502e1bf17d81807fd7cb85eecc8b56f57` after `630d417e9243ac19e701a9b298eee66a42fd7ae41918cae006ff1e1e1f683d5a`; `repo://tests/CanDoItAll.Tests.Components/ProcessWorkspaceShellTests.cs` before `4dec0917f0fdbd1180ce141b4e0de948b41b1b8f2df27cc23e4f7a2db6f355de` after `f6406621d78fdb7e143e7d5bdf27b0813f4aafce755229da3b11ee34ae04b5cd`.
- Production assertions: `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceShell.razor` uses the same `SpeakManagerChatTextAsync` path as manual read, guarded by `managerChatVoiceModeEnabled` and non-empty assistant content.
- Red-team negative case: `bundle://proof/SB01/transcripts/failing-first-auto-speak.txt` rejects the old no-auto-speak condition by failing when the production call exists.
- Downstream dependency check: `bundle://proof/SB03/transcripts/passing-5032-health.txt` proves the rebuilt host serves the Manager page dependencies.
