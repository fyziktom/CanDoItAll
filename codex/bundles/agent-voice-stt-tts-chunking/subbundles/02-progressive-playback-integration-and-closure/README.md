# Progressive playback integration and closure

## Status

- `Completed`

## Objective

- Wire progressive TTS playback and ordered STT recording chunks through every known app voice caller, then close the bundle with proof.

## Success Criteria

- Normal agent chat, floating contextual chat, and Cognitive Memory voice dialogue use progressive TTS synthesis and queued browser playback.
- Browser recording returns ordered chunks and all known STT callers send those chunks through the shared service.
- Existing settings sample playback remains functional.
- Execution report contains gate results, browser analytics or explicit blocker, raw-note closure, and residual risks.

## Covered Inputs

- N001 / R005: long STT recording chunks are used by app callers.
- N004 / R003: TTS playback starts while later chunks are still being synthesized.
- N005 / R007: all current app voice consumers use shared voice-layer behavior.

## Prerequisites

- `subbundles/01-voice-chunking-core` status is `Completed`.
- Subbundle 01 targeted unit tests pass.
- No open gate issue says the progressive service contract is unstable.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\wwwroot\js\agent-framework-voice.js
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentChatPanel.razor.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\ContextualAgentWorkspaceWindows.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\CognitiveMemoryPage.razor.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentVoiceSettingsPanel.razor
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\AgentVoiceTests.cs

## Deliverables

- Browser voice bridge supports ordered recording chunks and queued audio playback.
- App voice callers submit STT chunks when available.
- App voice callers enqueue progressive TTS chunks as they arrive.
- Execution report and bundle status are synchronized with proof.

## Dependency Impact

- This is the final integration phase. Weak proof leaves the raw request only partially solved because shared chunking would exist but users would still wait for full audio in the UI.
- Browser queue correctness protects every current and future progressive TTS caller.

## Validation Depth

- End-to-end regression and closure with UI/browser-proof when host is available.

## Implementation Steps

1. Update `agent-framework-voice.js` to emit recording chunks and queue audio playback sequentially.
2. Update normal agent chat to pass STT chunks and enqueue progressive TTS chunks.
3. Update floating contextual chat to pass STT chunks and enqueue progressive TTS chunks.
4. Update Cognitive Memory voice dialogue to pass STT chunks and enqueue progressive TTS chunks.
5. Keep settings sample playback short and single-shot unless compile or behavior changes require the progressive method.
6. Run targeted tests and browser validation or record a concrete blocker.
7. Update all execution report rows and final raw-note closure.

## Scope Exceptions

- Live OpenAI synthesis/transcription may be blocked by missing credentials.
- Live microphone proof may be blocked by browser permissions.

## Do Not Do

- Do not put TTS splitting logic in Blazor components.
- Do not change chat layout unless required by status rendering.
- Do not add persistent audio storage.
- Do not silently ignore playback queue errors if they are observable during validation.

## Acceptance Checklist

- Repository search shows no app voice caller still waits on full `SynthesizeAsync` for chat/dialogue voice playback.
- Repository search shows STT callers pass recording chunks when the browser returns them.
- Browser queue playback does not overlap chunks by construction.
- Execution report rows are complete.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter AgentVoiceTests`
- Build or broader test command if compile coverage requires it.
- Browser validation on the normal agent chat voice route with a large desktop viewport when a local host is available.
- Screenshot path or explicit local-host/credential/microphone blocker recorded in `reviews/01-execution-report.md`.

## Browser Validation Logging

- Route: normal AgentFramework chat voice UI route.
- Viewport: large desktop viewport first; narrower follow-up only if UI markup/CSS changes.
- Actions/assertions: navigate to route, verify voice controls render, verify no status text overflow after code changes, and capture screenshot if app host is available.
- Screenshots: `codex/bundles/agent-voice-stt-tts-chunking/evidence/agent-chat-voice-desktop.png` when host is available.
- If host, credentials, or microphone permission blocks live proof, record the blocker explicitly in the browser analytics row.

## Progression Gate

- Passed. Known app voice callers use shared progressive chunking, targeted tests and publish build passed, and browser validation completed on the published local host.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Wire progressive voice chunks through every app voice caller, update the browser queue bridge, run targeted tests, capture browser proof or a concrete blocker, then update final bundle closure rows.
```
