# P18 Workflow coverage of root cancellation

Status: implemented and qualified in [the P17/P18 validation](../validation/2026-10-23-native-workflow-cancellation.md), following the P17 native browser failure. Default activation remains gated.

## Evidence and required correction

The P17 client passes its 63 component/native HTTP cases across the original run and
one explicit test-hook repair. Both existing native Process browser cases fail the new
child-state assertion: root recovery reports Completed while the mapped Workflow remains
WaitingForInput. The failure occurs in both global and Project Process workspaces.
The native browser artifacts and unchanged candidate identities are retained under
`c4-cancellation-client-browser`; no test processes, database leases or temporary browser
profiles remain from that run. Do not close P17 or enable root recovery by default on
the strength of its Agent-only completion proof.

`AgentFrameworkProcessExecutionRecovery` currently enumerates only the Agent execution
index. Process SQL quiescence cannot account for a Workflow owned by the Workflow module.
The existing Workflow runtime can cancel a waiting run through its durable external
request/response boundary, but its public operator method materializes all pending
requests and has a separate legacy branch. Recovery must use a bounded owner operation,
preserve active resume leases and refuse ambiguous or unsupported historical evidence.

## Dependency and ownership decision

Keep Process Application and Persistence independent of Workflow persistence and runtime.
Keep the existing provider-neutral Process execution cursor and recovery queue. Add one
Workflow-owned port in Workflow Abstractions with bounded selection and stop operations.
Its implementation belongs in the AgentFramework module that owns Workflow SQL. The
Processes module already references those contracts and is the composition boundary that
combines Agent and Workflow execution recovery. No new project reference, reverse module
dependency, cross-owner SQL join or generic event bus is necessary.

The Workflow port accepts an exact Process run identity, an optional Workflow keyset
cursor, and an exact selected Workflow identity for stopping. It never accepts a caller's
replacement origin, profile or requester. Its selection returns one fixed-size metadata
candidate, with a bounded serialized origin. Stop re-reads the original selected owner,
checks the immutable Process origin and current canonical profile, and uses the native
Workflow cancellation owner. A known terminal outcome differs from a signal, a busy
resume lease, missing original evidence and a refused transition.

The Processes adapter validates the durable stopped root and selected run's original
root membership before acting. Workflow-origin validation requires the exact saved
Process scope and current policy fingerprint. Mapped dispatch and tool origins also
carry their original profile binding, which must match the canonical database; a tool
origin must retain its prepared Workflow ID. Legacy assignment history has no embedded
profile binding: its exact Process identity and scope are interpreted only inside the
already selected canonical database, after the Process owner checks the stopped root.
Missing or malformed evidence remains pending reconciliation; never manufacture a new
launch or origin.

## Bounded selection and progress

Enumerate the union of Agent execution IDs and Workflow run IDs in GUID keyset order.
Each invocation reads at most one candidate from each owner and processes the lower ID.
If both owners have the same GUID, process both before advancing that GUID. This keeps
one durable provider-neutral cursor without collisions dropping an owner. Empty scans
from both owners prove exhaustion. An unavailable Agent index cannot be interpreted as
empty or allow the cursor to skip unknown Agent work.

Keep source selection separate from owner effects: extract the existing Agent candidate
stop logic, add the Workflow stop result mapping, and share the bounded cursor validation.
At most two owner calls occur for a GUID collision. Signals and busy/uncertain effects
retain pending-cycle evidence; completion requires a later cycle that observes terminal
state. No list-all API, whole hierarchy walk, blocking retry loop or automatic resubmission
of business work is introduced.

Add an index on Workflow `(OriginProcessRunId, RunId)` for the exact page query. The
existing `(OriginProcessRunId, OriginProcessAssignmentId)` index does not provide this
ordering. Prove the native query plan with unrelated rows and a deep cursor, including
equal GUIDs across owners. Index creation must use the repository migration path and a
reviewed PostgreSQL deployment strategy; do not silently create indexes during a read.

The pending-request page also needs `(RunId, Id)` filtered by `RespondedAtUtc IS NULL`;
this is the persisted request owner's existing definition of pending. Select only IDs,
then inspect the original boundary. The existing response-time index does not provide
the required key order. Workflow persistence already freezes the full admitted Process
origin and definition identity in `PersistentWorkflowRunStore.RequireFrozenAuthority`.

