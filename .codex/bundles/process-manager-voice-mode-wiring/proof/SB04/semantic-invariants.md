# SB04 Semantic Invariants

## Invariant SB04-BROWSER-MANAGER-VOICE-CONTROLS

- Invariant ID: `SB04-BROWSER-MANAGER-VOICE-CONTROLS`
- Source raw note: "Do real demos/tests with voice mode."
- Expected behavior: In a real browser on the Processes page Manager chat tab, the selected voice-enabled manager agent renders enabled voice mode, record, and speak buttons, and toggling audio mode shows visible status feedback.
- Disallowed shallow implementation: Rely on bUnit-only proof or screenshots without DOM assertions for enabled controls.
- Failing-first test: `bundle://proof/SB02/transcripts/failing-first-manager-chat-voice.txt`.
- Passing test: `bundle://proof/SB04/transcripts/playwright-manager-chat-voice.txt` and `bundle://proof/SB04/transcripts/final-test-run.txt`.
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceShell.razor` SHA-256 `1DDCF834B7C33B3D8F14CB6EC500B5A766253D9B9663A59D653D735F3CA2E2B6`; `repo://tests/CanDoItAll.Tests.Components/ProcessWorkspaceShellTests.cs` SHA-256 `B32292E044B826BA216EE7C49ACC95CBA6B4DCE460290C61833CA5C16332BD0C`; `repo://tests/CanDoItAll.Tests.Unit/AgentVoiceTests.cs` SHA-256 `F8C2AC81D3150C88E3ECD2E40E05FC1CA136322C71A5E66B9E0AC5909F2A9A51`.
- Production assertions: Playwright DOM assertions show `chat-voice-mode-button`, `chat-voice-record-button`, and `chat-voice-speak-button` exist and have `disabled=false`; audio mode toggle produces `Audio on`.
- Red-team negative case: SB02 voice-denied component test proves Manager chat does not enable voice for agents without voice access.
- Downstream dependency check: Final affected component and provider runtime tests pass in `bundle://proof/SB04/transcripts/final-test-run.txt`.
