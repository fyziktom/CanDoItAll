# Review summary

## Overall judgment

The first implementation wave made the correct large architectural move: Simple Chats now exist as a
separate product rather than an agent mode. The core model distinguishes definitions, immutable
revisions, conversations, operations, turns, invocation records, provider snapshots, and profile
generation. Production composition and API surfaces exist, PostgreSQL migrations were added, and
focused tests were written.

That foundation is worth retaining.

However, several guarantees promised by the design are currently implemented as sequences of separately
committed actions. The names “unit of work,” “profile fenced,” “durable operation,” and “audited
invocation” overstate the actual atomic or distributed behavior. These defects are especially dangerous
because ordinary happy-path tests can pass while crash, cancellation, profile-switch, retry, or
multi-instance cases corrupt state.

## Must stabilize before the next wave

- One transaction and one writable truth for conversation/transcript metadata.
- Atomic admission, completion, compensation, cancellation, and terminal reduction.
- A fence covering the entire application operation.
- Durable execution ownership independent of one process and one HTTP request.
- Bounded query paths.
- Fresh proof on synchronized source.

## Streaming conclusion

Streaming is appropriate now as part of hardening, not as a separate UI feature. It must be implemented
in two independent layers:

1. Provider-neutral incremental inference plus provider driver adapters.
2. Durable operation events delivered through existing profile-bounded SSE infrastructure.

The canonical transcript remains final-message based. Partial deltas are operational evidence and a
delivery projection; they do not become assistant transcript truth until successful finalization.
