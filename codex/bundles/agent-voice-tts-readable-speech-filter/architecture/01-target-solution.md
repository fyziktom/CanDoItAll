# Target Solution

## Boundary

The readable-speech filter is part of the provider-neutral AgentFramework voice service, not the OpenAI driver. This keeps OpenAI, future local TTS providers, and any additional TTS drivers aligned.

## Proposed Shape

- Add a small immutable result type such as `AgentVoiceSpeechTextPreparationResult`.
- Add a service such as `IAgentVoiceSpeechTextPreprocessor` with one implementation in `CanDoItAll.AgentFramework.Voice`.
- Extend `AgentVoiceSynthesisRequest` with a strongly typed suppression flag, for example `SuppressIdentifierOmissionNotice`.
- Extend `AgentVoiceSynthesisResult` with metadata such as `SpokenText`, `IdentifiersOmitted`, and `IdentifierOmissionNoticeIncluded`.
- `AgentVoiceService.SynthesizeAsync` calls the preprocessor before constructing `TextToSpeechDriverRequest`.
- Normal chat, floating contextual chat, and Cognitive Memory probe voice maintain a per-session set of conversations where the omission notice has already been spoken.

## Text Policy

- Remove full GUIDs using canonical GUID matching.
- Remove shortened hexadecimal ID fragments only when they end in `...` or `…`.
- Clean obvious leftover punctuation and whitespace after removals.
- Add the notice only when identifiers were omitted and suppression is false.

## Non-Goals

- Do not summarize the whole assistant answer before speech in this bundle.
- Do not mutate persisted chat content.
- Do not add OpenAI-specific prompt rewriting.
- Do not invent semantic ID detection for arbitrary shortened strings.
