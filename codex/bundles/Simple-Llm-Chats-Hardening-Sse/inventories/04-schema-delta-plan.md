# Schema delta plan

Likely schema changes include:

- removal/demotion of duplicate transcript-root metadata;
- operation claim owner/epoch/heartbeat/expiry;
- durable cancellation generation/time;
- explicit attempt ordinals and outcomes;
- operation event journal with `(OperationId, Sequence)` unique key;
- indexes for queued/claimable/reconcilable operations;
- indexes for conversation keyset paging and bounded message retrieval;
- retention/cleanup indexes;
- optional outbox/high-water fields.

## Migration rules

- Do not edit the applied baseline migration.
- Generate one or more append-only migrations with descriptive names.
- Preserve existing data through deterministic backfill.
- Fail closed on ambiguous duplicate metadata; do not silently choose conflicting title/state.
- Update model snapshot.
- Run migration bootstrap tests.
- Run `has-pending-model-changes`.
- Exercise upgrade from a database created at the feature-branch schema.
- Exercise database transfer/export-import according to the locked event/audit inclusion policy.
