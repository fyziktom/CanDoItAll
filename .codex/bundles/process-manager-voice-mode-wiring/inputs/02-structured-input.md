# Structured Input

## Core Objective

- Fix the Processes page Manager chat tab so voice mode controls are enabled and functional when both general voice settings and selected manager-agent voice access allow voice mode.

## Success Criteria

- A manager agent with `CanUseVoiceMode=true` makes Manager chat render enabled voice toggle, record, and speak controls.
- A manager agent with `CanUseVoiceMode=false` still leaves those controls disabled and reports the existing explicit voice-denied behavior if invoked.
- Manager chat voice recording, transcription, and speech playback use `IAgentVoiceService` and the shared browser JS interop path, matching `/agents` chat and contextual agent windows.
- Provider runtime voice dispatch still resolves speech-to-text and text-to-speech capabilities through typed provider drivers after the provider refactor.
- Browser validation opens the Processes Manager chat tab and verifies enabled controls in a real rendered app.

## Hard Constraints

- Preserve shared component boundaries: `ChatWorkspacePanel` stays reusable and receives voice state/callbacks from the owner.
- Do not bypass provider runtime abstractions with direct OpenAI endpoint calls from UI code.
- Do not silently fall back when voice settings, provider profile, or provider capability is wrong; tests must keep explicit failure behavior.
- Keep edits minimal and local to the voice eligibility/wiring defect unless provider proof reveals a real driver defect.
- Follow existing Blazor component library usage; do not replace the shared panel with raw bespoke controls.

## Allowed Side Effects

- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceShell.razor` may gain manager-chat voice state and callbacks.
- Focused component/unit tests may be added or adjusted under `repo://tests/CanDoItAll.Tests.Components` and `repo://tests/CanDoItAll.Tests.Unit`.
- Bundle proof files may be written under `bundle://proof`.
- No provider API contract changes unless SB03 proves a real broken connection.

## Source Artifacts

- Raw request: `bundle://inputs/00-original-request.md`.
- Manager chat surface: `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceShell.razor`.
- Shared voice controls: `repo://src/CanDoItAll.AgentFramework.Components/ChatWorkspacePanel.razor`.
- Known working chat implementations: `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentChatPanel.razor.cs` and `repo://src/CanDoItAll.AgentFramework.Components/ContextualAgentWorkspaceWindows.razor`.
- Voice service/provider runtime: `repo://src/CanDoItAll.AgentFramework.Voice/AgentVoiceService.cs`, `repo://src/CanDoItAll.AgentFramework.Voice/ProviderRuntimeVoiceDriver.cs`, `repo://src/CanDoItAll.AgentFramework.Providers/Drivers/OpenAiProviderDriver.cs`.

## Input Coverage Signals

- N001: "manager in processes page manager tab, does not enable voice mode" maps to Manager chat UI control state.
- N002: "general ... voice mode ... sample works fine" maps to preserving `AgentVoiceService.SynthesizeSampleAsync` and general settings behavior.
- N003: "specific agent has allowed voice mode in its setting" maps to `AgentVoiceAccessMetadata.Read(...).CanUseVoiceMode`.
- N004: "buttons are still disabled" maps to `ChatWorkspacePanel.CanUseVoiceMode` input in the Manager chat tab.
- N005: "more generic trouble ... multiple places ... providers including voice drivers" maps to provider runtime driver integration and shared surface audit.
- N006: "real demos/tests with voice mode" maps to component/unit transcripts plus Playwright browser proof.

## Dependency And Sequencing Signals

- SB01 must establish the exact eligibility path before implementation starts; otherwise the fix could enable buttons for the wrong reason.
- SB02 depends on SB01 and unlocks browser proof because the visible bug is in Manager chat.
- SB03 can run in parallel conceptually but must close before final closure because provider-driver breakage would make enabled controls misleading.
- SB04 depends on SB02 and SB03 because browser proof should validate the final integrated behavior, not a partial UI-only patch.

## Validation Expectations

- Component failing-first proof must show Manager chat currently renders disabled voice buttons for a voice-allowed manager agent.
- Passing proof must show Manager chat supplies `CanUseVoiceMode=true` and invokes voice callbacks.
- Unit proof must show `ProviderRuntimeVoiceDriver` dispatches STT/TTS through typed provider drivers and still rejects unsupported provider capability explicitly.
- Browser proof must navigate to the Processes Manager chat tab, locate `chat-voice-mode-button`, `chat-voice-record-button`, and `chat-voice-speak-button`, and verify they are enabled for a seeded or selected voice-enabled manager agent.

## Evidence Contract

- `bundle://proof/SB02/transcripts/failing-first-manager-chat-voice.txt`
- `bundle://proof/SB02/transcripts/passing-manager-chat-voice.txt`
- `bundle://proof/SB03/transcripts/provider-voice-runtime-tests.txt`
- `bundle://proof/SB04/browser/processes-manager-chat-voice-desktop.png`
- `bundle://proof/SB04/browser/processes-manager-chat-voice-mobile.png` if layout is affected.
- `bundle://proof/SB04/transcripts/playwright-manager-chat-voice.txt`
- `bundle://proof/SB04/transcripts/anti-stub-audit.txt`

## UI Validation Strategy

- First pass: large browser viewport at `/processes`, open/select the Manager chat tab, inspect enabled state and click voice-mode toggle.
- Screenshot review questions: are the Manager chat controls visible, enabled, aligned in the composer, and not clipped by the tab layout; does the status badge update after toggling audio mode.
- Narrower pass: only required if SB02 changes layout or introduces new visible controls; otherwise component proof is enough for unchanged shared panel layout.

## Browser Validation Analytics

- SB02 logs component-level proof in the execution report; no browser row closes SB02 alone.
- SB04 logs route `/processes`, viewport, exact actions, DOM assertions for disabled/enabled state, screenshot paths, and result in `reviews/01-execution-report.md`.

## Working Assumptions

- General voice sample working means `AgentVoiceService.SynthesizeSampleAsync` and at least one TTS provider profile are already configured in the user's environment.
- The selected Manager chat agent is represented by `AgentDefinition.ConfigurationJson` and the same `AgentVoiceAccessMetadata` used by normal chat.
- Tests can use in-memory/test drivers for provider proof instead of making live OpenAI network calls.

## Primary Risks

- Enabling the buttons without wiring callbacks would produce clickable controls that do nothing.
- Reusing stale manager-agent records after an agent settings save could still leave voice disabled until reload; SB02 must verify reload or selection updates the selected `AgentDefinition`.
- Provider runtime can pass sample TTS while STT or capability selection is broken; SB03 must test both speech-to-text and text-to-speech.
- Browser microphone permissions may block actual recording; the fallback proof must document the environment gap and still prove JS interop/callback path through tests.
