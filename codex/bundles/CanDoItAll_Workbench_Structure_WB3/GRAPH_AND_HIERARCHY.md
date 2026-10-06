# Graph, clipboard and structural dialogs

## Clipboard remains a native structural command

The current private clipboard records operation, project, SurfaceId and root IDs.
Copy/Cut capture a normalized editable forest; Paste requires one valid destination
and the original surface. System-managed nodes, projected parent/subproject targets,
cycles and the unsupported Duplicate action have explicit restrictions. Preserve
these; do not add cross-project clipboard or browser-local business persistence.
S15.

A public SurfaceId is not a lifetime. Bind the copied roots and destination action
to original project admission and source-view origin where required. Test navigation
A-B-A, profile/lifetime replacement, source deletion, replaced roots, target mutation,
concurrent paste and two independent views. At the native writer retain the original
IDs and exact policy. If a copy returns an ID map and omitted boundary links, keep
both before any UI selection/refresh. Do not confuse Cut's retained node identities
with Copy's newly allocated identities.

Exercise parent/child selected together, disjoint roots, no editable roots, a target
inside the cut subtree, and preserved unrelated external references. Existing owner
restrictions on copying canonical tasks, retained history, file bindings and managed
content are not waived by this bundle.

## Hierarchy and reclassification

Move the real first three structural sections of `ProjectStructureCanvasDialogs`:
project hierarchy, block conversion and subtree transfer. The current component also
contains unrelated Process/Workflow/runtime/file sections; split these by real
ownership instead of pulling the whole mixed component into the new leaf. S16.

Capture opening + draft + immutable request. The displayed choices do not replace
native membership/cycle validation. Preserve add versus reconnect (including exact
old parent), multiple-parent semantics and the existing Projects modal handoff.
A different selected row or newly loaded choice list must not rewrite the target
of an accepted operation. Retry after known validation is different from observation
of an unknown write.

Block conversion has intentional metadata transformations. Preserve that current
contract and test retained IDs, links and content; do not declare every removed old
subtype field an accidental loss, or clear every unrelated extension field to simplify
mapping. Compare to native reclassification semantics, not just the new DTO. S18.

## Transfer is not an atomic UI save

`ProjectStructureSubprojectTransferCoordinator` has existing overloads for explicit
reserved target IDs, captured native owner/authorization, creation receipts and
partial transfer/recovery. It preserves a target when no original creation receipt
was acknowledged; it does not deduce rollback from a missing reply. Keep this
mechanism. S19.

The authoring dialog must retain source project/lifetime, source roots and draft,
original target/creation receipt and transfer disposition through all waits. If
transfer committed but assignment reconciliation remains, display its original
recovery ID and safe owner action. If empty-child compensation succeeded, retain
that fact and the original cause. Never delete a now-modified target merely because
it was originally created by this UI operation. No new generic saga framework.

## Deletion and cleanup

Reuse the exact existing delete prompt, storage-disposition choices, native owner
and pending recovery identities. WB2 added correct original-prompt checking; the
canvas context menu and keyboard path must not bypass it. A late confirm/cancel for
A cannot act on B. Preserve references/history and the existing physical cleanup
rules; this is not permission to broaden file deletion.

Postcommit cleanup or failed UI reload cannot relabel the graph mutation as absent.
Recovery must use the original owner and retained evidence, not fresh IDs read from
the current selection. Test no-write refusal, known commit, partial cleanup and
unknown acknowledgement separately, including repeated close/reopen.
