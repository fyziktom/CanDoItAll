# 01-tts-speech-text-sanitizer

## Status

- `Completed`

## Objective

Add provider-neutral preprocessing before TTS so spoken text omits exact IDs while preserving visible assistant text.

## Covered Inputs

- RN-01 through RN-05, RN-08, RN-09.
- REQ-01 through REQ-04 and REQ-06 service boundary.

## Prerequisites

- Bundle readiness gate passed.
- Prior voice repair work remains intact and should not be reverted.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Voice\VoiceContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Voice\AgentVoiceService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Voice\OpenAiVoiceDriver.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Voice\AgentFrameworkVoiceServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\AgentVoiceTests.cs`

## Deliverables

- A provider-neutral TTS text preprocessor in `CanDoItAll.AgentFramework.Voice`.
- Synthesis request/result metadata for notice suppression and proof.
- Service wiring so drivers receive preprocessed spoken text.
- Unit tests proving exact text transformation and driver input.

## Dependency Impact

- This is the critical foundation for chat behavior. If metadata about omitted identifiers or the notice is wrong, chat cannot safely suppress repeated notices.

## Validation Depth

- `Critical foundation`
- Unit-test proof must cover both text transformation and service-to-driver integration.

## Implementation Steps

1. Add strongly typed preprocessor result and interface.
2. Implement full GUID removal.
3. Implement conservative truncated ID removal for hex fragments followed by ellipsis.
4. Add notice insertion only when identifiers were omitted and suppression is false.
5. Clean obvious punctuation/whitespace left after identifier removal.
6. Register the preprocessor in DI.
7. Extend `AgentVoiceSynthesisRequest` and `AgentVoiceSynthesisResult` metadata.
8. Update `AgentVoiceService.SynthesizeAsync` to call the preprocessor before invoking the selected driver.
9. Add unit tests for full GUIDs, truncated IDs, unchanged normal text, notice suppression, and driver request body.

## Scope Exceptions

- Short strings without ellipsis, arbitrary hashes, and non-hex custom IDs are not removed in this subbundle.

## Do Not Do

- Do not mutate chat message content or persisted assistant text.
- Do not put generic text policy inside `OpenAiVoiceDriver`.
- Do not use a semantic model for ID detection in this phase.

## Acceptance Checklist

- Full GUIDs are absent from spoken text.
- Safe truncated hex IDs ending in ellipsis are absent from spoken text.
- Non-ID text remains readable and grammatically acceptable after cleanup.
- Notice is added by default only when identifiers were removed.
- Notice is not added when suppression is requested.
- `TextToSpeechDriverRequest.Text` contains the processed spoken text.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-build --filter FullyQualifiedName~AgentVoiceTests`
- `dotnet build CanDoItAll.slnx --no-restore /m:1`
- Execution report rows updated with test results and gate decision.

## Browser Validation Logging

- N/A for this subbundle; no browser-visible UI changes. Browser proof is required in subbundle 02.

## Progression Gate

- Downstream chat wiring may start only after unit tests prove the preprocessor, notice suppression flag, and synthesis result metadata.

## Suggested Agent Prompt

```text
Implement subbundle 01 only. Add a provider-neutral TTS text preprocessor, wire it through AgentVoiceService, return metadata for omitted identifiers and notice inclusion, and prove the transformation with AgentVoiceTests. Do not modify visible chat message content or make this OpenAI-specific.
```
