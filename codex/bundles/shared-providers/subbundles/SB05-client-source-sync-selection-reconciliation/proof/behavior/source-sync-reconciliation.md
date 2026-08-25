# SB05 source synchronization and reconciliation behavior

State: `PASS`.

The source application service creates, updates, tests, enables, disables, deletes, and explicitly
resets trusted remote identity with optimistic concurrency. A source stores one canonical URI and one
existing secret-record reference; every selected import/profile reuses that source credential.

Synchronization resolves the token at use time, propagates only the optional typed access-context
reference, validates the bounded catalog/schema/source identity, and uses a deterministic selection
plan inside one transaction. Stable import/profile IDs, local alias, and local enabled intent survive
remote display, routing-model, and capability refresh. Additive and replacement selection are
explicit; real PostgreSQL proof shows replacement de-selection retains both import/profile rows and
retires the deselected import with the same IDs.

ETag is sent only when the source is available and every selected import has authoritative
availability. A 304 is accepted only after reloading and confirming the same concurrency/health
state; otherwise an unconditional fetch is made. Transport, authorization, 404/upstream, schema, and
identity failures are non-destructive. Only successful authoritative absence marks an import missing.
Reappearance reuses the same import/profile and successful reconciliation notifies observers only
after commit; 304 and identical plans do not notify.

Focused proof is 22/22 deterministic unit reconciliation and 16/16 real HTTP/secret/PostgreSQL
integration, including disable short-circuit, successful sync after re-enable, direct same-catalog
recovery without `If-None-Match`, and recovery after `TestAsync` clears only source status.
