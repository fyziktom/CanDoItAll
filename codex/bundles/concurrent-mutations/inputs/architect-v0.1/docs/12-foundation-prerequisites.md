# 12 · Foundation prerequisites and improvement opportunities

## Mandatory before a protected pilot

**Writer coverage.** Identify the true enabled write perimeter, including C# callers, HTTP/tool schemas, UI clones, bulk SQL, imports and maintenance. Additive schema alone cannot protect a root if a child writer does not advance its token. Use current Code Analytics facilities where actually available, backed by symbol/caller and evaluated-project inspection; unavailable tooling is recorded and replaced by explicit evidence, not fabricated output.

**Commit-boundary knowledge.** A known commit must survive observer, read-back and cleanup exceptions. A connection failure during commit must remain reconcilable. Trace the result across each wrapper; a `Result<Guid>` can discard needed information if the adapter later turns a read-back failure into "save failed". Implement the smallest owner result that preserves accepted identity/stamp and safe recovery.

**Source/lock participation.** Preserve source-first authority acquisition, exact admission sets and the specialized PostgreSQL branch. Map nested scopes and held resources before introducing another validation path. Fault-inject cleanup/re-entry and use two processes, because local semaphores can conceal unsafe database behavior.

**Coherent reads and aggregate stamps.** Return project/phase/option data and its stamp consistently, bind the original stamp to the write, and advance it for every relevant child change. Verify tracked and bulk paths, including canonical mapping and import. Do not rely solely on the generic stamp helper.

**Safe public cutover.** New protected updates reject missing conditions. Every currently enabled caller either participates or is disabled. Migrations, active sessions and background producers are part of that perimeter. Define operation-result and dataset restore semantics before creating a public retry promise.

## Improvements that should be measured before being mandated

| Candidate | Why it may help | Required proof / reason not to do it blindly |
|---|---|---|
| Narrow owner fact reads | Replaces full hierarchy/structure materialization during validation | Confirm existing GetSummary/GetMany ports do not already solve the use case |
| Batched source validation | Bounds transport cost to owners/batches rather than rows | Must preserve complete result set, authorization and publication protection |
| Scope instrumentation | Reveals actual local/DB wait versus hold time | Bounded metric labels; do not put record IDs in high-cardinality series |
| Earlier gate disposal after commit | Avoids blocking unrelated work during callbacks | Project Save already does this; inspect individual remaining paths |
| Correct finally-based cleanup | Reduces stranded local gates after cleanup exceptions | Test ownership/idempotent disposal; preserve original exception and commit knowledge |
| Identity maps for child reconciliation | Avoids quadratic scans and accidental recreation | Validate duplicate/foreign IDs and normalization before changing matching behavior |
| Shared minimal outcome vocabulary | Makes UI/API/tool behavior consistent | Do not break each owner's established enum/schema just to rename it |
| Feature-neutral draft utility | Reuses submitted/live merge semantics | No CRM dependency leak; at least concrete multiple consumers justify generalization |
| Finer revision scopes | Fewer benign conflicts and replans | Every additional scope has a complete writer-maintenance cost |
| Alternative local lock strategy | Can reduce stripe collisions | Requires real contention evidence and multi-host correctness proof |

## Explicitly not prerequisites

No general microservice split, universal message broker, global event bus, cross-module transaction coordinator beyond the existing actual SQL enlistment, CRDT editor, central mutable object repository, complete authentication redesign, or mandatory typed-ID refactor of all modules is necessary to fix lost Projects edits.

The entire P01 should not become an architectural cleanup programme that delays the concrete defect indefinitely. A shared-foundation change is mandatory only if the pilot cannot safely or efficiently use the existing behavior. Otherwise record the issue, instrument it and implement a separate narrowly justified slice.

## Performance-aware guard consolidation

Some checks can be combined when they examine the same bounded facts under an already held invariant: lifecycle and content can be read together; required source lifetimes can be fetched in a bulk owner query; accepted-result projection can be assembled from current tracked accepted state. Others cannot: a source lease before thinking does not replace current claim validation at publication; a UI capability flag does not replace backend policy; a cached project lifetime from another dataset is invalid.

Document the proof that makes each reuse safe. Avoid a general ambient "already authorized" flag whose validity depends on undocumented prior calls. Transaction-scoped participation already exists and should remain explicit (SRC-05/SRC-10).

## Migration-authority alignment

The Projects runtime context does not own independent schema creation. Add mappings to the canonical complete model and the owning context consistently; migrations remain in the canonical PostgreSQL migration project (SRC-15). Validate migration/transfer model parity and tests rather than calling EnsureCreated from a new owner ledger service. Mixed migrations and data transfer are intentional maintenance boundaries, not ordinary mutation routes.

## Ready-to-design checklist

Before P02, reviewers must answer: what exactly changes the edit stamp; how every writer does it; how a client obtains an original coherent stamp; what a duplicate means; which record proves commit; what is protected until publication; what the input-size limits mean; what search consistency guarantee is selected; and how old binaries/sessions are prevented from bypassing it. Any unanswered item blocks the corresponding guarantee, not the entire product's unrelated development.
