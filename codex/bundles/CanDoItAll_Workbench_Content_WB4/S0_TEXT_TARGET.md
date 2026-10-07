# WB4-T1: preserve the original text-asset destination

## Source-derived finding, not a claimed runtime incident

S10/S11/S12 show a concrete unbound path in the old, deferred text-asset family.
CreateTextAssetAsync constructs ProjectStructureTextAssetCreationContext(ProjectId,
CreateTextAssetNodeAsync). The coordinator captures that record but uses ProjectId chiefly
for diagnostics. The creator delegate calls CreateObjectAsync without capturedSurface or
capturedNavigationRevision. That method falls back to the route's live surface and obtains
its current admission. Adjacent task creation already captures its original action context.
WB3's generic composer opening protection does not automatically protect a separately
opened text dialog. S11's reload retires native authoring openings; the independent text
coordinator is not one of them.

The observed code therefore permits an old text submission to reach a writer prepared for
current state rather than its actual original destination. It is not enough to notice that
a different project's missing parent usually causes native rejection. Use the same public
identities under a replacement lifetime or a held preparation across a real route transition
so that incorrect authority substitution cannot hide behind an unrelated invalid fixture.
We have not run the reproduction; first establish it with actual rendered/native paths.

## Required failing-first tests

Use the existing native component harness, original text dialog and actual writer/storage.
Use task-completion barriers at preparation/owner boundaries, not sleeps or private-field
injection that bypasses public entry behavior. Retain source hashes before first execution.

- Open text creation for project A/node N, fill valid content, hold a native preparation or
  its browser upload. Move the route or recreate A and N with a new project lifetime; load
  that new surface. The old submission must never write with the new lifetime's admission.
- Exercise the same case before dispatch (refuse without write) and after the original
  native write was accepted (retain original receipt; no second create, no successor refresh).
- Reopen A after B: identical names/IDs do not reactivate old callbacks. Include changed
  node record/kind/parent with an unchanged public NodeKey and a changed authenticated actor.
- Close A/open B in the same dialog host, delayed upload completion, delayed ordinary error,
  and two independent mounted pages. Old completion cannot close B, replace its fields,
  reset its busy state or publish stale success as B's result.
- Prove a normal create and a known-created/follow-up-failed case still work. Capture actual
  node/storage identities, byte hashes and unchanged neighbor/root rows; not just a toast.

If a current native mechanism already prevents a proposed branch, document the exact guard
and retain its negative control. Do not force the fixture past it merely to manufacture a bug.
The unbound delegate must still be audited for every real caller, including encoded upload.

## Repair boundary

Capture original native context at the point the feature is opened: project/profile/lifetime,
node/parent occurrence, actor, opening and receiver. Pass a snapshot-bound creator, not a
reference to the mutable page. Keep authorization in the existing owner and enforce exact
expected participants where supported. Do not treat a read-time UI token as authority.

Freeze source mode, content, file identity and all metadata before the first await. Record
native acceptance before later links/placement/notification. Do not clear evidence on close.
Accepted old work may finish for its original owner; only publication is fenced separately.
A typed rejected-before-write result can permit a corrected submission. A known saved node
requires read-only recovery; a genuinely unknown create cannot be retried blindly.

No schema migration, automatic project recreation, generic operation journal, global dialog
lock, silent data cleanup or disabling text creation is authorized as an easy workaround.
Native security/identity surprises beyond this bounded plumbing get a precise call/commit
map and an explicit blocker for that path, while safe independent work can continue.
