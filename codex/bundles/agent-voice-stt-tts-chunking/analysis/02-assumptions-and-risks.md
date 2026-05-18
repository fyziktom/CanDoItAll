# Assumptions And Risks

## Assumptions

- TTS chunking should happen after the existing speech text preprocessor so identifier omission remains a shared voice-layer behavior.
- The chunk target should be much smaller than the OpenAI 2,000-token limit; a conservative character budget is the smallest provider-neutral approach currently available.
- STT chunking should use browser-produced recording chunks. Server-side byte slicing of WebM, WAV, or MP4 would create invalid media in common cases.
- Existing voice consumers are limited to normal agent chat, contextual floating chat, Cognitive Memory voice dialogue, and settings sample playback based on repository search.

## Critical Path Risks

- `01-voice-chunking-core` is the critical foundation. If the shared service contracts are wrong, every UI caller would either duplicate chunking or keep waiting for full audio.
- If progressive TTS chunks cannot preserve ordered playback, `02-progressive-playback-integration-and-closure` must reopen the core API instead of adding local UI workarounds.
- If STT browser chunks are not accepted by the provider, the STT chunk implementation must be marked partial and the follow-up must move chunking closer to a format-aware recorder/transcoder.

## Validation Risks

- Live OpenAI STT/TTS calls may be unavailable because credentials and provider profiles are environment-specific.
- Browser microphone permission may block live STT proof even when the app host is available.
- Unit tests can prove service order and request shape, but they cannot prove actual audio quality or OpenAI latency.

## Reopen Triggers

- Reopen subbundle 01 if any UI caller still needs to know OpenAI token limits or manually split TTS text.
- Reopen subbundle 01 if tests show `SynthesizeAsync` and progressive synthesis disagree about preprocessing or voice selection.
- Reopen subbundle 01 if multi-chunk STT silently drops an empty or failed chunk.
- Reopen subbundle 02 if queued playback overlaps chunks, leaves old audio playing across new requests, or only starts after all chunks are synthesized.
