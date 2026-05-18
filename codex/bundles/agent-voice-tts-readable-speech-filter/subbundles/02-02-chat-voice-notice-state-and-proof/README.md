# 02-chat-voice-notice-state-and-proof

## Status

- `Completed`

## Objective

Wire the TTS omission-notice suppression into normal agent chat and floating contextual chat so each conversation hears the notice at most once while visible text remains unchanged.

## Covered Inputs

- RN-05 through RN-08.
- REQ-05 and REQ-06.

## Prerequisites

- `01-01-tts-speech-text-sanitizer` closure gate passed.
- `AgentVoiceSynthesisRequest` has a suppression option.
- `AgentVoiceSynthesisResult` indicates whether the omission notice was included.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentChatPanel.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\ContextualAgentWorkspaceWindows.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\CognitiveMemoryPage.razor.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\AgentVoiceTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ChatWorkspacePanelTests.cs`

## Deliverables

- Normal chat tracks per-session skipped-ID notice state.
- Floating contextual chat tracks per-session skipped-ID notice state.
- Cognitive Memory probe voice tracks per-session skipped-ID notice state.
- Voice callers pass the suppression option into `AgentVoiceSynthesisRequest`.
- Voice callers mark a session as already notified only when synthesis reports that the notice was actually included.
- Proof that visible chat text remains unchanged and the audio path is still usable.

## Dependency Impact

- Depends on subbundle 01 metadata. Weak proof here would regress user-facing voice mode by either repeating the notice too often or hiding it entirely.

## Validation Depth

- `UI/runtime wiring with component-test and browser-proof`

## Implementation Steps

1. Add per-session notice-state tracking to normal chat.
2. Reset or key notice state correctly when selected session changes.
3. Pass `SuppressIdentifierOmissionNotice` based on the active session state.
4. After synthesis, record the session as notified only when `IdentifierOmissionNoticeIncluded` is true.
5. Apply the same pattern to floating contextual chat.
6. Apply the same pattern to Cognitive Memory probe voice.
7. Add or update tests where feasible.
8. Run targeted component tests and a browser smoke on `/agents?tab=chat`.

## Scope Exceptions

- This subbundle does not add a visible UI toggle. The requested suppression option is implemented as a synthesis request option and caller behavior.

## Do Not Do

- Do not alter persisted chat content.
- Do not suppress the notice globally across unrelated chat sessions.
- Do not hide IDs from the visible assistant response.

## Acceptance Checklist

- First spoken answer in a session that contains omitted IDs can include the notice.
- Later spoken answers in the same session suppress the notice.
- A different session can receive its own first notice.
- No notice is spoken when no IDs were omitted.
- Visible assistant text still contains IDs.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-build --filter FullyQualifiedName~AgentVoiceTests`
- `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --no-build --filter FullyQualifiedName~ChatWorkspacePanelTests`
- `dotnet build CanDoItAll.slnx --no-restore /m:1`
- Browser smoke on `http://127.0.0.1:5044/agents?tab=chat` or an available replacement port, with route, viewport, actions, and assertions recorded in the execution report.

## Browser Validation Logging

- Route: `/agents?tab=chat`
- Viewport: large desktop at least `1280x900`; narrower pass not required unless layout changes.
- Actions/assertions: page loads without Blazor console errors; chat voice controls remain available for voice-enabled agents or component proof covers enabled control state.
- Screenshot/evidence: snapshot or screenshot path under `evidence/`, plus execution report row.
- Visual review questions: controls are readable, not clipped, and no new layout overlap was introduced.

## Progression Gate

- Final closure may start only after normal chat, floating chat, and Cognitive Memory probe voice are wired to the suppression option and targeted tests/browser smoke pass.

## Suggested Agent Prompt

```text
Implement subbundle 02 only. Use the synthesis metadata from subbundle 01 to suppress the skipped-ID notice after it has already been spoken in the active conversation. Apply the same behavior to normal and floating chat. Preserve visible text and capture component plus browser proof.
```
