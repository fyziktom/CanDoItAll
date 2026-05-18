# Agent Voice STT TTS Chunking

This bundle coordinates the follow-up improvement for AgentFramework voice input and output: long speech-to-text input must be accepted as ordered recording chunks, and long text-to-speech output must be split into smaller speech chunks so playback can begin while later chunks are still being synthesized.

## Profile

- `feedback`

## Mission

Make STT and TTS chunk handling a shared AgentFramework voice capability instead of a one-off UI workaround. TTS should split prepared text on sentence boundaries into small ordered chunks, synthesize them through the configured driver, and let consumers enqueue playback as chunks arrive. STT should preserve ordered browser recording chunks and transcribe them through the same service/driver path instead of sending one oversized blob.

## Outcome Contract

- Requested outcome: generic voice service and driver-facing chunk support for long STT and TTS, with progressive TTS playback used by normal agent chat, floating contextual chat, and Cognitive Memory voice dialogue.
- Hard constraints: preserve provider-neutral contracts, keep OpenAI as the first driver, do not silently fall back on failed chunks, keep visible assistant text unchanged, preserve the existing identifier-omission speech preprocessor, and avoid provider-specific chunking in Blazor components.
- Evidence required before closure: targeted unit tests for text chunking, progressive synthesis, multi-chunk transcription, driver request shape, and a clean targeted test run. Browser validation is required for the chat voice playback route when a local app host is available.
- Known blockers or explicit scope exceptions: exact token counting is not implemented because the current driver layer does not own provider tokenizers; the chunk budget uses conservative character limits below the OpenAI 2,000-token limit. Live OpenAI playback and live microphone proof require configured credentials and browser microphone permission.

## Bundle Layout

- `inputs/` raw request, artifacts, and structured input
- `analysis/` current state, assumptions, and risks
- `requirements/` normalized, testable requirements
- `architecture/` target solution and important boundaries
- `plan/` execution order and dependencies
- `traceability/` requirement-to-bundle mapping
- `shared-prompts/` reusable implementation and QA prompts
- `subbundles/` numbered execution-ready workstreams
- `reviews/` bundle self-review and execution report

## Recommended Execution Order

1. `subbundles/01-voice-chunking-core`
2. `subbundles/02-progressive-playback-integration-and-closure`
3. Final validator and raw-note closure audit.

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Ready after validation`
- Execution status: `Completed`
- Subbundle gate review: `Passed`
- Final closure gate: `Passed`
- Browser validation analytics: `Passed`
