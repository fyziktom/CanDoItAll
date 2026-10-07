# Review of Resources readiness and Workspace Settings Core

## Decision and evidence scope

Keep the architecture. One bounded Files interaction defect is identified below; reproduce
it before repair. Then continue into API Access in the same assignment. There is no reason
to redo the four Core sections or reopen Memory/Scheduler wholesale.

Review baseline is `186ab60a8e1a2dd7ab79c71e1a256cb0b2fdaedf`; the preceding commit archives
the last handoff. The two-commit comparison to the previous Resources implementation was
read. This is source/metadata review, not execution. EV01, EV02.

## Preserve these changes

**Resources RS-R1:** exact editor acquisition now has `EditorAccess` separate from
`catalogAccess`. A successful catalog refresh does not complete the exact editor load;
same-ID retry is a no-op only for an acquired editor. `CanMutate` is checked by the handler.
Historical acquired records and independently stale optional references are not collapsed
into placeholder readiness. RS01.

**Dependency direction:** Core Contracts references existing Security.Abstractions; Core UI
references Core Contracts, History.Abstractions and BaseLib. Separate defaults/secrets/files/
history controllers preserve distinct ownership. Namespace preservation in a moved Security
value type is not a reverse project dependency. The boundary tests contain actual transitive
and unresolved negative controls. Declared references and test source were read; the
implementer's evaluated graph was not rerun here. WS01–WS04, WS12–WS14.

**Secrets:** exact editor acquisition is explicit; stale returned models have sensitive
references cleared. Metadata observation does not load plaintext. Receipts contain safe
identities and diagnostics, not a command copy. UI Save requires an existing record when an
ID was loaded; the non-UI upsert remains separate. After ambiguous metadata acknowledgement,
keeping staged vault payload avoids deleting bytes that durable metadata may reference.
Do not restore speculative cleanup to make a test simpler. This intentionally permits an
orphan on an actually failed commit; redesigning vault recovery is outside this slice.
WS02, WS07–WS10.

**Defaults/history/route:** accepted normalization and currency publication follow persistence;
history retains explicit Load, expected version and bounded shorter-retention confirmation.
The route keeps active-only deferred hosts and the Providers redirect. Machine-local Files
is not retired as a selected-database owner. WS01, WS04–WS06, WS15, WS16.

## WSC-R1 — a path edit prevents adoption of a confirmed destination extension

### Source-level mechanism

`WorkspaceFilesController.MutateAsync` accepts the owner's result, but both setting
`Draft.Selected` to the saved extension and normalizing the visible extension are inside
`ReferenceEquals(Draft, draft) && draft.Revision == revision`. A later change to only the
executable path increments that same revision. The owner has saved the requested destination,
but `Selected` can remain the previously selected source extension. Delete subsequently
captures `draft.Selected`, not the extension shown in the edited field. WS03, WS17.

### Deterministic reproduction

1. Load and select an existing `.sample` override, using the actual Files surface.
2. Change the extension to `.next` before Save; changing the extension is an upsert to a
   different destination, not a rename of `.sample`.
3. Hold the real owner-port Save after the request for `.next` was captured. While it is
   pending, edit only the executable path with the actual immediate input event.
4. Return a known successful result for `.next`, complete the metadata refresh and observe:
   the receipt names `.next`, the extension field still shows `.next`, and the later path
   remains, but `Draft.Selected` still names `.sample`.
5. Invoke Use system default. The current implementation requests deletion of `.sample`.
   This is not the now-confirmed destination being edited. No actual executable needs to run.

The existing file-lifetime test replaces the entire draft using `New()` while awaiting a
write. That correctly protects a successor, but does not cover the same live draft with a
path-only edit. The reviewed test range contains no equivalent regression. WS18.

### Required behavior, without overcorrecting

Separate acceptance of the captured destination identity from reconciliation of editable
fields. When the same editor still targets the submitted normalized extension, adopt the
confirmed extension even if its path was edited later; preserve the later path and form
context. Do not let a successful write keep a destructive action silently attached to a
previous extension. An explicit target label/confirmation may reinforce clarity, but is
not a substitute for consistent editor identity.

If the extension itself changed while Save was pending, or an editor was replaced, do not
assign an earlier result's identity to that successor or normalize away its raw text.
Track meaningful target revisions, including extension A→B→A; do not infer a new lifetime
merely from a field's eventual equality. Treat committed warnings identically for identity;
a genuinely unknown result must not be promoted to a confirmed save.

Do not delete the original extension as part of saving the destination. Do not change file
preference storage, host binding, migration or local launch policy. Keep the same owner port
and independent Files authority. Refresh and review remain reads, never replay.

### Proof required

Failing-first controller and actual-input renderer tests for the reproduction; confirm the
next Delete targets `.next` and `.sample` remains. Include new drafts, unchanged target,
later target edits, A→B→A, New/select-other/dispose, committed-warning/read failure and
unknown results. Exercise one production adapter path using private control-plane storage
and harmless existing executable fixtures without launching them. No fixed sleeps.

This review did not execute those reproductions; source reasoning is the finding, and
fresh regression execution is the implementer's responsibility.

## Previous test receipts are not a clean broad run

The maintained Core record reports 15,571 executed Stable cases, 15,567 passed, four failed,
zero skipped. It separately reports focused repairs of the four test-maintenance failures,
including seven shell tests and 24 guard tests; there was not a second all-green broad run.
Treat that as a completed mixed checkpoint plus targeted repairs, not a single passing
full suite. Raw local TRX/screenshots were not supplied to this reviewer. EV03.

S0 must refresh the relevant Files/Core and Resources evidence on the actual checkout.
Do not rerun every previously completed module or the long Stable aggregate merely because
another UI section is beginning. Apply the current repository's named widening triggers.
