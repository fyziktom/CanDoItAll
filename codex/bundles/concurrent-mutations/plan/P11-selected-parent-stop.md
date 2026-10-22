# P11 selected inherited Agent stop: implementation entry candidate

Entry reviewed after signed e4eadf619. Implementation is retained, but [qualification](../validation/2026-10-21-selected-parent-stop.md) is blocked: all 58 new cases pass; an existing receipt checkpoint repeatedly exceeds its unchanged deadline, and 98 selected cases remain unexecuted.
The verified monotonic persisted cancellation barrier is a prerequisite. The broader
root worker, finalization fence, snapshot refresh and producer activation remain separate.

## Boundaries and capability

Add a selected Core stop owner over the existing execution-run reader, atomic mutation
store, cross-instance dispatch lease store, cancellation registry and clock. It never
references Process, Workbench, a provider or a tool dispatcher. Its input is an exact
execution ID plus a dedicated original-parent-stop capability, carrying the retained
AgentPlanningInheritedOrigin and an asynchronous stopped-parent validator. Do not widen
Dispatch/ManagerReview continuation purposes. Validate schema, original origin/session,
metadata and immutable background execution fingerprint on every retained run read.

Extract the existing background identity/fingerprint helper into one small internal
owner used by the journal and stop capability. Retain serialized field order and bytes;
ordinary dispatch still compares the complete current profile including generation.
Stop validates against the original profile, while its concrete parent validator fences
the current profile ID/fingerprint and a stable current generation across waits. A
historical journal's generation is never rewritten to make a restarted host accept it.

Workbench supplies the concrete task-plan adapter through a typed Application child-stop
contract keyed by the existing execution contract. The adapter validates the retained
task-plan target, original child/material/policy/source evidence and exact current root.
For this first stop slice, require root CancelRequested or Cancelled; do not infer a
permanent stop from a failed step alone (failed steps can legitimately be reworked).
Use the original receipt-authority reader, which delegates only to Process SQL reads,
and selected runtime reads. These checks never acquire an Agent file gate. Invoke them
before signalling, after a dispatch-lease wait and through the file writer's existing
pre-publication authority callback. No Process transaction stays open across file waits.

## Stop state machine

The registry gets an exact execution-ID operation backed by its existing dictionary.
A live registration receives cancellation and yields Signalled; it is not a settlement.
An absent/disposed registration permits inspection but never proves work has stopped.
Attempt the real dispatch lease with the existing 250 ms bound; distinguish LeaseBusy
from caller cancellation. After acquisition reread the selected detail and revalidate.

Open measured work, unresolved provider dispatch, executing/reconciliation-required
proposals, or unknown effect outcomes remain explicitly pending. Prepared undispatched
proposals may be rejected only under this lease. Do not use HasUnresolvedEffects as a
blanket refusal, because it includes Prepared. Preserve terminal accepted/failed/cancelled
generation outcomes and effects; return AlreadyClosed only with no unresolved work.
An inconsistent terminal record fails explicitly instead of being rewritten.

For an idle nonterminal generation with closed work and no unresolved effects, reuse
AgentSavedEffectCancellation and the journal's existing cancellation transitions. Extend
its existing helper only with an explicit parent-stop reason so logs do not claim an
approval action when none occurred. Retain measured work, original grants, proposals,
approvals and committed receipts. Already terminal replay performs no new revision or
charge. FileSandboxWorkspaceStore already validates the supplied authority even for
unchanged payloads, then returns before writing; preserve that behavior.

The legacy Process cancellation observer must exclude inherited schema-7/origin records
both from initial selection and from the detail reread. It may still signal local work,
but cannot directly change an inherited run to Cancelled while leaving its journal open.
The dedicated selected owner supplies mutation evidence for later terminal accounting.

