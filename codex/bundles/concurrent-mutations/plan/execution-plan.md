# Execution plan

Status: P01a closed; P02a original-state implementation and focused validation committed
as `1452352f0`. P02b durable lifecycle, receipts, enlisted search, protected HTTP and
governed tool paths are implemented in the working tree; native P02c recovery is under
validation. The transfer snapshot-order repair, complete producer cutover, receipt
capacity and large-definition tail-cost investigation remain open. No phase closure
is inferred from these intermediate results. No production source change is authorized by a phase's
proposed status alone; its entry evidence must be satisfied. The user has authorized
implementation after design review, tests and incremental commits; no extra approval
ceremony is required for dependency-ready work within that scope.

| Phase | Coherent outcome | Dependencies | Entry/closure evidence and status |
| --- | --- | --- | --- |
| P00 | Refreshed architecture, complete first-owner perimeter and risk/test plan | Source package/current repository | In progress: independent persistence/runtime/UI reviews; baseline Infrastructure build passed with existing NuGet warnings |
| P01a | Transaction cleanup always releases acquired process gates | P00 cleanup design review | Pass: six discovered/passed tests, Infrastructure Release build, independent diff review and unchanged portability enforcement; see validation/P01a-cleanup.md |
| P01b | Commit knowledge, source lock order and lifecycle namespace defined/proven | P00; P01a for lock fault proof | Implemented candidate: explicit file/SQL activation, retained epochs and bounded exact receipts; lifecycle and recovery lanes passed. Complete transfer locks and offline command process proof remain open. |
| P02a | Projects definition handler and original-state contract | P00/P01 applicable guarantees | Conditional-write checkpoint recorded in validation/P02a-validation.md: builds, focused owner/HTTP/tool/UI/browser, migration/import, model/script, rendering/docs and portability pass; performance comparison retained with P02b tail-cost follow-up; no durable-replay or broad Stable closure claim |
| P02b | Complete Projects callers and durable result/projection | P02a/P01b | In progress: SQL receipt/search and protected Projects HTTP/governed tools implemented. Boundary lane 56/64 passed; eight failures repaired pending rerun. Transfer lane 75/77 passed; table-count and real snapshot-order failures under repair. Legacy producer inventory remains open. See validation/P02b-foundation-progress.md. |
| P02c | Draft-preserving conflict and unknown-result recovery | P02a/P02b | In progress: strict IndexedDB store, native frozen submission and known/unknown reconciliation implemented. Lightweight sessions 20/20 and capture bound 1/1 passed. Native/browser candidate validation and explicit metadata-only retained-reference disposal remain open. |
| P03 | Selective narrow edits reduce unnecessary conflicts | Closed P02 | Pending: retain first scalar command, add only measured useful intentions; no automatic collection merge |
| P04 | One governed task-plan publication with trusted bounded basis | P02; selected Workbench owner witnesses | Pending: source/task writer inventory, owner minimum policy, atomic set witness and publication/approval proof |
| P05 | Remaining owner waves have complete original-state protection | Closed relevant foundations | Pending: coverage inventory, owner-specific contracts and closure per module; no claim all engine data is protected by P02 |
| P06 | External effects have explicit conditional/idempotent/uncertain semantics | Relevant owner wave | Pending: actual driver capability matrix and recovery/compensation proof |
| P07 | Agent/process/workflow settings persist and control real execution safely | P04 plus actual affected owners/effects | Pending: settings, durable admission/checkpoints, policy-bound approvals, claims, bounded replan, UI and actual producer-to-owner proof |
| P08 | Complete release/operational closure | All required work | Pending: compatibility, migration/restore, measurements, frozen broad gate, portability and owned-resource cleanup |

Parallel work is allowed for read-only inventory and disjoint documentation. Production
work on the Projects command contract, writer and callers is ordered; do not independently
invent DTOs. Builds sharing standard outputs remain serialized by the parent agent.

## Validation rules

Each selected work card records exact affected projects, filter, expected discovery,
invalidation keys and results. Later phase envelopes above are not falsely declared
implementation-ready: decompose them using the then-current accepted contract before
their source edits. Existing test class names are discovery seeds, not passing proof.

Foundation/schema/public-contract changes trigger a single broad stable checkpoint
when that integrated candidate is frozen. Repeat only after a relevant invalidating
change or failure, never after an evidence-only edit. Apply final portability-static
enforcement to every protected-source change and refresh only reviewed baseline deltas.

## Reopen rules

Reopen the owner design if another writer, incompatible caller, lock inversion,
unbounded source read, missing receipt knowledge or dependency cycle is discovered.
Reopen runtime policy if the selected publication cannot validate its material inputs
atomically or a replan can duplicate an uncertain external effect. Invalidate dependent
proof when source/migrations/contracts/asset mode change. Do not substitute prose for
missing SQL, browser, process or retained-data evidence.
