# Process planning review API

The concurrent-mutations branch provides a review boundary for an already accepted
Process task-planning child. Governed executor activation remains gated by parent
refresh/cancellation, recovery, compatibility and capacity work. This API alone
does not enable new governed Process or Workflow runs.

Use GET and POST on
`/api/processes/runs/{runId}/steps/{stepInstanceId}/planning/{executionRunId}/review`.
Both require current Process and Agent read/write/execute plus Project read/write
grants. The original launching source must still allow disclosure; write admission
also enforces its original publication authority. General API access does not
substitute for the explicit grants of a managed credential.

GET returns the original child, displayed Agent and Process expectations, the
publication preview and available choices. It creates no action and changes no
decision. Its states mean:

| State | Meaning |
| --- | --- |
| `AwaitingChoice` | The displayed original proposal has an available manager choice. POST independently rechecks it. |
| `RecoveryRequired` | Accepted or unresolved work cannot be offered as a new approval. Retain the original request. |
| `Settled` | Child work is accounted for and a terminal result is observed in the original Process runtime. |
| `Unavailable` | The original Process no longer admits that displayed review selection. |

`parentPolicy` describes Process capture. `childPolicy` describes the inherited
Agent's fixed assigned material and finite grant. A StepSnapshot parent still
produces a RunSnapshot child. Snapshot selection never relaxes publication
freshness, source lifetime, permission or current-generation checks.

Before POST, retain the complete request and its new `agent.actionId`. Copy the
displayed Agent expectation and publication binding/approval ID into `agent`, and
the displayed Process expectation into `process`. Choose only an offered approve
or reject action. The Process completion key is the Agent action ID. An optional
`activityOperationId` identifies activity reporting, not a replacement action.

All revision fields are canonical positive decimal **strings**, including returned
child revisions. Preserve them without JavaScript number conversion. Requests are
limited to 24 KiB of UTF-8 JSON, including chunked requests; duplicate, unknown,
incorrectly cased and missing required members are rejected. Responses use
`Cache-Control: no-store`.

A successful Agent publication can coexist with `accountingPending=true`.
An uncertain acknowledgement or access refusal does not prove that publication
failed. Preserve the exact request and use it for recovery; never create another
action to repeat the effect. Exact retry reads retained intent and evidence without
another provider call or publication. Missing evidence can keep recovery pending.
If the original request is lost, explicit Process accounting reconciliation is
required; the intent digest cannot reconstruct it.

Results always set `requiresRuntimeReload=true`. Process command replay retains
original decision/budget receipts but reloads runtime state. Neither an old response
nor its observation time is an ordering token for an editor draft.

See the [HTTP design](../codex/bundles/concurrent-mutations/plan/P07-process-review-http.md)
and [validation record](../codex/bundles/concurrent-mutations/validation/2026-10-21-process-review-http.md)
for current evidence and remaining activation gates.
