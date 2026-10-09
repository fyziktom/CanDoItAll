# Required state and operation behavior

Use the current canonical seam guidance and existing project patterns such as mutation gates and submitted-draft reconciliation. Reuse an existing small mechanism where its ownership fits; do not introduce a framework or service bag solely to satisfy this table.

## Three different identities

| Identity | Changes when | Must not be confused with |
| --- | --- | --- |
| Opening/authority | Actual view opening or edited aggregate changes; captured database/profile/project lifetime is retired | A new read, tab or harmless parameter echo |
| Read request | Filter/tab/history/window/refresh observation supersedes a previous read in that lane | Cancellation/rollback of a native mutation |
| Mutation operation | One admitted semantic command with exact origin and immutable submitted values | A mutable global `submission` slot or the newest read generation |

Do not serialize authority-bearing tokens into URLs, local storage or the light rendering library. Backend admission is independently enforced. An A→B→A transition produces a new A opening even when public IDs match.

The host may expose presentation-level busy flags and actions through the existing session seam. The UI cannot acquire native authority by manufacturing an opaque token. Preserve the actual native command/receipt types unless a demonstrated boundary defect requires the smallest compatible addition.

## Authoring command-family floor

| Family | Specific risk | Required result |
| --- | --- | --- |
| Definition Save/Publish/Archive/Delete | Accepted revision lost when a tab/refresh increments workspace generation | Capture exact result; preserve later edits, native identity and a precise read-back state |
| Role Add/Save/ApplyTemplate/Delete | Single mutable submission; Add/Delete return a new selection | Origin-aware gate, authoritative selected identity and cleared deleted draft without overriding a later user choice |
| Step Save/AddBranch/AddArtifact/MapSubprocess | Hidden metadata from a different step; row identity/normalization | Exact edited-step metadata, immutable submitted collections, field-aware stable-row reconciliation |
| Canvas intents | Generic gate can drop useful gesture completions or apply old geometry | Correct original target/version, bounded/coalesced pending intents where appropriate, no lost final user intent |
| Template import | Refresh/selection can lose accepted import or retarget it | Exact source/target captured before await, one accepted import, read-only reconciliation without reimport |

Do not accidentally change the independently repaired launch, default-feed, live-operator or cancellation operation identities while fixing these lanes.

## Same opening, overlapping read and write

The active write is admitted once and owns its immutable submission and local completion slot. A same-opening read may continue and update independently safe read regions, but cannot erase that write's receipt or reset its busy state. If the read includes an editor projection, reconcile it conservatively against the active/acknowledged operation rather than replacing the draft unconditionally.

After native acceptance, record outcome/identity/version first. Apply accepted data to untouched fields and preserve fields typed since dispatch. Request a fresh observation when required. A read started before acknowledgement must not overwrite that acknowledged state. Opaque version tokens are not sortable; use actual operation/read lineage and owner concurrency semantics. If ordering cannot be established, preserve the draft and show a conflict/reconciliation state rather than claiming an older response is newest.

A dependent read failure does not convert a known accepted write into a retryable write. A genuinely unknown result blocks blind replay, retains recovery identity and exposes the owner's safe reconciliation path. Do not classify an ordinary deterministic validation refusal as unknown.

## Admission and retirement

Double-click, Enter and alternate command buttons for the same mutable draft share admission. A newer read cannot release that admission; a stale finally cannot release another opening's slot. Independent views must not share mutable editor state through a circuit-scoped service.

A real opening change retires its presentation. Late success/error/finally/notification cannot alter the successor, reopen dialogs or clear its busy state. Native work may still have committed; do not cancel real processes as a side effect of closing UI. Dispose read resources after their task unwinds and observe faults. Preserve read-only operations and unrelated working regions after a transient failure.

## Selection and semantic fields

For Add, adopt the owner-assigned role identity and selected role when the submitting selection is still current. If the operator selected another role while waiting, preserve that successor, retaining the accepted addition in the appropriate catalog/result state. For Delete, clear the deleted raw draft, baseline, validation, submission and dialog state; select a surviving returned role or an explicit empty state as current UX requires. Do not reconcile a deleted row's fields onto its replacement.

For steps, resolve the actual edited step before constructing the submitted Basic/operation/contracts/branch/binding/artifact/subprocess data. Store `DecisionRoleKey` with that draft and accepted baseline. Test nullable values and metadata hidden by the active tab. Bindings must never be inferred from a different projection-selected step or reset to default because no input currently displays them.

## Subsequent typing and row normalization

Compare current fields with the submitted snapshot, not only with the initial baseline. Keep changed fields/raw invalid text; adopt accepted values and assigned identities for untouched fields. Retain the actual draft/validation lifetime where necessary for Server-tab remount.

Pair branch/artifact/role-binding rows by their stable semantic keys. Handle accepted additions, deletions and remapped identities explicitly. A changed title must not overwrite an accepted normalization of another field in the same row. Retention/loop numeric text must correspond to its own row and the correct accepted/submitted revision. Determine the narrowest required field-level policy from actual owner behavior; do not generalize a large JSON merge engine.

An explicit discard, known rejection or retirement must close the matching pending-submission lifecycle. A later command cannot accidentally accept an earlier submission. A discard reachable during a pending accepted write needs an explicit policy: it can discard local post-submit edits, but cannot pretend the already-committed native change was rolled back.

## Proposed regression names (new tests, not existing results)

```text
Definition_accepted_save_survives_same_opening_refresh
Definition_stale_read_does_not_regress_acknowledged_version
Authoring_reentry_cannot_replace_active_submission
Role_add_adopts_returned_selection_only_for_eligible_origin
Role_delete_clears_retired_draft_and_selects_survivor
Step_save_round_trips_decision_role_of_locally_selected_step
Step_later_row_edit_preserves_owner_normalization_of_untouched_field
Authoring_retired_opening_never_updates_same_id_successor
```

Use actual repository namespaces and current fixture patterns. Tests must hold/release completions deterministically, await event dispatch correctly and assert native command counts/arguments, selected identities, raw values, outcomes and cleanup—not private field names or number of files/classes.