The existing mapped admission path also enlists `EfProcessWorkflowDispatchAuthority`
in the Workflow transaction, takes the Process root advisory lock and rechecks the exact
active dispatch before commit. The root cancellation barrier uses that same owner lock.
Consequently a new mapped Workflow cannot commit behind a completed recovery scan after
the barrier. Keep that fence and the separate Process-tool admission owner intact.
Legacy Process-assignment origins are readable history; current admission explicitly
rejects them. Compatibility fixtures must seed historical rows instead of manufacturing
a new legacy launch through the current admission service.

The migration follows the existing guarded concurrent-index pattern: verify the exact
owned definition, create outside a transaction, verify again, then reset only the seed
checkpoint. A conflicting or invalid leftover index stops the upgrade for explicit repair.
Concurrent creation avoids blocking ordinary writes, but requires extra scans and can
leave an invalid index after failure; it is not an instantaneous metadata operation.
See [PostgreSQL CREATE INDEX](https://www.postgresql.org/docs/18/sql-createindex.html)
and [Npgsql index modeling](https://www.npgsql.org/efcore/modeling/indexes.html).

For WaitingForInput, select at most two pending requests to distinguish exactly one
from ambiguity. Read its original durable boundary and call the existing native resume
boundary cancellation operation with that request/version. Its transaction arbitrates
against an active resume lease and retains terminal failure evidence. Refuse missing,
legacy-only or multiple pending request evidence. For an active run, use the existing
runtime cancellation signal and wait for durable terminal state; remote/noncancellable
backends remain pending. A lost commit acknowledgement is recovered by reading the same
run and boundary, never by starting a replacement workflow.

## Historical recovery and compatibility

Old recovery progress cannot certify the newly covered Workflow owner. Increase the
position document's coverage version to 2; keep the existing position fields, stage
numbers, intent format and payload bounds. Read and canonically validate version 1
without rewriting it during observation. New records use version 2. A version-1 Completed
record is reported as requiring reconciliation, never as completed current coverage.

When claiming unfinished version-1 work with no retained intent, reset its bounded scan
to the version-2 initial position under the existing row lease. If an original intent is
retained, resolve that exact intent first. On its definite resolution, reset the scan in
the queue's checkpoint transaction instead of discarding the original action. An unknown
outcome must keep its original intent. Preserve the root, live/legacy origin, barrier,
lease generation and monotonic progress revision.

The existing bounded legacy seeder performs a checkpointed scan of stopped roots. Reset
that single seed checkpoint in the migration when an older scan has advanced or completed,
then extend each page to
reopen valid completed version-1 records with no lease/intent, preserving their barrier
and resetting only recovery progress. Validate canonical evidence before updating it;
malformed evidence is not overwritten. Report reopened rows separately from inserted
rows. This avoids a table-wide rewrite or an unbounded startup backfill. Version-2
completed records are never reopened by this compatibility path.

A corrupt completed row rolls back its entire bounded seed page and raises the existing
typed corruption exception with its root identity. The worker logs that root without
the retained payload. Later legacy pages wait for explicit owner repair; other live
queue work continues. Advancing past the corrupt row would silently lose required work.

Mixed old/new recovery workers are unsupported: old binaries reject version-2 positions.
Deployment must stop old workers before enabling the new worker. Downgrade requires
workers disabled and retained evidence, not a lossy rewrite to version 1. Keep default
activation gated until the native, compatibility and capacity evidence is complete.

## Validation and closure

- Unit: ordered union, exhaustion, equal GUIDs, unavailable index, terminal/pending/refused
  Workflow outcomes, wrong run/root/profile, invalid metadata and bounded owner calls.
- Persistence: actual Workflow selection/cancellation, active external-response lease,
  original request receipt after lost acknowledgement, unrelated sibling preservation,
  and unavailable historical evidence. Capture query plans and inspect returned row and
  origin-byte bounds with a populated unrelated cohort.
- Queue/codec: canonical v1/v2 reads, invalid versions, unfinished v1 with and without an
  intent, unknown versus definite intent outcomes, bounded completed-row reopening,
  original barrier retention, and v2 completion remaining closed.
- Re-run the two actual native Process browser cases. Require both root recovery and its
  Workflow child to be terminal, and read-only Refresh to display verified completion.
  Add a deterministic held Workflow boundary/lease case to prove pending UI and later
  completion; retain selected-run and sibling assertions.
- Build affected standard Release projects and refresh owning test copies. Record exact
  discovery, source/output identities, scoped dependency deltas, full portability
  enforcement, documentation, artifact scans, and owned resource cleanup. Keep previous
  failures visible with explicit repair bridges. SPA stays on its feature branch.

After this correction, resume the legacy cancellation transport transition and remaining
Process/Workflow policy and mutable-owner work. This design does not declare the original
end-to-end request complete.
