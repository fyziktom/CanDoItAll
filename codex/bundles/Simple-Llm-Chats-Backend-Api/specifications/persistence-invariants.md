# Persistence invariants

## Definitions

- current revision is positive for every saved definition;
- revision rows are immutable;
- revision numbers are contiguous per definition;
- settings fingerprint is deterministic;
- settings fingerprint distinguishes provider default (`null`) from every explicit effort including `None`;
- tags are normalized and unique per definition;
- archived definition cannot be updated except by an explicit future migration/admin policy.

## Product conversation

- product conversation ID equals transcript ID;
- exact definition revision exists;
- title is bounded and normalized;
- archived conversation cannot accept a new operation;
- current origin is `Application` or `Api`; unknown/external origin input is rejected until a deployment owner exists.

## Transcript

- transcript revision is monotonic;
- message sequence is monotonic and unique;
- entry IDs are globally unique;
- active turn references the final pending user entry exactly;
- assistant and user entries in one logical turn share turn ID;
- turn ID supplied by product equals operation ID;
- compensation restores provider/acceleration snapshot and removes only the pending user entry.

## Operation

- operation ID is unique;
- request fingerprint is immutable;
- only the winner of the durable dispatch claim may begin provider dispatch;
- provider-dispatch-start evidence is persisted before calling the provider;
- a retry never redispatches when persisted evidence says a provider call may already have started;
- terminal operation state is immutable except explicit reconciliation from RecoveryRequired;
- resulting assistant entry, when present, belongs to the same operation turn ID;
- cancellation requested before completion prevents semantic success.

## Invocation audit

- one or more immutable records may belong to an operation if future dispatch retry becomes externally
  visible;
- current implementation records at least one logical aggregate record;
- token counters are non-negative and checked for overflow;
- known failed usage is not discarded;
- requested and effective thinking effort remain distinguishable when recorded;
- audit may be attributed to the originating profile even when conversation completion is rejected
  after a profile switch.

## Deletion and retention

- public API archives; it does not hard-delete;
- definition revisions used by conversations are restrict-protected;
- transcript/message/operation/audit purge is a later retention-policy feature;
- database transfer preserves referential order and immutable IDs.
