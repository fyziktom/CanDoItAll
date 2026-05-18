# Structured Input

## Raw Notes

| Raw Note ID | Exact requirement or observation | Normalized requirement IDs |
| --- | --- | --- |
| RN-01 | Agent chat voice works, but long IDs are annoying when spoken. | REQ-01 |
| RN-02 | Improve text before sending it to TTS. | REQ-01, REQ-02 |
| RN-03 | Remove IDs if present, usually GUIDs. Full GUIDs are easy to find. | REQ-02 |
| RN-04 | Shortened IDs are less certain and should only be removed if safe. | REQ-03 |
| RN-05 | Add a sentence that during speech exact IDs were skipped and visible in text response. | REQ-04 |
| RN-06 | If already told in that conversation, do not tell it every time. | REQ-05 |
| RN-07 | Provide an option to suppress adding the sentence. | REQ-05 |
| RN-08 | Visible text should still contain the IDs for the user to read. | REQ-06 |
| RN-09 | Saving TTS time and tokens matters. | REQ-01, REQ-02 |

## Assumptions

- The sanitizer belongs in the provider-neutral voice layer, not only in the OpenAI driver, so future local TTS drivers inherit the same behavior.
- "Conversation" maps to the active chat session where available.
- The notice should only be added when at least one identifier was omitted.
- Shortened identifiers are treated as safe to remove only when they look like hexadecimal ID fragments followed by `...` or `…`.
