# Process planning launch policy

Process launch accepts an explicit decision policy for the Workbench Project task planner.
It prepares a task proposal for review; it does not publish tasks automatically. Omitting
`decision` retains the ordinary Process execution contract.

The first supported scope is an operator launch for a current Project, one executable
Agent activity, and an Agent permitted to plan Project tasks. Additional artifacts, tools,
business host capabilities, branches and subprocesses are refused before preparation.
The standard managed execution capability remains part of the plan and runtime contract.
Agent-tool and inherited Process launches cannot use this option to obtain new root funding.
The published activity must explicitly allow `ExecuteExternalAction`; selecting the planner
does not add this operation to an existing definition or assignment.

## Select and review

Read `GET /api/processes/launch/decision-contracts` for registered execution identities,
operator descriptions and supported policy combinations. Each `supportedPolicies` entry
contains a complete policy whose durations and replan count are owner ceilings, not the
effective policy of a launch. Discovery needs Process read permission and does not grant
execution permission. The existing broad legacy API grant retains its normal access.
An empty catalog means no planning contract is registered.

Use the returned identity rather than hard-coding the example below. The catalog performs
no preparation, settings read or Agent/provider work. It does not certify that a selected
definition, Project or Agent is eligible. The launch check still validates those facts and
returns the actual policy after applying saved Workspace and requested bounds. Keep a
prepared intent's original selection even if a later catalog read differs.

Use `POST /api/processes/launch/check` with a nonempty `callerIntentId`, the Project and
published definition selection, and this optional addition to the normal launch request:

```json
{
  "decision": {
    "version": 1,
    "execution": {
      "contractId": "a8a1bce7-dfbd-40a2-8b5c-6c15f1558790",
      "version": 1,
      "executor": 0
    },
    "policy": {
      "readBasisMode": 0,
      "onBasisChange": 0
    }
  }
}
```

This is a request fragment. `executor: 0` identifies the Agent owner; `readBasisMode: 0`
selects RunSnapshot and `1` selects StepSnapshot. `onBasisChange: 0` selects Fail and `1`
selects PauseForReview. Either preference may be omitted independently. Omit `policy` to resolve supported values
from the saved Workspace policy. Unknown versions and unregistered execution identities
are refused. The execution identity is an external protocol contract, not an Agent ID.

| Choice | Capture point | When publication facts have changed |
| --- | --- | --- |
| RunSnapshot | The root captures its original shared material before dispatch. | Fail or pause for review, as admitted. |
| StepSnapshot | The activity captures material when its original work is prepared. | Fail or pause for review, as admitted. |

Both choices require current facts at publication. This first single-activity planner does
not support automatic replanning or pinned stale publication.
It does not refresh material silently. A multi-activity Process needs its own qualified
lifecycle before these capture points can express differences across successive steps.

Fail is evaluated when the original proposal attempts publication. A changed basis
publishes no tasks, closes the child and Process as failed, and settles the original work
grant. The shared run material scope closes with its existing Cancelled phase for a failed
root; this does not relabel the Agent or Process outcome as cancellation. PauseForReview
leaves the Process pending with its original grant reserved. Neither response alters the
provider's starting input while it works. Approval replay retains the original outcome;
it does not retry publication or call the provider again.

The Workspace policy must already be saved with a real settings revision. The owner bounds
work duration and basis age to at most ten minutes and permits zero automatic replans;
stricter Workspace/request bounds remain effective. Read `launchPlan.decision.policy` in
the response for the actual policy instead of assuming requested values were accepted.

## Accept and reconcile the original intent

The SPA project Processes page offers definition and planning-contract selection, an
explicit read mode and change response or saved Workspace defaults, the returned review,
and acceptance. Only advertised combinations can be selected for a fresh preparation.
It retains one pending intent per browser tab before sending a request. Recovery content
contains only the original selection and identifiers; the effective review is retrieved
from the server under the current caller. Browser storage must be available. A retained
intent for another project links back to that project instead of replacing it.

