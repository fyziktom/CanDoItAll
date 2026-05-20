# 03 Curator UI And Voice

## Status

- State: `Completed`
- Critical foundation: `UI-critical`

## Objective

Add a proper Cognitive Memory Curator tab that supports fluent text chat, runtime mode selection, bidirectional voice, and visible captured memory-improvement state.

## Covered Inputs

- `R-001`, `R-003`, `R-009`
- Raw notes: fluent talk, proper UI, voice both ways, too many manual forms.

## Prerequisites

- Subbundle 01 closure gate passed.
- Subbundle 02 closure gate passed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\CognitiveMemoryPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\CognitiveMemoryPage.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\CognitiveMemoryPage.razor.css`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\Components\CognitiveMemoryProbeWorkbenchTab.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\wwwroot\js\agent-framework-voice.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Voice\AgentVoiceService.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CognitiveMemoryPageTests.cs`

## Deliverables

- Curator tab in Cognitive Memory.
- Mode selector for `Agent` and `Direct LLM`.
- Transcript display for curator turns.
- Text composer that sends through curator service.
- Voice input using existing JS recording and `IAgentVoiceService.TranscribeAsync`.
- Spoken response using `IAgentVoiceService.SynthesizeChunksAsync` and existing JS playback queue.
- Captured improvement list/status for the latest turn.
- Component tests and browser proof.

## Dependency Impact

- Subbundle 04 depends on this UI proof for final closure.
- If UI cannot call the shared backend path, runtime/capture tests alone do not satisfy the user request.

## Validation Depth

- Component tests plus browser validation.
- UI is user-facing and must be visually checked.

## Implementation Steps

1. Query/inspect shared component options before adding custom layout markup.
2. Add `Curator` tab and component/state.
3. Implement send, mode switch, refresh, voice record, and speak handlers.
4. Keep voice capture path identical to text send after transcription.
5. Render captured improvements and warnings in the transcript panel.
6. Add component tests.
7. Run browser proof on `/cognitive-memory`.

## Scope Exceptions

- Actual microphone/provider audio proof may be recorded as blocked if credentials or host permissions are unavailable; UI/service paths still need test proof.

## Do Not Do

- Do not build another manual approval form.
- Do not put UI persistence logic in Razor components.
- Do not add broad custom CSS when existing components express the layout.

## Acceptance Checklist

- Curator tab is visible and usable.
- Mode switch is visible and typed.
- Text send updates transcript/status.
- Voice controls render and call existing JS/service handlers.
- Captured improvement state is visible.
- Large and narrow viewport checks pass without overlapping text.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter CognitiveMemory`
- Browser route `/cognitive-memory`, viewport `1600x900`, screenshot and assertions.
- Browser route `/cognitive-memory`, narrow viewport, screenshot and assertions.

## Browser Validation Logging

- Record route `/cognitive-memory`.
- Record viewport, tab click actions, mode switch, composer/voice button assertions, screenshot paths, and pass/fail result.

## Progression Gate

- Pass only when the rendered UI proves the conversation and voice controls are available.
- Captured-improvement state must be visible.

## Suggested Agent Prompt

Implement subbundle 03 only. Add the Curator tab and voice-enabled conversation UI using existing components and the curator service. Capture browser proof and update the execution report.
