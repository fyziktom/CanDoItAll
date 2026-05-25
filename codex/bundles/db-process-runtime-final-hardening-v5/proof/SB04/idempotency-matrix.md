# SB04 process outbox idempotency matrix

## Contract

Process outbox delivery is at-least-once. Every side effect must either be naturally idempotent under its canonical key or carry an explicit stable idempotency key that is independent of the transient outbox record id.

| Side effect | Canonical idempotency key | Consumer enforcement | Duplicate proof | Residual risk |
|---|---|---|---|---|
| `SearchUpsert` | `SearchDocument.SourceType` + `SearchDocument.SourceKey` | `Infrastructure_SearchDocuments` has a unique index and `SearchIndexService.UpsertAsync(...)` updates the existing row | Existing process outbox retry tests plus source audit | Concurrent insert race can still surface as a unique violation; process outbox retry will replay the upsert. |
| `SearchDelete` | `sourceType` + `sourceKey` | `SearchIndexService.DeleteAsync(...)` returns successfully when the row is already absent | Existing delete retry test and source audit | None beyond normal DB availability. |
| `Activity` | Stable `ActivityWriteRequest.IdempotencyKey` derived from process entity/version/run, not outbox id | `Activity_Entries.IdempotencyKey` is unique and `ActivityService.RecordAsync(...)` ignores duplicates | `Duplicate_definition_save_outbox_records_reuse_stable_activity_idempotency_key` | Older payloads without an explicit key still fall back to outbox-record id to preserve backward compatibility. |
| `AutomationDispatch` | Pending outbox row with same command key, run id, step id, and normalized trigger | `EnqueueAutomationDispatchAsync(...)` dedupes tracked and persisted pending records using parsed payloads | `Duplicate_automation_dispatch_enqueue_reuses_existing_pending_command` | Completed dispatch rows are not deduped so later legitimate transitions can enqueue new work. |

## Decision

No `ProcessOutboxRecord.IdempotencyKey` column was added in this phase. Search and activity already have consumer-level keys, and automation dispatch needs pending-command dedupe rather than a global uniqueness key because completed dispatches with the same step and trigger may be legitimate later runtime events.
