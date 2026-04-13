# Finding to subbundle map

| Finding | Primary fixing subbundle | Secondary follow-up |
| --- | --- | --- |
| `F001` True canonical dependency closure | `02-true-canonical-dependency-model-closure` | `03-architecture-review-gate-a` |
| `F002` Missing Process foreign keys | `04-process-schema-referential-integrity-hardening` | `06-architecture-review-gate-b` |
| `F003` Nullable dependency uniqueness bug | `05-null-safe-dependency-uniqueness-and-db-invariants` | `06-architecture-review-gate-b` |
| `F004` Lifecycle invariants not enforced | `07-definition-lifecycle-invariant-hardening` | `09-architecture-review-gate-c` |
| `F005` No outbox / durable side-effect boundary | `08-transactional-side-effects-and-outbox-alignment` | `09-architecture-review-gate-c` |
| `F006` Proof artifacts weaker than claim | `01-live-proof-reconciliation-and-gap-reopen` | `11-final-proof-and-closure` |
| `F007` Structural concentration | `10-service-seam-and-ui-orchestration-follow-up` | `11-final-proof-and-closure` |
