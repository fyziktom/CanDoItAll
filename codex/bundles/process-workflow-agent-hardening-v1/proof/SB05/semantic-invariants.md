# SB05 Semantic Invariants

1. Every workflow executor has an explicit side-effect descriptor. Executors that do not declare a side effect default to `None`; email download executors declare external read; email mark-processed executors declare idempotent processed-marker external write.

2. External writes cannot be hidden behind permission flags. Plugin manifest validation rejects `WritesExternalData` without an external-write side-effect contract and rejects external-write side effects when the permission policy does not declare external writes.

3. Idempotent marker permission requires a marker contract. An executor requiring `IdempotentExternalMarker` must declare a processed-marker mutation kind and idempotent retry-safe side effects.

4. Retry policy is side-effect aware. A workflow node with `MaxRetryAttempts > 0` can retry an external write only when the executor contract is idempotent retry safe or explicitly has the idempotent marker capability.

5. Preview is not commit. Preview simulation payloads for mark-processed executors use `sideEffectMode: "Preview"`, `dryRun: true`, `committed: false`, and `mutationApplied: false`.

6. Commit receipts are explicit. Commit payloads for mark-processed executors use `sideEffectMode: "Commit"`, `dryRun: false`, an idempotency record, a processed-marker record, and an external side-effect receipt.

7. Idempotency keys are provider scoped. Gmail keys use the `gmail:` prefix and Office365 keys use the `office365:` prefix, preventing cross-provider key collisions.

8. Duplicate processing is not harmless. Gmail reads the current label state before mutation and skips `/modify` when the processed marker is already present. Office365 duplicate handling skips category PATCH when the processed category already exists and the source category is no longer present.

9. Side-effect receipts describe the attempted external mutation. Receipts include provider, operation, mode, dry-run state, committed state, mutation-applied state, idempotency key, message id, source marker, processed marker, and schema.

10. Unavailable executors fail explicitly. Executor descriptors carry availability diagnostics, and runtime invocation throws an unavailable-executor exception that includes the node id, executor id, and availability descriptor.

11. Preview simulations are deterministic and external-system free. Simulation descriptors and plugin preview tests exercise email workflow shapes without Gmail or Microsoft Graph calls.

12. Legacy descriptors remain valid. Deserializing old workflow executor descriptors defaults missing side-effect metadata to `None`, so the new contract is additive for existing serialized definitions.
