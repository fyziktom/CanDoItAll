# Persistence invariants

## Definitions

- Definition head points to one immutable revision.
- Revision numbers/fingerprints are unique per definition and append-only.
- A conversation pins one definition revision; later definition edits do not mutate behavior history.

## Conversations

- One writable owner stores title/lifecycle/binding.
- Message order is deterministic and unique per conversation.
- At most one active turn exists per conversation in this release.
- Archive rejects active/nonterminal work atomically.
- Delete/archive/rename use expected revision/concurrency token.

## Operations

- Operation ID is caller supplied or server generated once and stable.
- Request fingerprint is immutable.
- Same ID + same fingerprint is replay; same ID + different fingerprint is conflict.
- Terminal state is immutable except an explicitly versioned administrative correction, which is not in
  scope here.
- Succeeded implies one assistant message and no active turn.
- Failed/Cancelled implies no completed assistant message for the turn.
- RecoveryRequired implies named unresolved durable evidence.

## Attempts and usage

- Each provider dispatch attempt has a real ordinal unique per operation.
- Attempt started is persisted before or atomically with dispatch admission.
- Known usage is never discarded because the transcript turn failed.
- Timeout, cancellation, provider failure, empty response and transport interruption remain distinct.
- One deterministic reducer produces the operation outcome from attempt evidence.

## Events

- Event sequence is monotonic and unique per operation.
- Admission and terminal events commit with the corresponding state transition.
- Delta events are bounded/coalesced.
- Retention cleanup never deletes canonical operation/transcript truth.
