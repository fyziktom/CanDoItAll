# Nested snapshot and decision-generation source review

Status: read-only source review; no runtime activation, build or test. Reviewed the
nested-delegation/generation paragraphs in `plan/P04-P07-runtime-policy.md` against
actual admission owners. The intended semantics are consistent: children share the
parent's admitted basis for that material scope; executor retry does not recapture;
semantic replan is an explicit successor in the original lineage. Card A's numeric
budget ceilings do not implement those runtime rules.

The statement that the selected owner must fence supersession is a future requirement,
not an existing generation coordinator. Current source has these concrete gaps:

| Actual seam | Required guard before enabling the policy |
| --- | --- |
| `Modules.Processes/Services/RuntimeIntegration/ProcessAgentExecutionAdmission.ExecuteAsync` builds `ExecutionRunSourceKey.ForBackground` from actual source plus Process run/step and reuses/reconciles that execution. | Bind a persisted semantic-decision identity and original basis into the actual same-source reservation contract. Calling again or changing a JSON generation field cannot create a new semantic attempt; normal claim rotation/retry must continue to resolve the original admission. Do not rewrite old run/step IDs or derive a new source from client arguments. |
| `WorkflowProcessStepExecutor.ExecuteAsync`, `CreateIdempotencyKey` and `RequireSameChild`; `IWorkflowProcessAssignmentRunQuery.FindAsync` | Current child lookup and caller key use Process run + assignment; more than one child is refused. Add the same immutable decision dimension to owner capture, retained-child query, key and intent comparison together. A new key alone still selects the old child or creates ambiguous history. Preserve the original Workflow version and old receipt lookup. |
| `WorkflowProcessDispatchBinding.HasSameIntent` | Intentionally ignores rotated ClaimToken while comparing original owner/input/contract fields. Preserve that transport-recovery property. A new decision generation needs an explicit versioned material field/fingerprint; it must not accidentally equate two semantic attempts or use claim token as the decision identity. |
| `EfProcessExecutionMutationGuard.RequireDispatchAsync` and `EfProcessWorkflowDispatchAuthority.RequireForMutationAsync` | Existing root advisory lock, enlisted owner context and current claim/attempt checks are usable infrastructure, but neither reads a current decision-generation record. Supersession CAS and every covered child publication must take the same root/decision fence in a documented order, validate current generation, and retain it through the effect commit. A preflight generation read is insufficient. |
| `EfProcessWorkflowDispatchAuthority.ReadContinuationCoreAsync` / `RequireForContinuationAsync` | Existing continuation requires the exact retained child, released deferral claims, active waiting parent and complete original event range; later StepReady invalidates old continuation. Do not reinterpret a fresh replan as ordinary continuation or weaken these guards. Original-generation receipt reads remain separately authorized even when a new generation cannot publish. |
| `ProjectStructureWorkflowAuthority.MappedProcess` held source/continuation leases | These revalidate original catalog/source policy and invoke the enlisted Process guard. They do not confer a generic parent-generation lease. A child needs an owner-supplied generation guard through this existing boundary; no product SQL query in Workflow runtime. |
| `WorkflowLaunchIdempotencyRequestFactory.CreateFingerprint/CreateScope` and `WorkflowProviderInputAdmission.RequireAsync` | Existing fingerprint and retained disclosure declaration bind input/origin, immutable definition/settings and node occurrence. They do not retain decision policy/basis/generation. Add those to the versioned declaration/admission and actual provider-input validation. Current protected-read disclosure is authority/provenance, not proof of parent snapshot reuse. |
| `ProcessRuntimeStepAssignment`, prepared-launch serialization/hash and immutable assignment comparison | No current typed policy/basis/generation field exists. Preserve legacy omitted-field meaning and immutable launch evidence. Runtime decision state must not be implemented by repairing immutable assignment/prompt/LaunchVariables in place. |

Supersession must cover **every affected enrolled child**, not merely the one proposal
that first detected a material change. A child can already be in an owner commit while
the parent decides to replan. Linearize parent generation change against those
publication guards; reconcile all potentially accepted old effects before generating
their replacements. A commit before the supersession point remains historical fact;
a child after it is refused. The old generation and accepted effects cannot disappear
when its last active claim is released. Test both orderings with independent owners.

For SQL-owned Process decisions in the same canonical dataset, the existing enlisted
root lock is a concrete place to extend that comparison. Agent file-journal and SQL
effect publication are not automatically one transaction. Workflow backends likewise
need their actual persisted occurrence/continuation owner. Cards E/F/G must specify a
real held source lease or enlisted decision guard and its supersession write path;
if that owner cannot fence the delegated material scope, reject that nested policy
combination before provider execution. Do not introduce a universal coordinator to
paper over these distinct boundaries, and do not claim an AsyncLocal or cancellation
token provides the fence.

No additional prospective test count is introduced here. Strengthen the existing
nested-execution/replan cases to inspect actual provider inputs, exact retained basis,
stable identity on claim rotation, decision-dimension child lookup, and both
supersession/child-commit orderings. R1 journal recovery and B1 basis capture remain
independent prerequisites; neither alone closes these activation guards.
