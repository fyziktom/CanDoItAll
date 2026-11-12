# Process cancellation

For a root run, use `POST /api/processes/runs/{runId}/cancellation` with no body.
Current Process read and execute grants and an enabled registered recovery worker are
required. A caller label cannot grant authority. `202 Accepted` confirms the original
cancellation barrier, while descendant cleanup may still be pending. `200 OK` means
verified completion of the current recovery coverage. The response and Location resource
identify the same original root; responses are not cached.

Use `GET` on that Location to observe progress. It requires current Process read permission
and never advances recovery. Runtime `Cancelled` by itself is not evidence that all owners
finished cleanup. `LegacyUntracked` and `ReconciliationRequired` require operator attention.
Preserve revision strings exactly; they are progress observations, not editor preconditions.

After a timeout or unconfirmed response, observe the original root. Do not assume rollback,
create a replacement run or automatically retry a write. Accepted recovery continues under
its own durable ownership, independently of later caller permissions. Cancellation does
not roll back already completed external effects.

New Agent invocation checkpoints distinguish local function proposals from hosted
provider tools. Cancellation can finish after local execution stops and its effects are
resolved. Historical invocations missing that evidence and uncertain hosted-tool effects
remain pending for reconciliation; a terminal Agent status alone does not close them.

The legacy `POST /api/processes/runs/{runId}/cancel` accepts only a selected child and still
requires a JSON object (`{}` uses defaults). A root receives HTTP 409 with
`process.root_cancellation_requires_admission`, before any mutation, observer or projection.
Its Location points to the finite cancellation resource. The server does not redirect or
repost the request. Clients must deliberately adopt pending/completed semantics and the
new endpoint's authority requirements. A successful child result concerns that child only;
it does not certify root or sibling cleanup. The legacy caller label is descriptive text,
not authenticated identity.

The native run-detail action uses finite root admission, selected-child compatibility and
manual Refresh status. A failed parent projection refresh preserves known acceptance.
Selection changes retire stale reads; there is no automatic retry of an uncertain mutation.

Recovery coverage version 2 includes both Agent and Workflow children. Old version-1
completion is not current completion. Stop older workers before enabling new workers;
mixed versions are unsupported. The migration creates guarded concurrent indexes and
resets the bounded legacy seed cursor. Conflicting index definitions or corrupt retained
recovery evidence require explicit repair. Default worker activation remains disabled
until its supported capacity and deployment envelope is qualified.