The legacy observer also has an existing indexed candidate port available:
ListExecutionRunCandidatesAsync(new ExecutionRunQuery { ProcessRunIds = processRunIds }).
The canonical FileSandboxWorkspaceStore resolves selected process-run IDs through its
execution index and loads only those records. Use this existing port, retaining the
observer's explicit ID/state filters because the candidate contract may return a
superset. Its compatibility default is existing behavior, not a new fallback. Prove the
canonical indexed path avoids unrelated runs; do not claim a page bound from Take,
which the candidate port does not enforce. Selected inherited recovery remains O(1)
per child and never calls the workspace-wide enumeration.

## Required proof

Use exact existing-owner fixtures, not fabricated approval/source rights. Pure tests:
selected signalling/isolation/disposal, immutable fingerprint compatibility, original
origin/session/metadata mismatch, explicit pending classifications, unchanged work and
terminal effect preservation. Real file tests in both snapshot modes: pending approval
stop, replay without revision change, post-restart current-generation fence, busy lease,
caller cancellation, lost acknowledgement, changed parent/profile before publication,
and preserved unknown work/effects. Exercise the concrete adapter with actual Process
SQL: stopped root required, sibling/foreign child refusal, changed accepted source,
stop followed by terminal accounting, and publication-versus-stop ordering.

Do not enable root finalization or a scanning worker in this slice. The next design must
define bounded fair progress over the existing root-funded allocation limit (256),
legacy unfunded children and interrupted captures. The registered selected stop contract
must be used by that recovery path; avoid a second independent cancellation mechanism.


## Entry architecture review and exact implementation choice

Core baseline is `snap-20261021235859-ee223b1b`: 257 documents, 791 types,
5,863 members, 1,054 edges, three existing cycles and 226 findings. Workbench baseline
`snap-20261022003520-81f98ea8` has 1,172 types, 10,139 members, six existing cycles
and 402 findings; 57 package warnings and 16 DI interpretation diagnostics remain.
Process module `snap-20261022003907-2b8ac856` has 359 types, 2,914 members,
zero cycles, 142 findings, 123 package diagnostics and one DI interpretation diagnostic.
Their workspace diagnostics are not compiler success; affected normal builds and actual
project references are required. Compact full baseline findings are retained under the
existing owned accounting-recovery investigation directory.

Use AgentBackgroundExecutionIdentity for the shared immutable predicate/fingerprint,
AgentInheritedParentStop for the dedicated capability, and AgentInheritedPlanningStopper
for selected execution cancellation. Keep the existing journal as a thin caller of the
extracted identity checks; add no partial file or provider dependency. Return explicit
Stopped, AlreadyClosed, Signalled, LeaseBusy, OpenWork, UnresolvedEffects or incomplete
terminal-work states. Core pure tests use only the narrow ports and retained data.

The Application child-stop contract selects by the existing execution contract; Workbench
owns ProjectTaskPlanProcessChildStopper. Reuse the existing target record's immutable
source matching in the outcome reader and stop adapter, without changing current read
or publication authority. The actual module composition root constructs Core from its
existing canonical file ports. Do not inject IServiceProvider into runtime behavior.

Existing manager cancellation is rejected as the implementation shortcut because it
requires active review/publication authority and would either refuse the stopped parent
or require weakening those checks. Existing cancellation-notification repair cannot
supply durable journal evidence. No new project or generic recovery framework is needed.
The selected owner must remain callable without constructing the broad workspace runtime,
and its adapter must be proven through actual DI, SQL and file owners before recovery
integration. No acknowledgement counts or local registration absence establish quiescence.

### Bounded root-barrier observation

Source review identified that IProcessRuntimeStateStore.LoadAsync materializes the root's
steps and claims. Four calls per selected stop would scale with unrelated root work.
Use IProcessRootCancellationBarrierReader in Application and a small EF implementation
in Process Persistence instead. Each call uses a fresh short-lived context and one
EXISTS query over the exact root primary key, matching RootRunId and a durable
CancelRequested/Cancelled status. It returns no aggregate or history and holds no SQL
transaction across the Agent file wait. There is no default aggregate-loading path.
The Workbench adapter still validates the original source and current profile generation.
Prove the four selected header probes through actual SQL interception. This changes no
project reference, schema, publication authority or cancellation-state transition.
