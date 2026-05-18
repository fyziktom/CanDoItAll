# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| RN-01 voice works but IDs are annoying | `requirements/01-normalized-requirements.md#REQ-01` | `subbundles/01-01-tts-speech-text-sanitizer` | `AgentVoiceTests` transformation checks | Establishes why the speech text differs from visible text. |
| RN-02 improve text before TTS | `architecture/01-target-solution.md` | `subbundles/01-01-tts-speech-text-sanitizer` | Driver receives preprocessed text in unit test | Provider-neutral service layer owns the behavior. |
| RN-03 remove full GUIDs | `requirements/01-normalized-requirements.md#REQ-02` | `subbundles/01-01-tts-speech-text-sanitizer` | Full GUID omission tests | No scope exception. |
| RN-04 safe shortened ID removal | `requirements/01-normalized-requirements.md#REQ-03` | `subbundles/01-01-tts-speech-text-sanitizer` | Truncated hex + ellipsis tests and unchanged ordinary text tests | Non-ellipsis fragments are explicit exception. |
| RN-05 add skipped-ID sentence | `requirements/01-normalized-requirements.md#REQ-04` | `subbundles/01-01-tts-speech-text-sanitizer` | Notice insertion tests | Added only when identifiers are omitted. |
| RN-06 do not repeat in same conversation | `requirements/01-normalized-requirements.md#REQ-05` | `subbundles/02-02-chat-voice-notice-state-and-proof` | Chat caller state tests or source inspection plus browser smoke | Key by selected chat session or Cognitive Memory probe session where available. |
| RN-07 option to suppress sentence | `requirements/01-normalized-requirements.md#REQ-05` | `subbundles/01-01-tts-speech-text-sanitizer`, `subbundles/02-02-chat-voice-notice-state-and-proof` | Request contract/unit tests | Request-level option, not global UI toggle. |
| RN-08 user can see IDs | `requirements/01-normalized-requirements.md#REQ-06` | `subbundles/02-02-chat-voice-notice-state-and-proof` | Tests/source inspection confirm persisted content unchanged | TTS-only transformation. |
| RN-09 save TTS time/tokens | `requirements/01-normalized-requirements.md#REQ-01` | `subbundles/01-01-tts-speech-text-sanitizer` | Spoken text shorter than original in tests | No token counting required. |
