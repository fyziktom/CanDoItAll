# Scheduler implementation review and bounded carry-over

## Verdict and review depth

Keep the implemented architecture. The routed host, production owner adapter,
reusable presentation logic and actual Canvas renderer form a useful boundary.
The next assignment begins with two bounded correctness repairs, then proceeds to
Memory. These are source-derived findings; the reviewer did not build .NET, run a
browser or reproduce the races in this environment. The executor must demonstrate
failing-first regressions before changing their current equivalents.

Implementation: `14f07bffa30ddb124011869e38bc5b264b8bce42`.
Branch snapshot: `b55baa3a94ff216353768f0a4ac9a50e89463d08`.
The child commit only adds the old Scheduler bundle to history. Product and test
content is identical at both SHAs. The implementation diff is the preceding
`0e176a3b99270cdc9a86a57d6276d05979d7352e` -> `14f07bff...` comparison. [EV01–EV03]

## Accepted work to retain

The previous Plugins selection repair now renders its list even when the selected
plugin vanished. It shows an unavailable detail without rebinding old drafts to a
surviving plugin. The catalog disappearance signal also retires the original
selection's late browser effect. The package extractor independently captures a
cleanup failure and carries the primary failure and exact replacement stage.
These resolve the two preceding source findings in the inspected paths. [PL01–PL03]

Scheduler has separate editable values without the privileged StructureAuthority,
a production adapter that captures operator authority and checks profile identity,
immutable save capture and field revisions, and retained exact owner facts after
durable save. Quartz synchronization remains distinct from persistence. The
calendar uses an instance registration and exact selected event/plan identity;
its host no longer owns one global document listener. Keep those choices. [SC01–SC09]

The reviewed implementation receipt reports 72 Plugins and 173 Scheduler cases,
245 distinct selected cases in total. It also reports source-mode build/watch and
published-asset proof. These are implementer claims in maintained documentation,
not independently inspected TRX/screenshots or fresh reviewer executions. Actions
lookup returned no runs for the implementation SHA. Neither observation establishes
that the product is untested or that every transition is correct. [EV04, EV05]

## SC-R1 — stale recovery can overwrite a newer write

**Priority:** correctness repair before starting the Memory slice.
**Affected responsibility:** recovery-request admission and continuation ownership.
**Source:** `SchedulerMutations.ReviewUnknownAsync`, `ReviewPlanAsync`; actual review
control in `SchedulerDraftReceipt.razor`. [SC02, SC10]

### Exact source path

`ReviewUnknownAsync` checks Unknown and captures the submitted values at entry.
It then awaits `owner.EditorAsync` without claiming a review operation/generation.
On return it updates the draft's ID, replaces its receipt, writes the plan receipt
and removes `submissions[draft.Origin]`. It does not recheck the originating receipt,
the dictionary entry, the latest review request or disposal. Its catch similarly
assigns an error regardless of the current recovery lifetime.

`SchedulerDraftReceipt` exposes another review click while the first read is
pending; the explicit disabled condition only validates the ID string. The public
method also permits overlapping calls regardless of any shared button behavior.
Blazor's logical synchronization context does not prevent reentrancy across an
incomplete await. [EV11]

### Deterministic reproduction

Use an **existing plan A**, so both reviews request the same original exact ID and
no uncertain create/name matching is needed.

1. Make an admitted Save return Unknown; retain its submission and replay lock.
2. Begin review R1 and hold its exact-plan read independently.
3. Begin review R2 against the same draft, receipt and exact ID.
4. Complete R2. It resolves current persistence as an explicitly qualified warning.
5. Start new Save S2 of that draft/plan; hold validation or owner execution. Its
   receipt is Pending and its own submission is registered.
6. Complete R1. In the inspected code it overwrites S2's Pending receipt and deletes
   S2's submission entry, despite returning a fact about the old review.
7. A further Save or same-plan Pause/Delete can now pass admission while S2 is still
   pending. If S2 later returns Unknown, its recovery submission may already be gone.

Two read completions must be independently controllable; the scenario store's
single global release gate alone is not sufficient to prove this ordering.

### Required correction and proof

Own the review by the original receipt/submission and request generation. Either
refuse overlap at the handler or accept only the latest matching request. Recheck
ownership after every await before publishing success, mismatch, error or final
state. Preserve the successor's dictionary entry. Disposal/retirement must suppress
obsolete effects without pretending the original write was rolled back.

`ReviewPlanAsync` has a final success comparison, but its observed-state-mismatch
and exception branches still assign an old receipt without that guard. Cover those
branches in the same bounded repair. A late failed flag/deletion review must not
restore an obsolete Unknown over a newer Pending/settled receipt.

Verify a direct-handler regression plus the actual recovery control. Assert new
receipt identity/status, retained submission, exact plan ID, owner dispatch counts,
same-plan conflict refusal, unaffected independent plan and eventual recoverability.
Do not consider a disabled control alone proof of safety.

No persistence protocol, new authority, automatic replay or broad Scheduler rewrite
is needed. Exact-ID review continues to describe current persistence, **not** prove
which earlier command committed or whether Quartz synchronized.

## SC-R2 — obsolete dependent validation blocks a corrected submission

**Priority:** bounded editor correction in S0.
**Affected responsibility:** invalidation of diagnostics tied to dependent values.
**Source:** `SchedulerInputSession.InputAsync` and the pre-validation branch of
`SchedulerMutations.SaveAsync`. [SC02, SC03]

When a parent changes, `InputAsync` removes direct dependent input/JSON values. It
removes errors only for the changed parent, not for the dependent value just
removed. `SaveAsync` refuses any draft with `Issues.Count > 0` before calling the
owner validator. The obsolete child issue therefore prevents reevaluation.

Reproduce with the current scenario's optional `project -> node` descriptors:
load valid object input, submit a node that a controlled validator rejects, then
change project through the real control. The old node is removed, but its issue
survives and another Save never calls validation. Since node is optional, a clean
submission without it should be eligible for fresh validation. [SC08]

Invalidate only diagnostics for values whose semantic context was retired. Preserve
unrelated issues and raw parse errors. For a required child, fresh authoritative
validation must still report the missing value; a parent change must not implicitly
satisfy required input. Repeated unchanged input must not cause extra independent
option requests. Exercise the real renderer, not just direct dictionary mutation.

## Scope guard

Do not convert either finding into a general reentrancy framework or a test-suite
rewrite. Use focused current tests, build the changed Scheduler roots and real page
consumer, and run affected browser cases and required static/documentation gates.
Expand owner/fire/authority tests only if the correction changes those contracts.
Preserve the existing S0 Plugins behavior and avoid reopening unrelated runtime
limits recorded in the previous receipt.

After recording S0, proceed to [Memory](MEMORY_REVIEW_NOTES.md) in the same run.
