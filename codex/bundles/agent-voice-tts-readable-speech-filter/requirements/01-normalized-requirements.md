# Normalized Requirements

| ID | Requirement | Acceptance Criteria | Owner |
| --- | --- | --- | --- |
| REQ-01 | TTS must use a speech-optimized version of assistant text. | `AgentVoiceService` sends preprocessed text to the selected TTS driver while preserving the original visible assistant message. | `01-01-tts-speech-text-sanitizer` |
| REQ-02 | Full GUIDs must be omitted from speech. | Strings matching canonical GUID format are removed from spoken text, including multiple GUIDs in one answer. | `01-01-tts-speech-text-sanitizer` |
| REQ-03 | Shortened IDs must be removed only when safely identifiable. | Hexadecimal fragments of at least seven characters followed by `...` or `…` are removed; ordinary dates, counts, and non-ellipsis words remain. | `01-01-tts-speech-text-sanitizer` |
| REQ-04 | When IDs are omitted, TTS should include a clear notice by default. | Spoken text starts with `During speech I skipped saying exact IDs, but you can find them in my text response.` when identifiers were removed and suppression is not requested. | `01-01-tts-speech-text-sanitizer` |
| REQ-05 | The skipped-ID notice must be suppressible and should not repeat every time in the same conversation. | `AgentVoiceSynthesisRequest` exposes a suppression option; normal chat, floating chat, and Cognitive Memory probe voice suppress the notice after it has already been spoken for the active session. | `02-02-chat-voice-notice-state-and-proof` |
| REQ-06 | Visible text must remain unchanged. | Chat message content remains the original assistant text with IDs; only the TTS request text changes. | `02-02-chat-voice-notice-state-and-proof` |

## Scope Exceptions

- Arbitrary shortened identifiers without ellipsis are not removed because they cannot be distinguished safely from normal words or codes.
- This bundle does not add a UI setting for global notice behavior. It adds the request-level suppression option and conversation-level caller behavior required by the user.
