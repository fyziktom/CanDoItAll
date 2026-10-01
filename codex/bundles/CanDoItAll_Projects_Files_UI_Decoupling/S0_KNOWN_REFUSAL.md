# S0 / P1-R1 — known original-lifetime refusal and exact recovery

Priority: bounded correctness repair before P2. Evidence level: source-confirmed presentation path plus an existing backend regression; no reviewer-run runtime reproduction. Sources: R03, R04, R06-R10.

## The observed code path

`ProjectsPage.SaveCoreAsync` correctly retains the native ProjectEditorAcknowledgement before calling IAdmittedProjectWorkbenchSeedService. The generic catch then selects SeedOutcomeUnknown for every later exception. `DeleteAsync` similarly maps every non-partial exception to DeleteOutcomeUnknown. Both leave `mutationTargets` occupied, keyed only by the public ProjectId. Acquiring a fresh draft does not remove that original slot.

The admitted seed writer passes the original ProjectWriteAdmission into its real transaction before preparing native objects. The existing `Seed_transaction_refuses_the_original_admission_after_same_id_recreation` test deletes/recreates the public ID while seeding is held, observes ProjectWriteAdmissionRejectedException and proves that the successor receives no seed. Exact Delete admission is checked before participant preparation. Those specific pre-write refusals establish no effect for the rejected stage; they do not establish rollback of an earlier completed project Save.

## Failing-first reproduction

Extend the existing real-owner seed case rather than substituting a mock-only scenario:

1. Open a new project through the real P1 editor, submit one planned object and hold the admitted seed before its writer.
2. Confirm the project acknowledgement is retained with original profile/project/lifetime. Retire the project and recreate the same public ID under a new lifetime in the isolated test owner.
3. Release the original seed. Verify the exact typed rejection and absence of seeded nodes in the replacement.
4. Assert the UI reports a known stale-lifetime refusal of the seed, not an unknown completion; keep the earlier project acknowledgement historical and the original unsaved plan visible for inspection.
5. Explicitly close the stale editor and acquire the replacement through the normal UI. Verify the new current lifetime is not blocked by the terminal rejected old operation. A new deliberate allowed action is permitted; the original seed is never silently rebound or retried.

Add a Delete equivalent: acquire A, replace its lifetime, invoke Delete from the old editor, verify rejection before deletion participants and unchanged replacement data. Assert exact UI classification and explicit current reacquisition. Test A→B→A, a separate same-public-ID draft and duplicate callbacks while the original action is still genuinely pending.

## Required state semantics

A known refused stage is terminal for that original attempt. Release only its mutation ownership, matched by exact operation/draft/admission; preserve a newer operation's slot. Keep a stale editor non-writable until the operator explicitly acquires a current record. Do not rewrite its ExpectedProjectAdmission or ExpectedLifetimeId to the replacement. A later fresh editor can use its own owner-issued admission.

The project-save receipt remains confirmed when only seeding is refused. Delete refusal must not emit Project deleted, remove a valid replacement card, create a fake cleanup entry or invoke a current dialog's Close. Explicit refresh reads state; it does not retry a write. Do not falsely report that an earlier committed project was rolled back.

A small typed presentation state or safe owner-outcome mapping is preferable to catch-all heuristics. Exception type alone is insufficient outside the verified stage: do not generalize all InvalidOperationException/IO/cancellation as pre-write refusal. Preserve actual partial deletion receipts and their exact recovery IDs.

## Negative controls that must remain green

- Lost save/seed/delete acknowledgement remains genuinely unknown; no automatic retry, no blanket unlock on a list refresh.
- A delayed old refusal cannot change a newer operation's busy state, message, acknowledgement, route or draft.
- Invalid original profile/lifetime does not acquire replacement authority.
- A successful admitted seed happens once and removes/marks only the submitted planned rows.
- An active original pending mutation still blocks conflicting same-target actions.
- Other projects and saved histories remain unchanged; the fix adds no database migration or writer semantic change.

Report which assertions failed before the repair and passed after it, with current owner-backed discovery and execution. Once closed, proceed to both P2 Files surfaces in the same task.
