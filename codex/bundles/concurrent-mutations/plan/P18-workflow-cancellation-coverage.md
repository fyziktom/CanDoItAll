# P18 Workflow coverage of root cancellation

Status: design before implementation, discovered by the P17 native browser gate.

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
root membership before acting. Workflow-origin validation supports typed mapped Process,
legacy Process-assignment and Process-tool origins only when their retained authorization
scope and policy evidence bind the current canonical profile. Missing or malformed
evidence remains pending reconciliation; never manufacture a new launch or origin.

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

The existing bounded legacy seeder already cycles stopped roots. Extend each page to
reopen valid completed version-1 records with no lease/intent, preserving their barrier
and resetting only recovery progress. Validate canonical evidence before updating it;
malformed evidence is not overwritten. Report reopened rows separately from inserted
rows. This avoids a table-wide rewrite or an unbounded startup backfill. Version-2
completed records are never reopened by this compatibility path.

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
