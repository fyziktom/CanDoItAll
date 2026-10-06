# S0: WB3-H1 — a hierarchy completion must not use a successor dialog

## Source-derived finding

`ProjectStructurePage.ProjectHierarchy.cs` reads `projectHierarchyDialog` before
calling AddSubproject/ReconnectSubproject, then reads that mutable field again to
assign an error, compute target selection and success text, and close the dialog.
`CloseProjectHierarchyDialog` sets the field to null, and the visible Cancel action
is available while that call waits. The open path also assigns its loaded dialog
after awaiting choices without an opening identity. S17/S16.

The analogous block-conversion path in `ProjectStructurePage.NodeMutations.cs`
reads its live dialog after Reclassify returns. Transfer has some immutable local
capture and typed partial-result handling, but still closes or republishes its
original dialog/readback without a complete opening fence. Do not erase that
existing partial-result logic while fixing presentation ownership. S18/S19.

This is a code-derived execution path, not a reviewer-executed reproduction.
It does not show an authorization bypass or prove that the first native write
failed. Establish the exact behavior with failing-first tests.

## Mandatory controlled sequences

1. Open Add A; select a valid child and submit. Pause at the real owner boundary.
   Cancel A; complete a successful owner action. No null dereference and no new
   dialog may appear. The original hierarchy result remains accurately reported.
2. While A waits, open B with different subject/selection and a draft error/input.
   Complete A successfully. B remains open and unchanged; no B-target navigation.
3. Repeat A with known rejection and with an unconfirmed completion. Only A's own
   outcome record may change; B's error/busy state stays untouched.
4. Close/reopen the same public target, A -> B -> A, duplicate submit, Enter versus
   toolbar submit, and two independent Structure instances.
5. Pause the available-project read. Retire the opening, route/profile/lifetime or
   actor; an old result cannot reopen the dialog or replace a newer choice list.
6. Apply the same suite to block conversion. For subtree transfer, preserve target
   creation receipts, compensated-empty-child outcomes and committed transfer
   recovery IDs even if the originating dialog or page no longer exists.

Use existing native test seams, controlled database/owner barriers and actual
renderers. Do not replace persistence with an echo fake and call it native proof.
Do not use sleeps as the concurrency control or assert only that no exception was
thrown: assert exact dialog identity, original native effect and untouched neighbor.

## Smallest correct repair

Separate dialog opening identity, live draft, immutable submission and native
outcome. Capture subject/current parent/selected child or target node, project
admissions and receiver before the first await. A known completion belongs to that
submission, not to whichever dialog the field contains later.

Prevent duplicate admission in the handler. Retiring a view may cancel unaccepted
reads; it does not roll back an accepted native mutation. Clear a busy slot only
when it still belongs to the completing operation. Close or reload only the
matching original view. Maintain accepted IDs/recovery facts in a bounded native
owner result, independent of presentation liveness.

Do not make a global DialogService change, refuse all close actions indefinitely,
catch every exception as success, replay an unknown create, or rebuild hierarchy
transactions. Preserve existing Projects hierarchy validation and typed transfer
outcomes. Continue into WB3 after this bounded repair is proven.
