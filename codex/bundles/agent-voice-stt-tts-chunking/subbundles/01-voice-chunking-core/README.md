# Voice chunking core

## Status

- `Completed`

## Objective

- Add shared voice-layer chunking contracts and behavior for long TTS text and ordered STT recording chunks.

## Success Criteria

- Long TTS text is split into ordered sentence-oriented chunks before driver calls.
- Progressive TTS synthesis yields one audio result per chunk.
- Ordered STT chunks are transcribed through the configured driver and joined in order.
- Existing single-shot short TTS and STT behavior remains covered.

## Covered Inputs

- N001 / R001 / R005: long STT and TTS must split into chunks.
- N002 / R001: TTS chunks must be well below the provider cap.
- N003 / R002: TTS chunks should follow sentence or few-sentence boundaries.
- N005 / R007: chunking must be generic in the voice service/driver layer.
- R006 / R008: chunk failures must fail explicitly and identifier omission must still apply.

## Prerequisites

- Bundle prepared-stage validation has passed.
- Previous voice bundles are completed and treated as current baseline.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Voice\VoiceContracts.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Voice\AgentVoiceService.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Voice\OpenAiVoiceDriver.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Voice\AgentVoiceModels.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\AgentVoiceTests.cs

## Deliverables

- Shared transcription chunk model accepted by `AgentVoiceTranscriptionRequest`.
- Progressive TTS service API that yields ordered synthesis results.
- Deterministic sentence-aware TTS text chunker.
- Service-level STT aggregation over ordered chunks.
- Unit tests for chunking, progressive synthesis, STT aggregation, and existing OpenAI driver behavior.

## Dependency Impact

- Subbundle 02 depends on this phase so every UI caller can use a shared progressive API instead of duplicating splitting logic.
- Weak proof here would invalidate UI playback because UI could appear progressive while still relying on provider-specific or caller-local chunking.

## Validation Depth

- Critical foundation, unit-test and compile-proof.

## Implementation Steps

1. Add or extend voice contracts for ordered STT chunks and progressive TTS results.
2. Implement provider-neutral sentence-aware TTS chunking in the voice layer.
3. Update `AgentVoiceService` to aggregate ordered STT chunks and yield TTS chunks progressively.
4. Preserve existing single-shot `SynthesizeAsync` behavior for short/sample usage.
5. Add unit tests covering chunking boundaries, progressive synthesis request order, STT transcript order, and failure behavior.

## Scope Exceptions

- Exact token counting is not included; conservative character-based chunking is used until a tokenizer abstraction exists.
- Server-side splitting of one compressed audio blob is not included because it can create invalid media.

## Do Not Do

- Do not add Blazor-specific chunking.
- Do not call OpenAI APIs directly from UI code.
- Do not silently skip failed or empty chunks.
- Do not change visible chat message content.

## Acceptance Checklist

- TTS chunker returns more than one chunk for long text and keeps each chunk under the budget.
- Progressive synthesis calls the driver once per chunk in order.
- STT aggregation transcribes multiple chunks in order and joins non-empty transcripts predictably.
- Existing `AgentVoiceService_Synthesize_UsesPreparedSpeechText` still proves identifier preprocessing.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter AgentVoiceTests`
- Record command outcome in `reviews/01-execution-report.md`.

## Browser Validation Logging

- N/A. This subbundle changes service/driver behavior only.

## Progression Gate

- Passed. Targeted voice unit tests passed and the execution report records subbundle 01 as safe for UI integration.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Add provider-neutral STT/TTS chunking to the shared voice layer, preserve existing short-call behavior, add targeted `AgentVoiceTests`, update the execution report, and stop before UI work unless the subbundle 01 progression gate honestly passes.
```
