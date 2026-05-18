# Implementation Prompt

Execute the current subbundle only. Preserve the visible assistant text, add TTS-only spoken-text preprocessing, keep the implementation provider-neutral, and update the execution report with proof before closing the subbundle.

Required behavior:

- Remove canonical GUIDs from spoken TTS text.
- Remove truncated hexadecimal ID fragments only when they end with `...` or `…`.
- Add `During speech I skipped saying exact IDs, but you can find them in my text response.` only when identifiers were omitted and suppression is not requested.
- Expose a request-level suppression option.
- Normal and floating chat suppress the notice after it has already been included for the active conversation.

Do not modify persisted chat message content or hide IDs from visible text.
