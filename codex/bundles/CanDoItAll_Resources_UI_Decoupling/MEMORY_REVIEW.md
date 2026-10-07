# Closure review: Scheduler and Memory

## Scope and verdict

Source review at `5f7f8329e3e3e30bd1451fc72757e04847579754`, comparing the accepted
Scheduler/bundle checkpoint `b55baa3a94ff216353768f0a4ac9a50e89463d08` to current HEAD.
The three-commit change separates bundle archival, Scheduler corrections and Memory
implementation. Current implementation receipt reports 184 Memory cases plus 76 Scheduler
cases; those are not reviewer-executed results. [EV01, EV02, EV03]

Keep the architecture and proceed to Resources in this same assignment after S0.
The review is a source-grounded check of important changed seams, not a full application
execution or a claim that no other issue can exist.

## Previous findings

SC-R1: the reviewed `ReviewUnknownAsync` now owns a latest request, captures the original
receipt and submission object, and checks both after the await. Its failure and finally
paths respect the owning lane. `ReviewPlanAsync` also fences mismatch/error outcomes.
This directly addresses stale review overwriting a successor mutation. [SC01]

SC-R2: clearing a dependent typed-input value now removes diagnostic entries for that
same dependent key. The malformed-raw-input checks remain ahead of synchronization.
This is the narrow requested correction, not a broad validation reset. [SC02]

The implementation receipt records their separate signed commit and 59 light, 15 real-page
and two browser cases. No fresh execution of those C# tests was performed by this review.

## Memory parts to preserve

- The production module retains concrete services; the Contracts project depends on
  `Memory.Abstractions`, the RCL on Contracts and real components, and the presentation
  controller is shared with the sandbox. The recorded seven-project sandbox graph is an
  implementer measurement, not a graph re-evaluated here. [ME01, ME11, ME12, EV03]
- Profiles, queries and nested transport fields are captured rather than passed as a live
  mutable form through owner awaits. Unknown operations stay locked and review is explicit
  and serialized. Original receipts survive selection changes/disposal. [ME01, ME02, ME08]
- All seven tabs remain, external Provider UI is mounted only when active and keyed by
  profile/draft revision. Failed ledger regions are separate from empty results. [ME03, ME05]
- The current capability refusals, safe registered extension boundary, credential-reference
  policy and protocol/runtime ownership were retained according to source/receipt evidence.
  This follow-up is not authority to enable missing provider features. [EV03, ME09, ME10]

## ME-R1 — already-published query result outlives its profile revision

**Priority:** P2 functional/provenance error. **Evidence:** source control-flow deduction;
not reproduced in a running product during this review. This is not a claim of a proven
authorization bypass or cross-account data breach.

The controller checks `selectedVersion == selectionVersion` when the query task returns.
If the version still matches, the observer assigns `draft.QueryResult` and may initialize
the empty feedback context ID. Only then does `ExecuteAsync` call `ReadAsync`. [ME01]

On a snapshot change, `ReadAsync` increments `selectionVersion` and replaces `Snapshot`,
but does not invalidate or label the already-published `Draft.QueryResult`. The renderer
then passes that result together with the replacement `SelectedProvider`; the Query panel
renders the result whenever it is non-null. `MemorySubmission` retains the original
provider ID but has no profile revision presentation at this boundary. [ME01, ME02, ME03, ME04]

The owner does supply a selected revision derived from the actual projected profile;
a changed name/configuration produces a different revision. This is therefore not merely
a missing backend signal. [ME05, ME06]

### Deterministic reproduction A: existing visible result

Initialize provider A at R0 and complete a query. Retain the returned context/operation
identity. Replace the same provider ID with changed configuration or display metadata,
then call ordinary Refresh. Assert R1 is now the snapshot revision. The old result remains
in the current Query panel without explicit R0/historical status. Removing A gives a
related state: no selected provider but the old result still renders. [ME01, ME04]

### Deterministic reproduction B: the query's own read-back

Hold a query launched at A/R0. Change the fake/controlled owner profile to A/R1 without
refreshing the page. Finish the query. The controller sees its still-current R0 version
and publishes the result, then its automatic read-back discovers R1. The current code
leaves the old result published. This ordering bypasses the otherwise useful pre-publication
fence. Use the existing scenario store/owner wrapper to control timing, not sleeps.

### Existing proof is adjacent, not sufficient

`Same_ID_profile_replacement_fences_old_query_while_retaining_raw_input` explicitly
refreshes the new profile **before releasing** the held query. It proves the first fence,
not snapshot publication after the result. The submitted result must remain in historical
receipts in both orders; do not change that established guarantee. [ME07]

### Required correction

Bind live query presentation to its actual submitted provider and observed profile context.
At accepted snapshot publication, re-evaluate whether it still belongs to the current
context. Either clear it from the current result region and keep the original receipt,
or render it explicitly as historical/unavailable. Preserve known operation/context/handle
identity and provenance. Do not label current persistence as proof of the earlier dispatch.

Preserve raw profile/query/feedback/ingestion inputs, including manual context IDs and
comments. If auto-filled feedback context is no longer current, distinguish it from a
manual edit before invalidating it. Do not implement feedback execution to demonstrate
this UI policy. Do not erase historical ledger rows simply because provider metadata changed.
A failed snapshot read is stale/unknown availability, not evidence of a new revision.

Cover same revision, replacement, disappearance/reappearance, the two timing orders,
A-B-A, stale read after a newer result, and no automatic dispatch. At least one test must
assert the real visible Query/result region, not only a counter or a private version field.
Use a real owner snapshot in addition to scenario policy tests. This is one bounded S0
fix; do not create a new durable profile lifetime or distributed concurrency protocol.

## Evidence discipline

The current record reports 27 light, 39 page/compatibility, 32 owner/integration, 78 expanded
runtime, six composition and two browser Memory cases: 184 total. Together with 76 Scheduler
cases this is 260 selected cases, not a full Stable run. The runtime lane lists 70 discovery
entries expanded to 78 cases; do not compare those as if they were the same metric. [EV03]

GitHub Actions lookup for this SHA returned zero runs. That does not disprove local testing.
Raw TRX/screenshots/watch graphs were not available to the reviewer. Package checks and
product behavior are deliberately reported separately. [EV04]
