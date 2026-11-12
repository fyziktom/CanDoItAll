# Process planning review HTTP boundary

The draft was transferred after the manager integration candidate passed all 172
cases and its source/runtime identities were verified. The original source and
transfer pins are retained in the ignored validation ownership record
`manager-http-continuation-ownership.json`. Preserve unrelated continuation work.
Implementation validation is tracked in
[the HTTP checkpoint](../validation/2026-10-21-process-review-http.md).
The boundary now passes all 76 selected cases after repairing the test clock's
periodic-timer support. Compatibility, selected-read capacity and remaining parent
lifecycle work still gate activation.

## Reviewed decisions

- Web captures current authentication and all eight owner grants. It composes the
  existing bounded credential/session observer; Workbench receives an operator
  capability without a new Project admission. Renewed credentials retain the same
  manager identity; original source lifetime and permissions remain independent.
- Process Application provides a selected review observation and a pending
  selection. A GET creates no fake action ID or completion command. The pending
  selection shares the existing original-input, single-activity and runtime review
  checks. One selected child query is used; no accepted full-plan JSON is decoded.
- Workbench owns the review presentation. AwaitingChoice, RecoveryRequired,
  Settled and Unavailable are distinct. Accepted or unresolved work never produces
  another approval opportunity. The original request is still needed for exact
  recovery; an accepted intent digest cannot reconstruct a lost request.
  Settled requires both accounted child work and an observed terminal runtime
  result. Accounted child work without that result remains recovery pending.
  Parent Process and effective child Agent policies are named separately; an
  inherited child always consumes its immutable assigned material even when the
  parent captures a new snapshot per step.
- Read and write paths use the workspace's original-source disclosure checks.
  The reader reobserves the selected Agent aggregate and requires the same revision
  before its final current-manager/profile checks. POST rechecks current Process
  and Agent conditions independently of this advisory GET.
- Every revision in the new HTTP DTO is a decimal string, including the returned
  child run revision. The existing generic Agent response has a numeric revision,
  so this endpoint uses a minimal explicit child projection instead. It does not
  return raw metadata, authority envelopes, source bindings or runtime claims.
- The request has a 24 KiB limit, depth 16, required constructors, case-sensitive
  unique members and no unknown members. Process identities must match the route;
  the completion key is the Agent action ID. No actor or permission override is
  accepted from the body.
- An accounting refusal after Agent publication is a successful transport response
  with `AccountingPending=true`. An unacknowledged Process commit returns an
  explicit unconfirmed response. Neither permits a new publication. A retained
  exact request can recover after host restart.
- Original decision and budget receipts are historical; runtime state returned
  by idempotent lookup is reloaded from persistence. The API does not serialize
  either runtime observation as an editor snapshot. Results require runtime reload.

## Boundaries and capacity

No new project reference, persistence table, generic pipeline or partial owner is
planned. Web registers a dedicated mapper and access service. Workbench registers
one reader alongside the existing action owner. Process Application retains its
owner-neutral query/input contracts. The previous Workbench snapshot remains the
baseline for its additions. The Web/Application before snapshot is
`snap-20261021164047-51755eb3`: 880 types, 7,785 members, 849 dependencies, no scoped
cycles. Its 189 pre-existing MSBuild/package diagnostics are collector limitations;
recorded Release builds remain compiler evidence.

The Agent payload is already bounded by the inherited single-step contract and
provider limits (131,072 response-text bytes; 262,144 argument bytes). Presentation
keeps the existing 300,000-byte content ceiling. JSON bodies allocate at most the
24 KiB limit plus one overflow byte. There is no provider dispatch on read/replay,
and no external IO is introduced inside a global publication lock.

The GET loads the existing full runtime aggregate once and reads the
selected Agent aggregate twice. This is an explicit remaining scaling concern:
runtime load cost grows with sibling steps/claims/results even though selected
child and accepted-input reads are indexed. Before recurring SPA invalidation or
governed executor activation, measure 1/128/1,024-step cases and either qualify
that cost against the agreed budget or replace the read with a typed selected
runtime observation. Do not represent a partial graph as a complete runtime
snapshot, or reuse display data as mutation authority.

The v1 durable intent hashes still serialize nested public records. A reviewed
fixed compatibility fixture (or explicit versioned encoding with a migration
decision) remains required before activation. Do not silently change v1 semantics.

## Planned proof

The implementation adds 27 strict-contract cases, four real authentication-host cases,
eight pure selected-read cases and seven actual HTTP/SQL cases. The latter cover both snapshot modes, approve/reject,
restart, exact retry, forbidden replacement action, invalid/oversized/ambiguous
requests without intent, accounting refusal and lost commit acknowledgement.
They reuse the declared `candoitall-api-tests` environment family and its shared
database across the source fixture and restarted HTTP hosts.

Confirm the final discovered cases before execution. The selected-read cases cover
one query/no mutation, cancellation, an active claim, later results, other local
or shared work, and incomplete versus completed runtime accounting.
Refresh affected standard Release outputs serially, run full portability
enforcement, compare source/runtime identities and architecture, and observe lease
cleanup. UI recovery, parent refresh/cancel, original-request loss reconciliation,
Workflow/settings, remaining writers and sustained capacity still gate full closure.
SPA stays on `codex/concurrent-mutations`; it is not merged or pushed.
