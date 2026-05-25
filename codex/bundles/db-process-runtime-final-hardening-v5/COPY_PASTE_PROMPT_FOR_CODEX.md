You are a senior C#/.NET architect working in the CanDoItAll repository.

Target branch: `db-remove-sqlite`.

Use the repository-local bundle execution skills. Execute this follow-up bundle:

`codex/bundles/db-process-runtime-final-hardening-followup-bundle-v5`

Goal:
Finalize the PostgreSQL-only process runtime after SQLite removal. The branch is much better now, but there are still process DB canonicality and proof gaps.

Main risks to close:
1. Startup process recovery must not clear non-expired automation dispatch outbox leases unless owner death is proven.
2. Long-running process dispatch must continuously renew durable step dispatch claim and outer outbox lease while AgentFramework/workflow execution is in flight.
3. Process outbox side effects must have explicit idempotency proof under lease loss and retry.
4. PostgreSQL claim queries must have indexes and EXPLAIN ANALYZE proof.
5. Throughput improvement must have numeric benchmark proof, not only source-level proof.
6. Broad integration/component validation caveats must be closed or explicitly classified with evidence.

Critical constraints:
- PostgreSQL is the only persistent runtime DB.
- Normal runtime contexts must use canonical pooled AppDbContext.
- Profile-specific contexts are maintenance/admin only.
- A non-expired lease is canonical ownership.
- No stale worker may write canonical process DB state after losing a lease.
- Parallelism must partition by canonical aggregate to avoid duplicate mutation.
- Source code comments must be in English.

Execute subbundles in order:
SB01 validation evidence and merge scope
SB02 startup recovery lease reclaim canonicality
SB03 long-running process dispatch heartbeat
SB04 process outbox idempotency and side-effect canonicality
SB05 PostgreSQL process DB indexes and claim query plan
SB06 throughput benchmark and runtime metrics
SB07 process DB red-team tests
SB08 final merge readiness

Do not claim completion until proof manifests and semantic invariants are filled for every critical subbundle.
