# Target Solution

## Voice Layer

- Add shared transcription chunk models so callers can provide one audio blob or ordered audio chunks without bypassing `IAgentVoiceService`.
- Add a provider-neutral progressive TTS service method that yields ordered `AgentVoiceSynthesisResult` items.
- Split prepared speech text using a small, deterministic, sentence-aware chunker owned by the voice layer.
- Keep provider profile validation, voice access validation, credential resolution, speech text preprocessing, and effective voice selection in `AgentVoiceService`.

## Driver Boundary

- Keep `ITextToSpeechVoiceDriver.SynthesizeAsync` as the low-level one-text-to-one-audio driver operation.
- Let the shared service call the selected driver once per TTS chunk so future drivers inherit the same chunk behavior.
- Keep OpenAI request construction, response format handling, PCM wrapping, and timeout handling inside `OpenAiVoiceDriver`.

## Browser Boundary

- Make the browser bridge return ordered recording chunks from `MediaRecorder`.
- Add queue-based audio playback so Blazor can enqueue chunks without overlapping audio and without waiting for all later chunks to synthesize.
- Keep Blazor components focused on orchestration: call the shared service, enqueue returned chunks, and update status.

## Explicit Non-Goals

- Do not add a provider-specific tokenizer abstraction.
- Do not transcode audio in the browser or server.
- Do not persist audio chunks.
- Do not change visible assistant messages.