Definition catalog, overview, role and step reads accept an optional `projectId` query.
They use current project overlays and lifetime admission; omission preserves global reads.
Empty IDs are rejected and missing/deleted projects return 404. Editable drafts are not
silently published when selected for a launch. Read permission is not execution permission.

If the preparation response was lost, use
`GET /api/processes/launch/intents/{callerIntentId}` before replaying the check. This read
verifies the original caller, database profile and operator surface. It never prepares or
continues work. A found observation supplies `admissionId`; include it as
`preparedAdmissionId` when restoring the saved review through the original check request.
Missing or forbidden observations must not trigger a fresh preparation in another dataset.
A 404 is an observation at that instant, not proof that an in-flight request cannot finish.

1. Keep the caller intent, request selection and returned preparation admission identity.
2. Review the effective policy and the normal launch plan.
3. Send the same selection and intent to `POST /api/processes/launch`, including the
   returned `preparedAdmissionId` and the deliberate `execute` choice.
4. With `execute: true`, the normal background dispatcher owns execution. Observe the run
   and planning review; do not start a second unrelated launch because execution is pending.
5. Use the returned planning review expectations and approval identity for publication.
   Retain the original review action identity when reconciling its acknowledgement.
   Each HTTP attempt has a separate activity operation ID: omit `activityOperationId`
   to let the server assign it, or supply a fresh value. Reusing that one-use activity ID
   returns a duplicate-operation conflict; it is not the durable review action identity.

Settings changes after preparation do not rewrite that intent's original policy, settings
namespace/revision, budget or shared-scope identity. Same-intent preparation races retain
one winner. Changing the selection with the same intent conflicts; an explicitly new
intent may capture current defaults and creates separate work. Current authority is still
checked at the normal acceptance, execution and publication boundaries.

Unsupported selection/plan/policy returns HTTP 400 with `process.launch_decision_rejected`.
Missing operator/task authority or caller intent is refused with HTTP 403. Reusing an
intent with different input returns HTTP 409 with `process.launch_intent_conflict`.

The SPA sends bounded, single-attempt requests and never automatically retries a write.
After an uncertain acceptance, recover the original intent; even a `Prepared` observation
cannot prove that the earlier acceptance will not commit later. Retry acceptance with the
same admission and execution choice. A known accepted run can be observed or explicitly
continued when its scheduling is pending. Failed or authority-reconciliation states need
operator attention and are not presented as completion. No automatic navigation assumes
that a backend UI route exists in the SPA.

An unaccepted review may be explicitly discarded if acceptance was never attempted;
preparation alone cannot start its run. After acknowledged acceptance, the operator may
explicitly prepare separate work. Clearing that local recovery slot never cancels or
repairs an existing run. Closing the browser tab loses tab-local recovery evidence; retain
the caller intent/admission externally when that recovery horizon is required. This UI
does not provide a cross-device pending-launch inbox or planning-publication review page.

## Ownership and compatibility

Process Application owns selection and immutable admission binding. Workbench owns the
task-planning semantics, supported policy, selected Agent check and Workspace read.
Persistence saves the original preparation and existing runtime/budget/scope admissions.
The provider never replaces the plan, assignments, source identity or prompt.

The optional request and review fields preserve legacy fingerprints and omitted-null
serialization. Selected preparations use a separate hash domain so older readers reject
them instead of silently ignoring governance. No schema migration or new project reference
is required for this addition.

New Fail launches save a version-6 step admission with an explicit child response grant.
The initial Agent binder copies that verified grant into its immutable inherited origin.
Dispatch and original receipt reads require equality with the saved grant. Legacy step
admissions and inherited origins without this marker preserve their original pause
semantics, even if their parent policy says Fail. Older strict codecs reject the new
admission before child enrollment and reject the new inherited origin after enrollment.
Do not roll an active Fail launch onto binaries that cannot read these contracts.

See the [design and qualification status](../codex/bundles/concurrent-mutations/plan/P23-process-policy-producer.md).
Native selection/review controls, planning-publication UI, authored definition policy
defaults, Workflow activation, parent refresh/replan and broader lifecycle remain open.
SPA integration stays on branch **codex/concurrent-mutations** for colleague review.
