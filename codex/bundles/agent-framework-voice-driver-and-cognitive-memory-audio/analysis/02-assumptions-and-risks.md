# Assumptions And Risks

## Assumptions

- Non-realtime REST transcription/synthesis is sufficient for the first production slice.
- `MediaRecorder` output can be sent as `webm` or another browser-supported audio format accepted by the selected STT provider.
- Existing provider profiles can provide the OpenAI connection/credential source for voice settings.
- Per-agent voice access belongs in the same `ConfigurationJson` metadata family as project/process/workspace/image access.
- Cognitive Memory voice confirmation can start with deterministic phrase matching and later be replaced or augmented by semantic intent classification.

## Critical Path Risks

- If the voice contracts are tied directly to OpenAI request fields, local model support will require refactoring. Mitigation: keep provider-specific fields behind driver settings and request records.
- If voice settings are only component state, chat and Cognitive Memory will diverge. Mitigation: persist general settings through a shared service and consume them from all voice surfaces.
- If the shared chat panel owns provider calls, floating chat will duplicate logic. Mitigation: keep the shared component UI-only and make parent surfaces call a voice service.
- If Cognitive Memory voice storage bypasses `RecordFeedbackAsync`, it will violate the existing review gate. Mitigation: create/confirm feedback through the probe service only.

## Validation Risks

- Unit tests cannot prove browser microphone permissions or audio playback. Browser proof must cover visible controls and JS availability; manual microphone permission can remain a residual environment note when unavailable.
- OpenAI network calls should not be required for tests. The OpenAI driver must be testable with fake `HttpMessageHandler`.
- Component tests may not execute JS. Use component tests for rendered controls and targeted browser proof for JS integration.

## Reopen Triggers

- Reopen `01-01-voice-driver-core` if driver selection falls back silently, stores raw keys, requires OpenAI-specific types in UI contracts, or cannot be tested without network access.
- Reopen `02-02-agent-settings-and-chat-audio` if normal and floating chat do not share voice controls, per-agent voice settings do not override general voice, or settings are not persisted.
- Reopen `03-03-cognitive-memory-voice-dialogue` if voice correction can mutate memory without confirmation/review, ambiguous confirmation is stored, or probe audio only works outside the Probe workbench.
- Reopen UI phases if Playwright proof shows clipped controls, overlapping text, inaccessible buttons, or hidden audio status.
