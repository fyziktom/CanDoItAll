# Retention, backpressure and cleanup

## Event coalescing

Do not write one database row per token. Use validated defaults such as:

- coalesce until a short time window, minimum character count, or sentence/newline boundary;
- cap one event payload;
- flush before terminal state;
- never reorder deltas.

The exact defaults are options and tests must use deterministic time.

## Bounds

Define and validate:

- maximum assistant aggregate characters/UTF-8 bytes;
- maximum durable delta events per operation;
- maximum operation duration;
- maximum queued age;
- claim lease/heartbeat;
- SSE replay capacity;
- event retention period;
- terminal operation retention and cleanup batch size.

A bound violation creates a typed terminal failure and preserves known usage.

## Cleanup

- Only terminal operations are eligible for event compaction/deletion.
- Retention never deletes canonical assistant transcript or usage summary.
- Cleanup is batched, indexed and profile-scoped.
- Active claims/turns/events are never removed.
- Cursor gap behavior remains explicit after cleanup.
- Database transfer either includes retained event/audit data according to locked policy or clearly
  excludes it as transient operational history; the decision is documented and tested.
