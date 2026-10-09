# Durable Processes authoring

Status: implementation in progress. PC3 authorizes the previously deferred native boundary.

Before PC3 the five editor services owned unrelated per-scope snapshots. Fresh native client
scopes discarded writes; the catalog and launch compiler read the distributed template pack.
Persistence now owns one complete definition rather than five serialized forms.

Application owns the versioned semantic envelope, bounded editor patches and resolution
of draft/publication content. Persistence implements a narrow aggregate store with one
PostgreSQL transaction for head revision, publication and operation receipt. The module
supplies trusted profile/caller identity and captured project admission through a policy
adapter. Existing Application -> Templates/Projections and Persistence -> Application
references suffice; no project references or UI infrastructure dependencies are added.

The envelope retains the template document and separately materialized execution guidance,
effective role resources, authored reference layout and import provenance. Serialization
uses an explicit schema and source-generated metadata. Editor mutations patch only owned
fields. Opaque UI tokens are accompanied by an owner-defined monotonic observation.
Selection, zoom, invalid raw input and request generations remain view state.

A non-null storage scope key includes profile, project and project lifetime. Database
uniqueness and a transaction-scoped lock serialize first writes and operation replay;
expected revision remains a logical CAS checked after the lock. A receipt is scoped to
caller and operation and fingerprints the submitted payload. Recovery returns its exact
committed result; it never reapplies a stale patch to the current head. Project policy
participates in the same physical transaction and rechecks the captured admission.

Published content is immutable. Saving another draft preserves the published pointer.
Launch resolution chooses project publication, global publication, then unchanged template;
an archived selected override blocks new launch. Prepared launches and child dispatch pin
their resolved content before delayed execution. Delete removes the local override while
retaining publication/receipt history required by prior runs.

The rejected simpler option is persisting each existing form independently: it would keep
conflicting revisions and discard hidden execution contracts, guidance and relationships.
A new generic command bus, contracts assembly or cache would add complexity without fixing
those defects. Existing projection helpers can remain pure rendering/validation adapters;
their dictionaries must not remain production authorities.

Proof includes lossless rich-document round trips, patches preserving unrelated fields,
real PostgreSQL CAS/replay/rollback tests, ordinary DI across scopes and an OS process
restart, migration upgrade/model/transfer checks, browser journeys and one frozen Stable
campaign. Source/DI/reference inspection substitutes for unavailable CodeAnalytics MCP.
Final validation and measured query/build effects will be linked here after execution.

Storage checkpoint: nine PostgreSQL integration cases passed in ProcAuthoringPC3 on the owned PostgreSQL 18.6 fixture. They cover concurrent first writes and operation replay, exact recovery, CAS, immutable publication retained through a newer draft, real pre/post-COMMIT faults, non-null global uniqueness, all distributed documents/guidance round trips, and an additive/idempotent upgrade preserving prepared/accepted runtime payloads. The native client regression still reproduces lost edits before S2; this is not UI or complete PC3 closure.

The S2 native adapter uses a scoped read session for a coherent five-panel observation and
creates an ephemeral projection engine for each mapping/validation pass. Those scratch
engines reuse existing normalization and lint rules; their dictionaries have no native
write authority. Each accepted patch commits the complete semantic snapshot through the
same CAS store. The client creates a normal scope for canvas writes as it does for the other
families. Catalog metadata is read in one bounded query, with project rows overriding global
rows and deletion restoring inheritance. FeedDefaults remains a repeatable read of defaults.

Canvas actions materialize semantic steps, branches, bindings and artifacts. Clones and
geometry live in a distinct reference layout without duplicating executable items. Import
merges allocate collision-safe keys, rewrite internal links and retain source/remap history.
Receipts retain command-selected identities and original commit time. The positive native
regression and twelve family cases cover independent scopes, stale cross-family saves,
referenced/last-role deletion, all nine canvas actions, and the three real import kinds.
Publication validation, executable selection and UI reconciliation remain subsequent PC3
stages; this checkpoint does not claim those paths are complete. The combined S2 checkpoint
passed all 22 selected integration cases with zero skips; the production integration build
completed without errors. The nine initial receipt-selection failures were test comparisons
of collection references, corrected to compare both the scalar fields and list contents.

S3 adds a monotone observation comparison and a coherent sibling read-back from the same
committed in-memory snapshot. It does not assign new tokens to dirty forms. Clean forms
advance on a newer observation; dirty forms keep their baseline and report a conflict.
Historical command receipts survive subsequent reads, and stale observations cannot replace
confirmed state. Project inheritance carries the observed global revision separately.

Unknown outcomes retain their original operation locator and immutable submitted command in
the opening. An explicit status read is scoped to the original profile and project lifetime.
A recorded result is recovered through the idempotent owner; an absent result offers a
separate explicit retry using the same ID and payload. Neither rendering nor refresh repeats
a write. Known command outcomes remain known when sibling projection reconciliation fails.
Retirement fences status callbacks and their cleanup as well as write completions.

S3 validation passed 222 component cases and 24 PostgreSQL integration cases without skips.
The native recovery pair injects faults before and after the real transaction COMMIT,
checks status from a separate native scope, retries the original command, and verifies one
head and one receipt. The Application assembly retains its dependency boundary; safe host
logging reports reconciliation failures without adding a logging dependency to Application.
