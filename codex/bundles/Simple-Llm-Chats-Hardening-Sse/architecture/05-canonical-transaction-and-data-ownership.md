# Canonical transaction and data ownership

## Canonical ownership

Recommended canonical owner:

- `LlmChatConversationRow` owns conversation identity, definition/revision binding, title, lifecycle,
  timestamps and summary revision.
- transcript message rows own ordered message content.
- operation/active-turn columns or a dedicated turn row own in-flight state.
- any generic `LlmChatTranscriptRow` root is either removed or demoted to a strict persistence detail
  with no duplicate independently writable title/lifecycle metadata.

The executor may choose another single owner only in SB01 and must record the reason. Two writable title
columns are not acceptable.

## Atomic commands

### Create conversation

One transaction creates:

- conversation/binding;
- initial system message or equivalent pinned prompt snapshot when applicable;
- initial revision counters;
- audit/outbox event if the application publishes one.

Failure commits none.

### Rename conversation

One transaction validates expected revision and changes the canonical title once. Derived read models
are updated in the same transaction or asynchronously from an outbox; they are never independently
authoritative.

### Turn admission

One transaction:

- resolves operation ID/fingerprint;
- checks conversation lifecycle and no active turn;
- appends pending user message;
- creates active turn;
- marks operation admitted/queued;
- stores cancellation generation baseline;
- writes admission event.

### Success finalization

One transaction:

- verifies claim epoch and profile generation;
- verifies no winning cancellation;
- appends exactly one assistant message;
- clears active turn;
- records usage/attempt summary;
- transitions operation to Succeeded;
- writes terminal event.

### Failure/cancellation/compensation

One transaction removes or marks only the exact admitted user turn according to locked semantics,
clears active turn, records known usage/evidence and sets the deterministic terminal or
RecoveryRequired state.
