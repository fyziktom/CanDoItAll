# SSE transcript evidence

## Environment

- Commit:
- Host/OS:
- Database/profile ID and generation:
- Dependency mode:
- Provider double/protocol fixture:
- Operation ID:
- Conversation ID:

## Admission

```http
POST ...
202 Accepted
```

Record elapsed time to 202 and prove provider completion happened later.

## Initial stream

```text
id:
event:
data:
```

Capture accepted/claimed/admitted, at least two real deltas, completion and terminal event.

## Reconnect

- Last received ID:
- Reconnect header/query:
- First resumed ID:
- Duplicates:
- Missing semantic text:
- Second provider dispatch count:

## Disconnect

- Disconnect point:
- Operation state after disconnect:
- Reconnected terminal result:

## Gap

- Retention/replay setup:
- `stream.gap` event:
- Recovery cursor/status snapshot:

## Profile switch

- Old profile/generation:
- New profile/generation:
- Stream close evidence:
- Old-generation commit rejection evidence:

## Redaction review

- [ ] No system prompt
- [ ] No user prompt
- [ ] No credentials/headers/endpoints
- [ ] No raw provider error
- [ ] No hidden reasoning
