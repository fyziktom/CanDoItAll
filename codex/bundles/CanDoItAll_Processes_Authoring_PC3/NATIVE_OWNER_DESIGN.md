# Native owner design and transaction boundaries

This file fixes invariants and ownership, not exact class/table names. Introduce a narrow
port only for a real storage/resolution boundary. Do not impose an assembly/interface quota.

## Placement based on the current graph

The reviewed Processes.Persistence project already references Processes.Application;
Application references Templates and Projections. A native storage/resolution port and a
complete semantic envelope can therefore live at the existing application boundary while
reusing the template document internally. Persistence implements it; the Processes module
composes it with the existing trusted caller/profile/project policy. Small durable identity,
revision and observation value types may belong in existing light Contracts/Abstractions/
Projections as the evaluated graph permits. Do not introduce Templates -> Contracts ->
Templates cycles or make UI depend on Application, Persistence, EF, module services or DI.

Do not move the whole template schema into a new assembly unless current consumers make
that necessary. Likewise, do not create a second lossy set of every role/step DTO merely
to store data. Existing document types plus a versioned native envelope and explicit pinned
resources are a reasonable smallest design. Preserve supported JSON/AOT codec registration.

## Logical persisted facts

| Fact | Required content | Not part of it |
| --- | --- | --- |
| Scoped definition head | Explicit scope/incarnation, stable definition key, expected/head revision, complete current draft, base provenance, immutable published pointer, lifecycle state and catalog metadata | View selection, EditContext, transient errors, browser generations |
| Immutable publication | Publication identity, schema/content identity, complete executable document, resolved resource provenance and necessary dependency pins | A mutable pointer to today's template pack or today's draft |
| Command receipt | Caller-scoped operation identity, payload fingerprint, aggregate origin, committed/rejected outcome as appropriate, exact committed revision/result identity, safe diagnostics | A replacement authorization capability or a cached current UI model |
| Authored layout/import provenance | Stable reference-node placement and semantic links, imported source/content identity and target/remap | Per-frame pointer deltas, zoom/pan, opened dialogs |

Use the smallest normalized tables and/or schema-versioned JSON payload satisfying these
facts. Do not split a single definition into unrelated per-panel authorities. Metadata used
for list/count/filter queries must not require deserializing every complete document.
Unrelated runtime/telemetry data remains under its existing owner.

A published definition may also have a newer draft. Do not encode that reality as a single
mutable status that accidentally unpublishes the previous revision on SaveDraft. Map the
existing UI/status/API surface compatibly and make draft-versus-published identity explicit.
No new application-wide event sourcing system is needed.

## Stable identity and scope

The logical storage key includes canonical database-profile ownership, global versus project
scope, the captured project lifetime when project-scoped, and the definition key. Use the
existing definition-ID/hash conventions for unchanged distributed templates. Do not change
historical IDs through a new case-normalization pass. Reject ambiguous new keys according
to current supported key rules and test collisions under the actual database collation.

Enforce uniqueness in PostgreSQL, including concurrent first writes in global scope. A
nullable project ID in an ordinary composite unique constraint is not enough. A non-null
scope discriminator/key with checked project columns or equivalent reviewed indexes is
valid. GUID/public IDs and hash strings supplied by the renderer do not establish authority.

Editing in a project workspace creates/changes the local authoring entry derived from the
resolved baseline; it must not accidentally overwrite a global template/definition because
the selected catalog row originated globally. Make the write target visible. Preserve the
current global workspace behavior and applicable native permissions. A draft remembers the
base source/version from which it was created; a template update cannot silently rebase it.

## Transaction and concurrency

Capture immutable submitted values and the admitted owner/incarnation before asynchronous
work. Load/validate/normalize outside long locks where safe, then enter the current coordinated
mutation transaction. Revalidate the captured project admission under the existing mutation
gate, verify expected aggregate revision, apply the bounded patch, write new head/publication/
receipt and commit atomically. Use the canonical profile-bound context, not a new current
profile captured after a switch. Complete physical transaction participation matters more
than merely sharing a DI scope. [Primary references R1-R3]

Reuse ProjectWriteAdmissionService.RequireUnderMutationGateAsync and current coordinated
transaction APIs at the owning module boundary where applicable. Do not add a Projects
implementation reference to the light Processes contracts/application solely to get this
check. Use the existing owner-adapter pattern/narrow native policy port and inspect current
callers. Profile-global writes need the corresponding current profile/caller checks.

Optimistic concurrency is on the whole authored definition. Two different editor families
must not overwrite each other's changes from the same revision. Return a typed conflict and
the safe current observation; do not retry a stale user patch against the newest revision
without reconciliation. Current code can keep independent loaded panel state, but every write
must identify the baseline actually submitted. Bounded internal DB serialization retries
are not permission to change that logical expected revision.

An operation identity is created before dispatch and remains the same for recovery/retry.
The native owner scopes it to the caller and target and fingerprints the complete semantic
request. Same ID/same payload returns the original result; same ID/different payload is a
conflict. Concurrent duplicate requests and an unknown response after commit cannot produce
another role/import/publication. A receipt ID first generated only after the result returns
does not solve this. Use existing suitable primitives instead of a general new bus.

A successful commit followed by failed projection refresh returns a known accepted identity
with a warning. Only a genuinely unobserved commit is Unknown. Recovery consults durable
receipt/status with the same operation identity before any retry, including after a new
host. Give the UI an explicit recover/status path; don't leave WorkspaceBusy true forever
with no resolution. Rejected commands retain drafts and permit explicit corrected actions.

Do not perform file traversal, external inference, workflow dispatch, template loading with
unbounded I/O or browser work inside a DB transaction. Materialize bounded owned data before
commit, and persist/verify the exact immutable resource identity used. Apply normal backend
limits and redaction. Persist neither service objects nor exception objects in receipts.

## Existing entry points and temporary adapters

Inventory the native client, shell service, API/producers/tools and direct service consumers.
Route all supported writes through the same authority, with compatible adapters for existing
signatures where needed. An optional legacy field is not silently converted into a breaking
mandatory API field. Define/test a compatibility transition without retaining a second
production dictionary or a bypass that accepts unversioned overwrites. UI disabled state
never replaces native permission or version enforcement.

Temporary in-memory owners remain only explicit sandbox/test implementations. Remove their
registration from production paths before closure. Any caching must be optional, bounded,
profile/incarnation/revision-keyed and unable to claim success when the durable store failed.
Prefer no new cache initially; measure before adding invalidation complexity.

See [primary references](PRIMARY_REFERENCES.md) for R1-R4 and [source register](SOURCES.json) for the reviewed project/owner paths.
