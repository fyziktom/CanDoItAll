# P02a: conditional Projects definitions

Status: implementation in progress after architecture and performance review.
Proof tier: Behavioral, with real PostgreSQL and native browser proof for changed UI
interaction. Requirements R1/R2/R3/R4/R6/R7; no whole-engine closure is implied.

## Accepted contract and ownership

Use [the exact contract](../../../../docs/architecture/concurrent-mutations/07-projects-contract-cutover.md).
Mandatory original `ProjectDefinitionExpectation` binds profile, project lifetime and
definition revision. Creation, full replacement and scalar update have different typed
commands. Exact acknowledgements carry accepted revision, normalized values and submitted
child identity mappings. The existing facade maps callers and delegates to a cohesive
top-level writer; the new reader returns one coherent definition snapshot. No new partial
class, project reference, service locator or generic mutation framework is required.

The PostgreSQL diagnostic reopened the original blanket Serializable recommendation.
Even indexed root-only writes to different projects conflict through SSI index-page
predicates. The definition-specific entrypoint uses Read Committed only for ordinary
definition writes, after acquiring the complete ordered existing project/identity
advisory keys. The original root CAS, child-writer perimeter, lifecycle guards and unique
constraints establish the concrete invariants. Source-authorized writes retain their
existing Serializable/source-lease protocol. Other `ProjectSourceMutationScope` and
`SerializableMutationScope` callers are unchanged. See the diagnostic evidence and
owner design; no automatic retry or isolation-wide downgrade is authorized.

## Parallel ownership

- Contracts/draft worker: typed contracts, required accepted expectation, legacy editor
  field, draft snapshot/ack reconciliation, sandbox constructor and lightweight tests.
- Persistence worker: definition writer/rules, root mapping, definition-specific scope,
  facade save delegation, compensation fingerprint extraction and DI.
- API/agent worker: full/scalar HTTP mapping, coherent definition read route/tool,
  unchanged original expectations through prepared tool requests, focused adapter tests.
- Parent: coherent reader, facade read integration after writer edits, canonical migration,
  owner/race tests, retained-data/model checks, UI host work and serialized validation.

No worker runs builds against shared outputs or creates another database server. Use the
owned fixture with bounded leases. Ordinary application/database and sibling repositories
remain outside mutation scope. Source updates to the same file are sequential.

## Acceptance and planned proof

1. Existing full and scalar updates without original expectation fail explicitly.
   Lifetime/profile/route contradictions reject; current read is never used to refill it.
2. First accepted write advances the root even for child-only or unchanged data under a
   frozen clock. Later stale originals reject without scalar/child/projection residue.
3. Scalar updates preserve all children and external identity. Full replacement rejects
   duplicate, foreign, empty and unknown persisted child IDs. Cleared optional rows are
   deleted and acknowledgements clear their former IDs without discarding later typing.
4. A definition read sees root, token and children from the same short snapshot. Missing
   explicit definition reads return not-found. Accepted values never come from a later read.
5. Actual API/tool serialization carries the original expectation, including malformed
   nested identity refusal. Agent scalar edits cannot erase collections. Existing authority
   and claim boundaries remain current at mutation time.
6. Draft state and EditContext survive conflict; accepted revision advances while later
   typing survives. Native conflict actions never silently replace the original token.
7. Canonical migration backfills retained projects with nonempty distinct revisions;
   narrow/canonical models match, fresh imports get new revisions, no pending model changes.
8. Matched performance run reports accepted/rejected/faulted outcomes. Disjoint saves must
   not simply turn unexplained serialization failures into apparently fast rejection.

New parent filter `FullyQualifiedName~ProjectDefinitionConcurrencyTests` has **17 planned
expanded cases**: eleven Facts plus two two/four-row Theories. A paused SQL update proves
the root comparison still rejects and rolls back children after admission; a foreign
blank option cannot bypass ownership validation. Confirm exact discovery
after the final source compiles. The lightweight Projects.UI baseline is 26 cases;
the worker records its final expanded count. API/tool and existing owning regressions
will use source-derived filters/counts before execution. No planned count is test proof.

Build every affected production project directly using Release, then refresh/discover
owning test assemblies. Run relevant model/migration, Projects owner, lifecycle, external
identity, scalar tool/HTTP and draft cases. Use a deterministic paused-root read for
snapshot coherence and original drafts for same-target contention. Browser proof uses
two independent large-desktop contexts after the complete native conflict path is ready.

Finish with full portability scan/review and no-write enforcement, rendering register
review where applicable, documentation validation and independent architecture diff review.
Shared schema/contracts/persistence require the named frozen broad stable checkpoint;
do not run it repeatedly during incomplete compilation.

## Remaining work and reopen conditions

P02a has no durable operation identity, replay or response-loss reconciliation promise.
P02b must implement atomic exact receipts/projection, trusted caller/epoch/namespace,
retention and safe restore. P02c closes the full recovery journey. Definition deletion,
external effects, material planning basis and other owners remain separately tracked.

Reopen this design if any editable writer bypasses the root protocol, the source branch
loses a current authority guarantee, read coherence fails, a new dependency cycle is
introduced, or the candidate hides unrelated-project contention instead of fixing it.
