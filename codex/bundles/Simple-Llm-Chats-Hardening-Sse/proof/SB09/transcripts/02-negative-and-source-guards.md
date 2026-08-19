# Negative and source guards

## Behavioral negative proof

- Conflicting `Last-Event-ID: 2` and `?after=1` returns HTTP 400 containing
  `llm-chat.stream-cursor-invalid` before SSE framing starts.
- Disposing the first SSE response after `llm.response.delta` does not cancel or abandon the operation;
  it later succeeds and reconnects from that sequence with one provider invocation.
- A blocked provider call changes state only after explicit POST cancel; status becomes cancelled and
  the stream emits exactly one `llm.operation.cancelled` event before EOF.
- A provider failure closes after one `llm.operation.failed` event and its data does not contain the raw
  provider secret.
- Deleting retained terminal event rows emits one `stream.gap` with `cursorBeforeRetention`, a usable
  resume cursor, and `/api/llm-chat-operations/{id}` snapshot URL; GET status remains succeeded.
- Profile switch cancels the captured direct session token, makes a later read throw
  `LlmChatRuntimeProfileChangedException`, and closes the active HTTP projection without a fabricated
  terminal state.
- The generic writer stops on the first terminal envelope and does not serialize the following event.

## Sensitive-data proof

The real admission/replay/status/SSE assertions exclude:

- the system prompt `Review the supplied design carefully.`;
- the user prompt `stream reconnect proof`;
- provider credential `SB09_PROVIDER_KEY`;
- provider endpoint `provider.invalid`;
- raw provider failure secret `provider-secret-must-not-leak`.

Only normalized assistant text, stable model/usage data, and stable `llm-chat.*` failure codes are part of
the public operation/event contracts.

## Source assertions

Commands searched the changed production scope and returned:

```text
partial production owners: none
SSE projection execution ownership references: none
product stream owner Web/ASP.NET dependencies: none
sensitive SSE contract fields: none
```

The guards cover `partial class|record|struct`, dispatcher/dispatch/cancel/invocation-port references in
the stream session/replay adapter, Web/ASP.NET dependencies in the product owner, and prompt/credential/
raw-provider field markers in the public SSE contract/mapper.

Additional inspection confirms:

- POST has no 200 success declaration or terminal-failure switch;
- the SSE route calls only session open/read plus the generic response writer;
- no WebSocket, query bearer token, full-response buffer, TODO/FIXME, test-only branch, or
  `NotImplementedException` was added;
- the local signal remains an accelerator behind authoritative SQL reads.

Result: Pass.
