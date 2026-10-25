# P11 continuation: irreversible cancellation and accounting replay

Status: implemented and qualified after signed `9b6785309` with 84 passing selected
cases, including 14 new cases; see [validation](../validation/2026-10-21-cancellation-barrier-and-replay.md).
The preceding 178-case accounting proof remains evidence for its tested cases, but the
later audit reopened receipt recovery after legitimate rework. That finding is now
corrected and proven in both snapshot modes. No producer is activated.

## Concrete findings

`CancelOpenWork` preserves previously Failed steps. `RequestStepRework` rejects Completed
and Cancelled roots but permits CancelRequested, then makes the root Active. A parent
stop capability cannot depend on this barrier remaining in place until that is fixed.

`EfProcessRuntimeUnitOfWork.FindCompletedCommandAsync` returns historical decision/budget
receipts with freshly loaded runtime state. The new terminal-accounting receipt validator
incorrectly treats a later step-status change as receipt corruption. A previously Failed
step may legitimately be reworked after accounting, so exact replay must not compare its
current status with the status originally included in the action identity.

## Minimal edits and ownership

Reject rework while CancelRequested in the existing runtime transition with an explicit
cancellation-in-progress diagnostic. At the persisted current-state/CAS boundary, reject
any change from CancelRequested except retaining CancelRequested or finalizing Cancelled.
Once Cancelled, retain Cancelled; a direct commit cannot reopen a finalized barrier either.
Place this after original-state comparison and receipt replay, before state replacement.
This is a commit invariant, not a new coordinator or persistence abstraction. Preserve
ordinary rework from Failed and existing accounting-only mutations.

Validate terminal-accounting receipts through their original request identity, run/root,
child/material, closure, budget and measured work. Keep the immutable request StepStatus
in the command digest and completion validation; remove only the comparison with mutable
current runtime StepStatus. The returned runtime remains the store's current view. Exact
receipt replay cannot reactivate a root or authorize another effect. Another actor/action
and changed original accounting evidence must still refuse.

No project references, schema, new partial, UI, provider call, root finalization/quiescence
activation or workspace-wide scan is needed. Runtime baseline
`snap-20261022000141-289734b4` has 60 documents, 253 types, 1,635 members, 404 edges,
zero cycles and 56 findings. Persistence baseline `snap-20261022000150-531e33b7` has
66 documents, 178 types, 1,370 members, 203 edges, zero cycles and 62 findings. Its ten
workspace diagnostics are existing NuGet vulnerability warnings surfaced as errors by
the analysis workspace; ordinary affected builds, actual project references and source
inspection remain required. Application baseline for this continuation is the qualified
P11 A snapshot `snap-20261021235110-bef0246a`.

## Proof and progression

First reproduce the failed-step rework bug with a focused pure runtime regression on
the unchanged production binary. Retain that expected negative result. Then prove the
runtime refusal, ordinary failed-run retry, repeated begin and valid finalization.
Use actual SQL to reject direct reactivation, preserve already completed command replay,
and test both cancellation/rework ordering cases with stale-CAS rejection. Re-run the
terminal-accounting owner and real SQL/file tests, adding replay after legitimate failed
step rework in both snapshot modes. Include existing narrow rework/cancellation tests.

Freeze exact selected source/method inventory and confirm built discovery before the
final run. Build Runtime, Persistence, Application, Process module, Web and test outputs
serially in Release. Run full portability scan/review/final enforcement, rendering/docs,
scoped architecture comparison and source/runtime/resource reconciliation. Reuse the
existing isolated server and owned fixtures. The new profile has no timing qualification;
existing finite-read/capacity failures and the final integrated Stable gate remain open.

After this proof, continue selected Agent stopping and parent quiescence/recovery before
refresh or activation. The dedicated stop owner must use exact original ownership and a
stable current profile fence, preserve actual effects/work, and leave unknown work pending.
SPA remains on its existing branch for colleague review.
