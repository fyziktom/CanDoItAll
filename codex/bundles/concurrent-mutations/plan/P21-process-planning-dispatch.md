# P21: connect admitted Process planning to the real dispatch path

Status: qualified with P22 on 2026-10-23; [all 588 final cases pass](../validation/2026-10-23-process-planning-dispatch.md).
Design was reviewed before implementation. P20 is frozen separately;
its pending signed commit is not changed by this successor. This slice connects an
already admitted task-planning contract. Public definition/settings admission,
shared refresh, Workflow planning and sustained capacity remain separate gates.

## Problem and source evidence

The registered Process dispatcher prepares root material and accounts actual child
results, but `AgentFrameworkProcessStepExecutor` still rejects every non-null
decision policy. Integration fixtures call `ProcessDecisionStepPreparationCoordinator`
and `IAgentInheritedPlanningWorkspaceService` directly. Consequently their passing
proof does not establish that the actual execution adapter can perform this work.

The ordinary executor requests an automatically approved `ProcessStepOutcomeResult`.
The task-planning owner requires a different provider response and explicit publication
review. Reusing the ordinary request or removing its unsupported-policy guard would
break the admitted contract. Workbench already owns the participant, source/basis
binding, closed startup evidence, historical outcome reader and manager continuation.

## Boundary and minimum edits

Add one provider-neutral `IProcessDecisionStepExecutor` port in Processes.Application.
It identifies its exact immutable execution contract and accepts the existing typed
adapter request. A synchronous metadata-encoding delegate accepts the original
`ProcessRuntimeStepAssignment`; the Process module supplies its existing encoder.
This is protocol serialization, not a provider call or an authorization callback.
It avoids both copying metadata rules and a Workbench-to-Modules.Processes reference.

The existing module executor delegates only a registered exact contract. Missing,
unknown or Workflow contracts retain the explicit unsupported result. Duplicate
registrations fail composition. Workbench registers the actual project-task executor
and owns its business validation, original input, Agent request and result mapping.
No new project/reference, generic coordinator, partial class or service locator is
needed. Application must not acquire Agent, Workbench, SQL or SDK dependencies.

## Execution and concurrency protocol

1. Validate the exact step and claim, load the current runtime and one indexed original
   accepted input, and require the supported single-activity contract. Check both the
   captured requirements and supplied adapter contract before allocating work. Require
   the original Agent executor, Project lifetime and task-write source. Use the original
   accepted prompt and assignment for metadata; mutable assignment repair cannot replace
   them. Current source/claim checks remain with their existing authoritative owners.
2. Prepare the selected step through its existing coordinator, with the child's work
   bounded by the dispatch timeout and retained parent/local budgets. RunSnapshot uses
   the root's exact material; StepSnapshot captures once for this logical generation.
   Only a freshly committed enrollment may create a missing Agent journal. A replay
   uses the existing-only start mode and never manufactures a replacement child.
3. A concurrent preparation with no child is a typed pending dispatch, not a fabricated
   execution ID or a failed result. Extend the dispatcher's existing pending catch to
   preserve the winning claim. Active child work uses the existing pending exception.
   Neither path submits a result or releases the claim on behalf of another caller.
4. Construct the inherited workspace request from the actual enrolled child, original
   policy/material/allocation, original input, exact claim metadata and original Project.
   Recheck the parent enrollment before admitting/reusing a journal. The existing source
   policy and initial binder verify the candidate, current authority and profile.
5. Delegate provider work to the real inherited workspace lifecycle, preserving its
   lease, activity, cancellation, work accounting and no-automatic-approval behavior.
   Do not hold SQL transactions or root/catalog locks across provider work.
6. Map only verified owner state. Approval/basis changes and uncertain outcomes require
   manager reconciliation. Active work remains pending. Closed startup failure carries
   its existing in-memory measured evidence to the actual result coordinator; unknown
   startup/save acknowledgement must not fabricate a refund or permission to restart.
   The dispatcher retains sole ownership of atomic result/decision/budget submission.

## Failure and cost review

Retain original deterministic child and allocation identities across duplicates, lease
rotation and acknowledgement loss. Unsupported obligations fail before enrollment.
Source revocation, root cancellation and Project/profile changes remain fenced at the
existing admission and publication boundaries. No exception text, prompt, provider
response or private configuration enters public diagnostics. Log run/step/child and
typed disposition only; leave sensitive exception evidence with existing owners.

The added boundary performs constant-size registry selection and one selected accepted
input read. It does not decode the full launch, enumerate siblings/history or recapture
material on replay. Existing bounded preparation and file/SQL waits still dominate
latency. Existing capacity/default-worker gates are not waived by this integration.

## Validation before acceptance

- Build each changed production owner and the actual Web/composition and test outputs
  in standard Release, serially. Compare evaluated project references and scoped
  CodeAnalytics before/after, including collector diagnostics and existing cycles.
- Unit/contract coverage: exact registered routing, unsupported/ambiguous contracts,
  original metadata input, and pre-enrollment rejection of unsupported obligations.
- Real PostgreSQL/file/MAF transport: invoke the registered adapter in both snapshot
  modes; preserve one original child, basis and allocation; require review; submit via
  the actual Process result owner; complete the exact reviewed publication once.
- Hold the provider/preparation boundary and race a second adapter call: no duplicate
  request, claim release, child or accounting. Replays and lost/absent child evidence
  cannot create fresh work. Changed source/claim/root and closed startup failure retain
  original accounting. Record original failed candidates and exact repair boundaries.
- Confirm filtered discovery before execution; refresh source/output identities after
  edits. Reuse the isolated PostgreSQL fixture and resource observer. Run full
  portability and rendering/documentation checks. Preserve historical cleanup denials.

No new UI is part of this slice. It cannot by itself claim user-selectable Process or
Workflow policy support. Those producers must explicitly admit supported contracts
and persist the chosen policy after their own inheritance/refresh qualification.
