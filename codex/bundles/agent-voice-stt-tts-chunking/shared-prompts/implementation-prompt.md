# Implementation Prompt

Implement the selected subbundle only.

Preserve the raw request: STT and TTS must handle long input as chunks; TTS must split into sentence-oriented chunks small enough to feel responsive; playback should begin while later chunks synthesize; the behavior must be shared across voice drivers and app call sites.

Use the smallest correct change:

- Keep chunking in `CanDoItAll.AgentFramework.Voice`.
- Keep OpenAI-specific HTTP behavior in `OpenAiVoiceDriver`.
- Keep Blazor components as orchestration only.
- Preserve provider validation, voice access validation, credential resolution, and the speech text preprocessor.
- Fail explicitly on failed or empty chunks.

Required proof:

- Targeted `AgentVoiceTests` run.
- Broader build/test if compilation or shared contracts require it.
- Browser validation for the normal chat voice UI when the local app host is available.

Stop and repair the bundle if any caller needs duplicated provider-specific chunking logic, if STT requires unsafe media byte slicing, or if queued playback overlaps chunks.
