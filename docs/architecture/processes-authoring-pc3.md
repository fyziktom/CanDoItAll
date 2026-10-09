# Durable Processes authoring

Status: implementation in progress. PC3 authorizes the previously deferred native boundary.

The current five editor services own unrelated per-scope snapshots. Fresh native client
scopes discard writes; the catalog and launch compiler read the distributed template pack.
Persistence must own one complete definition, not five serialized forms.

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
