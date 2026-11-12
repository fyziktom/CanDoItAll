# P27: fail a Process when reviewed planning material changes

Status: implemented and qualified, 2026-10-23; cleanup follow-up remains. Behavioral proof
tier. [Validation](../validation/2026-10-23-process-basis-change-failure.md) records 247
distinct backend cases, 152 SPA tests, sixteen browser states and eight historical-reader
checks. Prerequisites are
signed backend P26 `6e3cb4d14` and SPA `c611d35`; both worktrees were clean at entry.
SPA stays on `codex/concurrent-mutations`, with no merge or push to main.

## Behavior and compatibility

Qualify `OnBasisChange.Fail` for the existing single-activity Project task planner in
both RunSnapshot and StepSnapshot modes. A successful current publication still succeeds.
If its original material is no longer current when publication is attempted, publish no
tasks, fail the original child and Process, and settle the original finite grant exactly
once. Retain the original proposal, conflict evidence and review action for replay.
This is a publication-boundary decision: it adds no background polling, live mutation of
the provider's input, or automatic restart while a provider is working.

Existing inherited origins always resolve to PauseForReview, including origins whose
parent policy says Fail. Changing that interpretation would corrupt saved decision
validation. Add an optional typed `ChildOnBasisChange` to the inherited origin and its
strict codec. Absence retains the old pause behavior and encoding. The only new explicit
response is Fail, and it is valid only when the original parent policy is Fail. Reject
unknown values, Replan, and mismatched parent policy. The new property is part of the
immutable original origin; persistence cannot add or remove it after admission. Older
strict codecs reject its presence instead of silently executing different semantics.

Entry review refinement before backend execution: the child marker alone does not guard
a rollback between preparation and child enrollment. Add the same optional response to
the saved Process step admission, with schema version 6 when present. Legacy versions
1-5 remain byte compatible; a version-6 admission requires an explicit Fail response,
matching parent policy, material and original funding. Earlier codecs reject the new
version/member before provider work. Pass the original response through the existing
dispatch/receipt authority query. Bind and recheck equality against the child origin on
all original-source reads, including the Process background-source policy. Legacy Fail
admissions without the marker still produce the historical Pause child. The public
producer alone opts new Fail admissions into the marker. This extends Runtime,
Application, Persistence and Modules.Processes through existing references, without a
new dependency. Preserve the pre-change Runtime binary for an actual old-reader probe.

Only the Workbench initial binder selects the new response, after verifying the original
Process dispatch, policy, source, executor and Project lifetime. No public request can
submit an inherited origin. Existing origins and replays are never upgraded. The Agent
Models project owns this provider-neutral grant contract; it gains no Process reference.
The existing Core publication completion already interprets Fail as terminal failure.
The existing Process review coordinator consumes the verified child closure, atomically
settles its budget and terminal shared scope, and records idempotent completion. Reuse
these owners; do not introduce a second settlement path or cancellation-as-failure.

Extend the Workbench discovery descriptor and launch admission to advertise Fail and
PauseForReview for both read modes. Work, basis-age, source and single-activity ceilings
stay enforced. A retry keeps its saved response even if Workspace defaults change.
Automatic replanning remains refused: it needs parent-owned debit, quiescence, successor
material/generation and retirement of the old child/effect before it can be advertised.
Pinned publication and mixed/multiple activities also remain outside this slice.

## SPA selection and recovery

Add a change-response selection using existing components, beside the read-mode choice.
Offer Workspace defaults and the server's advertised responses. Validate selected tuples
against the discovered contract before dispatch, then show the actual resolved review.
Persist the optional override with the same immutable original request. Keep version-one
pending records readable, including records containing only a read-mode override; reject
unknown fields, invalid enum values and empty policy objects. Recovery and acceptance
must retain the selected response and caller intent after reload or lost acknowledgement.
No API schema or SDK regeneration is expected: the existing override already has this
typed field. Rendering remains separate from session effects and storage validation.

## Risks, costs and proof

There are no new database reads, indexes, transactions, locks, providers, services or
project references. Resolution adds a bounded enum check; JSON adds one optional field
only for new Fail children. Publication and accounting use existing keyed operations.
Verify that a conflict is classified before settlement, approval replay never dispatches
again, no child work remains open after terminal failure, and parent reserved work reaches
zero exactly once. A paused conflict must remain pending rather than pretending to fail.

Before closure:

- Build Models, Process Runtime/Application/Persistence, Modules.Processes, Workbench and
  the owning Unit/Integration projects in standard Release,
  serially. Refresh Web through the Integration graph. Confirm discovery for every filter.
- Unit proof covers legacy encoding and policies, explicit Fail round trip, rejected
  response/parent combinations, immutable origin, and all four advertised choices and
  their ceilings. Run the existing inherited journal and launch-policy topics.
  Include Process admission versions, missing/downgraded grants, original source mismatch
  and the historical binary reader. The initial discovery caught a wrong Unit Process
  namespace before tests ran; correct the filter and reconfirm the expanded selection.
- Real public launch/queued MAF/HTTP review/SQL proof covers unchanged and changed material
  in both read modes, Fail and Pause, original-intent replay, no duplicate provider/effect,
  closed child work and exact parent accounting. Retain existing refusal tests.
- SPA session/storage tests cover defaults, valid and unsupported tuples, legacy storage,
  lost-response recovery and immutable retry. Run its full existing small test set,
  typecheck, build, and owned formatting/lint checks. Inspect final 1920x1080 selection,
  open response selector, resolved Fail review and restored original-intent states.
- Compare scoped CodeAnalytics and direct project references; report existing analyzer
  limitations. Run complete portability-static, review every baseline delta, and enforce
  without writing. Reconcile owned processes, database leases and temporary paths; preserve
  ordinary 5032 and historical denied cleanup. Update docs and sign both checkpoints.

Entry review: existing owners provide the terminal and accounting paths; the additive
origin field is necessary to preserve historical semantics. No new architecture layer or
dependency edge is justified. The codec change invalidates inherited journal consumers;
bounded journal/owner/review and real Process producer cases cover those callers. The
whole-engine Stable gate remains a later frozen-checkpoint requirement, with its existing
failures still open; this slice cannot claim engine-wide closure.
