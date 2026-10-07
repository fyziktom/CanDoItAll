# Process integration: full Workbench family, not the Processes product refactor

## Actual surfaces

Complete link selection; confirm/estimate; variables/context; full-screen staffing role rail
and overview; required gaps; candidate cards and complete directory picker; read-only Agent
details; exact candidate-switch confirmation; manual matching; separately confirmed HR match;
reviewed-plan Save and close; Start reviewed run; accepted-run navigation; preparation restore;
explicit new launch; observation/link-delivery/recovery statuses. Reuse all four actual child
dialogs, not placeholder cards. [S10-S15]

## Native semantics that must survive

`ProjectStructurePage.Processes.cs` already bounds the definition catalog and separates
project/global scope. `ProcessPreparations.cs` stores only caller intent in sessionStorage
and resolves the saved prepared plan natively; it restores exact authority, variables and
link target. It distinguishes spent admissions from active continuations and performs original
pending link delivery. Preserve that protocol. Browser storage is not a new durable run store.

The saved native preparation contains the reviewed roles, assignments and exact definition
version. Changing these must invalidate or explicitly reprepare the review under the existing
contract. A stale picker cannot switch the currently selected role by reusing a displayed
index. Estimate-only may prepare native state as currently specified, but cannot execute,
provision, grant capability or imply approval. HR matching is an explicit effect with its own
captured request/result; no automatic real-model invocation on open/render.

An accepted Process run is not equivalent to delivered graph linkage. Preserve admission,
run ID, provisioning/continuation state, link-delivery status and public failure separately.
Recovery observes/completes the original native operation; it must not start a replacement
run. Opening a saved accepted run is navigation. Prepare another launch retires only the
appropriate retained browser intent and uses an explicit new operation.

Use current ProcessLaunchApplicationService, ProcessLaunchProducerRequests, operator authority,
PreparedLaunchStore, ProjectProcessLaunchTargetQuery and ProjectProcessLaunchDeliveryService.
Do not construct runtime assignments manually, call an easier runner, write Process tables,
or loosen Project Structure control-plane/approval requirements.

## Models, prices, and authority

Friendly provider/model labels in candidate cards, details and tooltips must match the source
catalog even for source-managed providers. Opaque IDs stay in native routing. Missing provider,
unknown price, incomplete historical evidence and true zero are distinct. Preserve per-role,
step and candidate identities, human/AI executor differences, requirement/readiness gaps and
provisioning availability. Never add process/project/file/secret access merely because a
candidate is selected in the UI.

## Tests and actual small Process

Test both original confirmation and staffing paths, valid/manual/required-gap choices,
late estimates, nested picker replacement, cancelled switch, two concurrent preparations,
old role IDs, saved version/input drift, actor/project lifetime change, restored accepted
intent, known rejection, timeout with accepted admission, pending graph delivery, explicit
new intent and repeated restart. Prove real native rows/receipts and no duplicate runs.

For the customer rehearsal choose the smallest supported real Process with one or two useful
steps and explicit roles. Use a native definition with a published version, actual Agent
assignment and ordinary capabilities. Prepare via Workbench, review exact assignments, start,
complete the real work and inspect the final run plus a concrete output artifact under the
expected project. A deterministic fixture run is separate from a real-model run. No hidden
simulation flags, no stopped-at-Prepared result presented as Completed, no automatic HR match
or giant multi-agent plan just to demonstrate a simple process.

The standalone Processes UI is preserved, not extracted in WB6. Small newly exposed defects
needed for this native path can be repaired; a fundamental governance/schema redesign is a
blocker to map, not an overnight speculative rewrite.
