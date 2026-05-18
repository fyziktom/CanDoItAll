# Normalized Requirements

| Id | Requirement | Acceptance |
| --- | --- | --- |
| R001 | Long TTS input must be split in the shared voice layer before provider calls. | A service-level test proves long prepared speech text becomes multiple ordered driver requests, each below the configured chunk budget. |
| R002 | TTS chunks must prefer sentence or few-sentence boundaries. | A chunker test proves normal sentence punctuation is preserved and packed into ordered chunks without splitting every word. |
| R003 | Progressive TTS must let callers receive and enqueue each audio chunk as soon as that chunk is synthesized. | App callers use a shared progressive voice service method and browser queue playback instead of waiting for one large `SynthesizeAsync` result. |
| R004 | Existing single-shot TTS must remain valid for short text and settings sample playback. | Existing `SynthesizeAsync` and `SynthesizeSampleAsync` tests continue to pass. |
| R005 | STT must support ordered long recording chunks without server-side media byte slicing. | Browser recording returns ordered chunks and service-level STT tests prove chunks are transcribed in order and joined predictably. |
| R006 | Chunk failures must fail explicitly. | Tests cover empty chunk rejection or provider failure propagation; no skipped chunk or fallback transcript is accepted. |
| R007 | The implementation must be provider-neutral across current and future drivers. | Blazor callers do not reference OpenAI chunk limits or OpenAI endpoints; OpenAI-specific code remains inside `OpenAiVoiceDriver`. |
| R008 | The existing identifier omission speech filter must still apply before TTS chunking. | Tests prove identifier removal and notice metadata still flow through progressive TTS. |
