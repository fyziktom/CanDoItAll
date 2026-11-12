# 10 · Projections, files, external effects and compensation

## Projects search: the first coupled consequence

The inspected save commits the project, releases its source scope, then uses a separate project-guarded transaction to upsert search with the saved values. That protects lifetime identity but not content ordering between two saves (SRC-03/SRC-15). A later callback from an older save can apply after a newer callback. A host failure can also prevent the callback entirely.

The concurrency pilot must choose a coherent projection policy, not silently leave this inconsistency behind while claiming a consistent accepted result.

### Option A — same-database transactional projection

Use the existing `UpsertForMutationAsync` participant with the actual source owner transaction and typed projection input. The project changes, final source stamp, receipt and small search update commit together. There is no new broker, projection queue or consistency lag for that data.

Trade-off: index work lengthens the source transaction and search failure can now reject the primary save. Measure SQL/lock/WAL cost and decide whether the stronger atomic boundary is appropriate for the current lightweight index. A derived search index should not pull large embeddings or external APIs into this transaction. Lock ordering with deletion and other search writers must be re-proven.

### Option B — durable rebuild intention

Persist a bounded source-owned intention atomically with the project. A worker rebuilds using a correctly guarded canonical read or version-ordered event, with idempotent application and deletion/lifetime handling. Coalesce obsolete same-target intentions when allowed; never lose the newest requested state. Expose lag/backlog and a repair path.

Trade-off: extra durable records, worker/retry logic, retention, storage capacity and eventual consistency. An outbox guarantees durable intent, not automatically correct ordering or exactly-once application. Define poison-message handling and don't let a failing document block all projects.

### Decision for P02

Prefer A if current index cost is small and all owner ordering contracts compose. If not, B must be completed to its bounded recovery guarantee within the pilot's declared scope; "add an outbox later" does not close F05. Record the choice after the P00 baseline. Optional activity feed can remain best effort, but a mandatory audit record must be durably part of the effect/protocol it audits.

## Revision ordering is not GUID comparison

An equality token identifies a state but cannot order two projection messages. Use an owner-defined monotonic scope generation if ordered application is needed, or rebuild from the current canonical state while holding the source/projection protocol that prevents an old rebuild from winning after a new one. Content hash equality is also not temporal order.

A deletion tombstone and lifetime identity prevent a delayed update from reviving retired content. Do not let an imported historical event update the active target dataset. Bulk rebuild tools must obey these rules too; maintenance is not a silent alternate writer.

## File bytes are a separate boundary

Before changing storage, re-read the actual revision-aware driver contracts and the existing placement/recovery intent state machine; SRC-11/SRC-14 only establish documented reuse candidates for this package. A metadata row CAS does not conditionally replace external bytes. Local files can be edited outside the application; content length/mtime alone may not express the required guarantee.

Classify each driver: atomic conditional replace supported; create-only supported; immutable version supported; application-local locking only; or best-effort external observation. Specify the exact supported multi-writer guarantee per driver. A local lock coordinates only cooperating writers and is not an atomic compare-and-swap against arbitrary other processes.

Keep potentially slow hashing, upload and provider calls outside the business transaction where the existing protocol permits. Stage immutable content, record its exact intended publication, and let the owner's recovery protocol bind placement/metadata. Do not mutate unrelated source files to compute a revision. Bounded resource sizes, containment, authorization and existing recovery receipts stay mandatory.

## External APIs

For an external invoice, email or provider mutation, record the local intended effect and its originating proposal/authority, then deliver through the specific provider's idempotency and conditional-write protocol. Use the stable provider operation ID where supported. Distinguish queued, dispatching, confirmed and uncertain states according to existing owner behavior.

A local database rollback cannot unsend an already accepted external operation. A timeout is not proof that nothing happened. Reconciliation must query the external identity or expose a manual recovery requirement. Where an API lacks deduplication or lookup, document the weaker guarantee and do not automatically repeat an uncertain action.

Authority recheck immediately before dispatch narrows the cancellation race, but a revoke can overlap the remote effect. A strict cancellation guarantee requires provider-side fencing or an explicit ordered delivery contract. Do not claim that a local preflight removes this distributed boundary.

## Compensation and Undo

A compensation is a new, narrow domain intention with preconditions. It must not restore a whole old snapshot and erase later human work. Existing Projects creation compensation already compares exact lifetime/reservation and a state fingerprint plus bounded owner evidence (SRC-15). Preserve this stronger rule when content tokens are introduced.

For an accepted phase addition, undo can propose removing that exact phase if its relevant content/reference conditions remain unchanged. If another actor added tasks depending on it, automatic removal may be invalid. For an external operation, compensation is defined by that provider's domain, not by an inverse JSON patch.

## Batches and partial completion

A bounded same-database multi-item command can be atomic. Large imports or external fan-out may need staged work with per-item stable identities and explicit partial outcome. Never label a loop of independently committed items as an atomic batch. Chunking changes the product guarantee and requires its own preview, progress, cancellation and compensation semantics.

The dependency set, payload size and lock footprint are bounded before execution. Reject an oversized atomic request or ask the caller to use the explicitly staged operation; do not silently drop dependencies or split into partial commits behind the same success response.
