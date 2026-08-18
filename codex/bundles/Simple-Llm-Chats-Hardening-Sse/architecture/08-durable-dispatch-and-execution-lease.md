# Durable dispatch and execution lease

## Admission and dispatch separation

HTTP admission commits `Accepted/Queued` and returns. A dispatcher reads durable queued work and claims
it via CAS.

## Claim fields

At minimum:

- `ExecutionOwnerId`
- `ExecutionEpoch` or opaque claim token
- `ClaimedAtUtc`
- `HeartbeatAtUtc`
- `LeaseExpiresAtUtc`
- operation revision/concurrency token

Every mutation after claim verifies owner and epoch. A stale worker cannot commit after another worker
takes over.

## Heartbeat and expiry

- Heartbeat interval and lease duration are options with validated bounds.
- Provider streams update heartbeat without writing one row per token.
- Expiry is evidence that the owner is no longer trusted, not evidence that dispatch did not happen.
- Reclaim policy consults attempt/first-delta/terminal evidence before choosing redispatch versus
  RecoveryRequired.

## Cancellation

- Cancel endpoint commits durable cancellation first.
- The owning process observes it via local notification plus bounded polling/heartbeat checks.
- Local CTS is an optimization, not canonical truth.
- A second app instance can request cancellation.
- Finalization checks durable cancellation generation in the same transaction.

## Host lifecycle

Startup reconciliation scans only bounded eligible states using indexed queries. Shutdown requests local
cancellation but does not fabricate terminal state; durable reconciliation handles interruption.
