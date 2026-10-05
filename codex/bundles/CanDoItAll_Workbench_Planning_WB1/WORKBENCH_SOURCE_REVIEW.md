# Workbench source observations and reproduction obligations

These observations concern the pre-existing family selected for WB1. They are not attributed
to AC1. Source inspection is not a newly executed reproduction. Use current source and tests
before coding. Existing correct owner behavior must survive each correction.

## WB1-Q1 — quote result can outlive repricing eligibility

Source S23: QuoteRequestIdentity contains resource + estimate; the cache contains resource +
effort (and not project/lifetime/execution state). OnParametersSet changes the informational
message for a Started/Unknown task but does not retire a request if resource/estimate remain
the same. ApplyQuoteAsync checks those values but not current execution eligibility.
S21's HandleExecutionChangedAsync changes only the execution snapshot, so a parent-driven
state transition is a meaningful path to reproduce.

Sequence: open a NotStarted task with a selected resource; hold the explicit quote; change
its execution state to a valid non-repriceable state without changing the estimate; release
the quote. The old amount must not reach EstimateChanged or overwrite the new informational
state. Also test unavailable results and ordinary errors, not only a successful amount.
No backend cost corruption is asserted: the native TaskApplicationService independently
restores historical amounts when authoritative repricing is forbidden (S27).

Existing tests cover a task already Started before a request, effort changes and manual cost
changes (S24); they do not test the eligibility transition during that request. Preserve them.
Add real parent and direct controlled tests, including transition back to NotStarted, the
same IDs in another project/profile lifetime, cache hit after context change, and disposal.
Do not fix this by removing quote previews, accepting invented zero prices, or weakening the
native Save check. A canceled result that ignores cancellation must still be fenced.

## WB1-C1 — real calendar versus demonstration capabilities

S17 gives the real calendar read-only events with create/edit/delete/drag/resize disabled.
S16 separately manufactures a validation surface with those flags enabled plus synthetic
playlists, checklists and defaults. This is not evidence of existing native calendar CRUD.
Move demonstration composition to a clearly synthetic sandbox lane; production facts cannot
inherit its assumptions. Existing genuinely consumed diagnostic functions must be accounted
for explicitly rather than silently dropped.

## WB1-L1 — asynchronous origin and view-state persistence

Calendar load tracks project ID and reload generation, while selection/state callbacks use
live page values and persisted view-state Save has its own asynchronous effect (S16).
Gantt load already checks the captured Surface around independent reads; preserve that.
Mutation/readback/notification callbacks and some dialog-opening paths still use current
parameters after awaits (S20). Reproduce stale action, parent switch and late completion
orders; do not call an old result a new target merely because public IDs match.

Preserving a native project admission is necessary but not sufficient for UI ownership.
It can prevent a wrong-lifetime write while a stale dialog still modifies a valid old target
that the operator has left. Capture both original native authority and presentation opening.
A currently accepted write is not rolled back by retiring its view.

## WB1-M1 — task edits are not a single transaction

S25 first saves task details/pricing, then optionally attaches a definition resource. S27
has native assignment and compensation stages. S26 enforces title/progress/estimate/execution/
cost-basis/direct-assignment revisions at the real mutation boundary. Preserve these checks.
A general failure before a high-level method returns is not automatically known rejection;
a partial effect or failed compensation must keep its original cause and exact identity.
Do not hide an owner-result gap behind the UI phrase “chart unchanged.” Add small owner-specific
facts only where needed and prove them with fault injection at the real phase boundary.

## WB1-D1 — full raw form and metadata round-trip

The two task dialogs have once-initialized private state, parser callbacks and child value
extraction (S21–S22). Invalid date/number edits currently can leave the previous typed value
while another field clears a common error. Test real invalid raw input followed by another
edit and Submit; do not silently submit a stale valid value. Preserve unset progress (-1),
unknown execution history, repository references and fields the form does not expose.
Hidden data round-trip and whole original state are more important than matching file counts.

## Disposition

WB1-Q1 is the concrete narrowly reproducible source-derived defect to close first. The other
items are required design/proof obligations of this extraction, not assertions that every
possible failure has already been observed. Do not manufacture a new AC1 blocker or call a
pre-existing limitation a regression without comparison evidence.
