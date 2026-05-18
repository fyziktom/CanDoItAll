# Assumptions And Risks

## Assumptions

- The text response remains canonical and unmodified.
- TTS preprocessing may change only the spoken string passed to a TTS driver.
- Full GUIDs are safe to remove from speech.
- Truncated hexadecimal fragments followed by an ellipsis are safe enough to omit from speech because they are visibly identifier-like in the user example.
- Per-conversation suppression can be implemented at the chat component level by tracking session IDs that have already received the notice.

## Critical Path Risks

- If preprocessing happens inside `OpenAiVoiceDriver`, future local drivers will repeat the same problem. The sanitizer must be provider-neutral.
- If no metadata is returned from synthesis, chat cannot know whether the notice was actually included and may suppress future notices incorrectly.
- If shortened-ID detection is too aggressive, it could remove meaningful non-ID words from speech.

## Validation Risks

- Unit tests must include full GUIDs, several GUIDs in one sentence, truncated fragments such as `a845e5c9...`, and ordinary text that must remain unchanged.
- Browser validation cannot easily inspect audio bytes for spoken text. Browser proof should prove chat still renders and voice controls remain usable; unit tests should prove the exact spoken text transformation.
- Existing worktree changes from the prior voice repair are present and must not be reverted.

## Reopen Triggers

- Reopen subbundle 01 if a driver receives unprocessed text in tests.
- Reopen subbundle 01 if the sanitizer removes dates, ordinary counts, or non-ellipsis words.
- Reopen subbundle 02 if normal chat or floating chat announces the skipped-ID sentence on every assistant response in the same session.
- Reopen subbundle 02 if visible chat text loses IDs.
