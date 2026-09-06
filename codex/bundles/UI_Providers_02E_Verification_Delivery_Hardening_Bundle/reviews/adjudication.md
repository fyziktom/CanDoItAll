# Pre-implementation adjudication

Entry: 1506386afddd0ed98c4ac43911263198e352c2ba, clean local/remote branch, siblings unchanged. CodeAnalytics snap-20260906114059-f296347d: 2 projects, 281 documents, no blocking load errors; two existing module/type cycles, no project cycle.

- A1 confirmed: SourceVerification uses requested.All(current.Selected.Contains); SourceSyncService uses Replace. Exact set equality must preserve identity/revision/time/status evidence.
- A2 confirmed: TargetAttempt has Kind/Before only. LoadAsync(verify) completes any successful current-target read. Imported alias normalization currently belongs to private NormalizeAlias in the authoritative management service; move that exact policy to a non-UI owner used by both paths.
- A3 confirmed: CompleteTarget/Source and ClaimPublication precede callback awaits. RefreshButton uses the same unsafe sequence and is an actual producer, so its delivery path is included. Parent ReconcileSharedAsync currently conflates successful no-replacement with failed/stale reads; a bounded explicit completion result is required to avoid falsely acknowledging failed reconciliation.
- A4 confirmed: local Complete/Remove retain current bookkeeping; shared knownChanges/delivered grow indefinitely. Terminal cleanup must compare active attempt identity and reject late retention.
- A5 accepted: registry, source creation, sanitized API 409, ownership guards, permanent publication and no-replay verification remain intact. No registry/schema/API redesign.

Product decision: desired current authoritative state is satisfaction evidence, not historical causation. Matching unchanged before identity, semantic fields and tokens permits deliberate action; contradictory or insufficient evidence stays unresolved. Callback acknowledgement is circuit-scoped and retryable, not durable exactly-once delivery.
