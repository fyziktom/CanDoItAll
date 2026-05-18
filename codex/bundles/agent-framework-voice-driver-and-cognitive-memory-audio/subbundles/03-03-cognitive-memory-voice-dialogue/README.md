# 03 Cognitive Memory Voice Dialogue

## Status

Completed

- Subbundle status: `Completed`

## Objective

Add voice interaction to the Cognitive Memory Probe workbench, including spoken ask/answer flow and confirmation-gated memory correction feedback.

## Covered Inputs

- Voice communication with Cognitive Memory probing.
- Spoken "wait while processing" status.
- Spoken interpretation of how memory understood a proposed memory.
- Confirmation phrases such as "yes", "ok", and "this is good, store it".
- More interactive audio control during probing.

## Prerequisites

- `01-01-voice-driver-core` closure gate passed.
- `02-02-agent-settings-and-chat-audio` closure gate passed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\CognitiveMemoryPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\CognitiveMemoryPage.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\CognitiveMemoryPage.razor.css`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Advanced\CognitiveMemoryAdvancedContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Advanced\CognitiveMemoryAdvancedServices.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CognitiveMemoryAdvancedServicesTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CognitiveMemoryPageTests.cs`

## Deliverables

- Probe workbench audio controls for voice ask and spoken answer/status.
- Voice correction mode that transcribes a memory-worthy statement, prepares `AddCorrection` feedback, speaks the interpretation, and waits for confirmation.
- Deterministic confirmation classifier with affirmative, negative, and ambiguous outcomes.
- Confirmation-gated call to `ICognitiveMemoryProbeService.RecordFeedbackAsync`.
- Tests for confirmation classification and no-store-on-ambiguous behavior.
- Browser proof for the Probe workbench audio controls and dialogue states.

## Dependency Impact

- Final closure depends on proving this phase does not violate Cognitive Memory review gates.

## Validation Depth

- Unit tests for phrase classification and service/UI state transitions that can run without OpenAI.
- Component/browser proof for the Probe workbench controls, processing status, interpreted correction state, confirmation state, and cancellation/ambiguous state.
- Inspect saved feedback/review item path when correction is confirmed.

## Implementation Steps

1. Add probe voice state to `CognitiveMemoryPage`.
2. Reuse the AgentFramework voice service and browser JS helpers.
3. Add voice ask flow: record, transcribe into question, ask probe, synthesize answer/status.
4. Add correction flow: record, transcribe into correction text, set feedback action to `AddCorrection`, speak interpretation/status, wait for confirmation.
5. Add confirmation classifier and only call `RecordFeedbackAsync` on clear affirmative confirmation.
6. Add tests and browser proof.

## Scope Exceptions

- Full semantic confirmation via LLM/local classifier is deferred. Deterministic phrases must be explicit and tested.
- Direct canonical memory writes are not allowed and are not part of this phase.

## Do Not Do

- Do not bypass `ICognitiveMemoryProbeService`.
- Do not store on ambiguous confirmation.
- Do not create a separate voice settings system for Cognitive Memory.

## Acceptance Checklist

- [x] Probe voice ask can populate and send a probe question.
- [x] Probe answer/status can be spoken through configured TTS.
- [x] Voice correction prepares review-gated feedback and explains the interpretation before storing.
- [x] Clear affirmative confirmation saves feedback.
- [x] Negative or ambiguous confirmation does not save feedback and keeps the operator in control.
- [x] Browser proof shows controls are readable and usable in the Probe workbench.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-restore --filter FullyQualifiedName~CognitiveMemoryVoice`
- `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --no-restore --filter FullyQualifiedName~CognitiveMemoryPage`
- Browser proof logged in `reviews/01-execution-report.md` with screenshots under `evidence/`.

## Browser Validation Logging

- Record route, viewport, actions, assertions, screenshots, and result for `/cognitive-memory?projectId=<guid>` Probe workbench audio states.

## Progression Gate

- Final validation cannot start until confirmed voice correction creates probe feedback through the review-gated path and ambiguous confirmation is proven not to store.

## Suggested Agent Prompt

Implement Cognitive Memory voice probing by reusing the AgentFramework voice service. Keep corrections review-gated, speak processing/interpretation states, and require explicit confirmation before saving feedback.
