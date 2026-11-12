# Selected Agent authority snapshot

Design accepted on 2026-10-20 after reviewing the actual file-store lock ownership.
This precedes Process initial business binding and closes a concrete serialization
risk found while reviewing that integration.

`AcquireAgentsReadLeaseAsync` currently returns with the workspace gate and
cross-process workspace lock held. Existing Process source reads/capture carry
that lease across Project SQL checks. Initial business binding would extend this
exclusion to more SQL work. Unrelated Agent edits must remain able to commit while
one source/executor pair is being checked.

Add `AcquireAgentAuthoritySnapshotAsync` to the existing Core catalog lease port
for one or two distinct nonempty Agent IDs. An unsupported store explicitly
refuses; it must never fall back to the coarse API. Preserve the old API and its
execution-admission behavior.

Use the existing file publication scope and canonical Agent-key ordering:

1. Enter the short workspace guard and complete existing recovery/normalization.
2. Capture pending-recovery evidence and pending publication targets. Do not bind
   unrelated catalog payload bytes before key acquisition: no Agent value has
   been accepted yet.
3. Acquire only the requested Agent keys, using the existing finite acquisition
   and cancellation/cleanup path.
4. Reload the catalog under the reacquired workspace guard and freeze the exact
   selected Agent authority values, preserving primary identity and exact absence.
5. Release the workspace guard before returning. Hold the selected Agent keys
   until the authority lease is disposed.

The returned capability exposes scope, acquisition revision and selected Agents.
Its revision identifies that snapshot, not a promise that unrelated catalog data
cannot change. It does not expose the optional capability-catalog, execution-read
or Project-deletion-recovery capabilities of the coarse lease. In particular,
execution reads must not pretend that the old workspace guard remains held.
Missing selected Agents remain exact locked absences so the existing source owner
can issue its normal denial; no substitute Agent or grant is selected.

Catalog mutations already take keys for changed Agents; shared provider/capability
mutations take all existing Agent keys. Adding or deleting a selected Agent also
takes its key. Verify those invariants with independent-store writers. Preserve
immutable permission, tag and capability-assignment collections in the returned
Agent records. No Process or Workbench dependency enters Core or Persistence.

The canonical catalog adapter exposes this capability after checking its original
host, like its existing planning publication snapshot. It must not wrap it in the
coarse recovery lease. Workbench uses it in Process current-result reads and the
source lease held across real root/step capture. Initial business binding will use
the same capability for its executor/launcher pair. Ordinary coarse callers retain
their existing semantics.

Validation covers bounded input, missing/duplicate identities, same/independent
stores, selected revocation/deletion, unrelated Agent progress, shared catalog
mutation, reversed overlapping selections, cancellation and failed-read cleanup,
use after disposal, unsupported stores, and refused optional execution reads.
A real Project capture paused at a SQL barrier must allow an unrelated Agent edit
while excluding a selected-source edit. Reuse the existing file and isolated SQL
fixtures; no provider network call or new persistent server is needed.

Implementation stays in the existing storage/adapter owners, with one concrete
authority-only lease and a small Agent-key factory. There is no new runtime
partial, business façade, generic lock manager or provider implementation.

The first validation exposed redundant Project SQL during normalization: catalog
policy loaded binding facts for already retained lifetimes on every lease. Keep the
repair in `AgentProjectAccessCatalogPolicy`: request bulk facts only for a new lifetime
or an unbound selection without a previous binding to restore. Existing bindings remain
original; the business authority service still checks current Project liveness.
Both publication phases must validate genuinely new bindings. There is no cache,
new dependency, timeout increase or weaker legacy/reservation check. The real-store
bulk regression must prove zero queries for selected reads and unrelated edits while
retaining the two-query-per-phase check for new grants.

Restoring the legacy deletion regression also exposes a pre-existing source-boundary
defect: allow-all grants admit a target's replacement lifetime alongside the original
source's retired lifetime for the same Project ID. The native owner then rejects a
duplicate key rather than producing its governed refusal. The existing Workbench source
owner must reject conflicting exact lifetimes before returning its lease. Project-scoped
governance compares the complete admission. Preserve the Projects owner's duplicate-input
contract and the source owner's lease cleanup; add no query or dependency.
