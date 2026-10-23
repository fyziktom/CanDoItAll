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
      "readBasisMode": 0
    }
  }
}
```

This is a request fragment. `executor: 0` identifies the Agent owner; `readBasisMode: 0`
selects RunSnapshot and `1` selects StepSnapshot. Omit `policy` to resolve supported values
from the saved Workspace policy. Unknown versions and unregistered execution identities
are refused. The execution identity is an external protocol contract, not an Agent ID.

| Choice | Capture point | When publication facts have changed |
| --- | --- | --- |
| RunSnapshot | The root captures its original shared material before dispatch. | Pause for explicit review. |
| StepSnapshot | The activity captures material when its original work is prepared. | Pause for explicit review. |

Both choices require current facts at publication. This first single-activity planner does
not support automatic replanning, pinned stale publication or the parent Fail response.
It does not refresh material silently. A multi-activity Process needs its own qualified
lifecycle before these capture points can express differences across successive steps.

The Workspace policy must already be saved with a real settings revision. The owner bounds
work duration and basis age to at most ten minutes and permits zero automatic replans;
stricter Workspace/request bounds remain effective. Read `launchPlan.decision.policy` in
the response for the actual policy instead of assuming requested values were accepted.

## Accept and reconcile the original intent

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

## Ownership and compatibility

Process Application owns selection and immutable admission binding. Workbench owns the
task-planning semantics, supported policy, selected Agent check and Workspace read.
Persistence saves the original preparation and existing runtime/budget/scope admissions.
The provider never replaces the plan, assignments, source identity or prompt.

The optional request and review fields preserve legacy fingerprints and omitted-null
serialization. Selected preparations use a separate hash domain so older readers reject
them instead of silently ignoring governance. No schema migration or new project reference
is required for this addition.

See the [design and qualification status](../codex/bundles/concurrent-mutations/plan/P23-process-policy-producer.md).
Native and SPA selection/review controls, authored definition policy defaults, Workflow
activation, parent refresh/replan and broader lifecycle support remain separate work.
SPA integration stays on branch **codex/concurrent-mutations** for colleague review.
