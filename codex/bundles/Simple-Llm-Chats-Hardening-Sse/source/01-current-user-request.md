# Current user request

## Literal objective

Review the completed first phase of Simple LLM Chats on branch `simple-chats`, determine whether it was
implemented correctly, and prepare a follow-up hardening bundle for every deficiency that must be fixed
before the next development phase.

The follow-up must also prepare streaming through Server-Sent Events because:

- chat answers may be long;
- local LLMs may produce tokens slowly;
- external applications need progressive responses;
- UI integration will later need the same backend stream.

## Sequencing constraints

- Complete and prove backend/API hardening before any UI integration.
- Keep shared-component isolation and UI integration in later separate bundles.
- Do not repeatedly run the entire test suite during implementation.
- Use focused tests during each subbundle.
- Run the broad stable gate only at final closure.
- Deliver this coordination artifact as a ZIP bundle.

## Interpretation locked by this bundle

“Streaming” means true incremental provider output where supported, not polling a completed response
or slicing a final string after completion. SSE is the HTTP delivery protocol; it is not the provider
protocol and must not own canonical transcript state.
