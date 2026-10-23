# P17 — Cancellation observation and finite root admission

Status: design for the successor to P16. Read-only observation can be implemented
independently of worker activation. Mutation cutover still requires qualified recovery,
an explicit deployment/activation contract and the API/native-client proof below.
The current P16 candidate is under native validation; this document is not its result.

## Source findings and boundaries

`ProcessRuntimeOperatorApplicationService.RequestCancellationAsync` currently starts a
root barrier, notifies observers, walks all cancellable descendants for up to five
passes, finalizes the root and catches projections up inside the request. The finite
pass count does not bound the number of descendants or observer work. `ProcessesApi`
then catches projections up again. The same application service serves the native
`ProcessWorkspaceProjectionClient`.

`ProcessCancellationRecoveryStaging` already enlists the original root barrier identity
and queue row in the runtime transaction. The recovery coordinator and worker own
bounded continuation. They must remain the only continuation owners after cutover.
The HTTP handler must not run a second cascade or claim a queue lease to make its
response look complete. No SQL transaction may span File, provider or browser work.

A child cancellation currently promises to cancel only the named child. The recovery
queue is keyed by an actual root. Preserve that distinction: never resolve a child to
its root and submit a root barrier implicitly. Child compatibility needs its own proof;
root recovery must not become an accidental cancellation of siblings.

## First slice: bounded observation

Expose an owner read contract in Process Application, implemented by Process Persistence
and registered by the existing Processes module. Web and native UI use that contract;
neither reads an EF context or queue entity. A dedicated read port is a real persistence
boundary, not a second queue/coordinator or a general-purpose status framework.

Read one selected runtime identity and its root recovery record in one database statement.
Select only the fields needed for observation. Do not load runtime steps, children,
receipts, serialized intents or full history; do not lock, seed, claim, repair or advance
the queue during a read. Keep the existing five-second database command bound and pass
the caller cancellation token. Validate input identity before database access.

The typed result must distinguish missing run, child identity, no cancellation requested,
pending recovery, verified completion, and retained state requiring reconciliation.
Report runtime status separately from recovery progress. In particular:

- A `Cancelled` runtime row is insufficient to claim completed cleanup. Historical
  direct cancellation can leave child accounting or original effects unresolved.
- A stopped historical root with no recovery row is explicitly untracked; reading it
  must not manufacture a barrier identity or seed it from current settings.
- Completion requires a valid completed queue checkpoint consistent with the cancelled
  root. Invalid stage/evidence is an explicit reconciliation outcome, never an empty
  success or a reset to the initial stage.
- Return only bounded progress, original barrier identity when present, revision and
  public pending reason. Keep lease tokens, owner actions, credentials and retained
  planning material private. A revision is an observation version, not authority to edit.

Bound serialized position material before it crosses the database read boundary, then
use the existing strict codec. Preserve malformed original bytes. Validate completion
consistency without using another subsystem's status as an invented receipt.

Add an authorized `GET /api/processes/runs/{runId}/cancellation` with `Cache-Control:
no-store`. Current Process permissions apply on every observation; knowledge of an ID
is not authorization. State explicit child semantics instead of redirecting to root
mutation. Reuse the current profile and API authentication owners. The read contract
must remain useful while the worker is disabled, without suggesting that automatic
progress is enabled.

## Subsequent admission and activation

Use a small Process Application owner for root admission. It loads the selected root,
establishes one original command context, and commits the existing barrier/queue unit of
work. A bounded optimistic-concurrency retry reuses that context; it does not create a
different semantic action on every attempt. Repeated requests for an already stopped
root observe the original barrier. Terminal non-cancelled runs remain immutable.

Admission returns the accepted barrier separately from completion. Root HTTP responses
use `202 Accepted` for pending work and point to the authorized observation endpoint.
Only a verified completed checkpoint supports a completed response. A lost response or
deadline is uncertain; clients observe the selected root and retained original intent
before another action. Do not claim rollback merely because the request was cancelled.

Admission must not promise continuation when the configured worker is unavailable.
Define that availability at the Processes composition boundary; Application must not
depend on module options, Web or hosted-service implementations. Preserve an explicit
disabled outcome before any mutation. Decide the legacy `/cancel` adapter cutover and
native-client behavior together, including older clients' interpretation of `Applied`.
Do not add an error-triggered fallback to the old unbounded cascade.

Keep current caller authority separate from the caller's display label. The source
profile, root lifetime and authorization checks must remain valid at admission; a label
in a request is not a trusted actor. Background completion continues an accepted stop
and must not reconstruct an absent admission from a new caller's credentials.

Native UI owns its selected-run request generation, pending display and refresh lifetime.
A late completion cannot update a different selection or restore a disposed component.
Projection refresh is outside the admission transaction and cannot turn a known accepted
barrier into an uncommitted error. Read-only refresh can recover an uncertain response.
SPA edits remain on `codex/concurrent-mutations` for colleague review; no main merge.

## Proof and release constraints

For the observation slice, qualify the actual PostgreSQL query, missing/root/child and
legacy cases, pending versus completed consistency, corrupt bounded position material,
unchanged queue/receipt bytes, independent profile isolation, cancellation and access
denial. Confirm indexed selected reads without a full-history query. Include codec and
existing queue regression cases. Compare scoped dependency snapshots and run the full
portability gate before committing. No migration or new project is expected.

For admission/cutover, hold a real child's selected publication key and prove the root
request returns pending while the worker owns continuation. Verify lost acknowledgement,
duplicate admission, restart/takeover, current authorization, disabled availability,
child-only compatibility, exact accounting and no repeated provider/effect work. Prove
native and HTTP pending/complete displays and selection changes in the real host.

Activation additionally needs the remaining sustained-load and separate-OS-process
qualification. P16's process-local queue does not promise cross-process fairness or
remove history-sized canonical publication. Record the supported deployment and capacity
envelope from measurements before enabling the worker by default. Do not replace those
gates with a larger slice deadline or a broader retry loop. This slice does not close
Process refresh, Workflow inheritance, remaining mutable owners or final Stable/E2E.
