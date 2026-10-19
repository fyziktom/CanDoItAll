# Process decision runtime, publication fence and continuation

Design refinement after signed native recovery checkpoint `14f830e92`. This follows
[immutable Process admission](P07-process-policy-admission.md) and the
[nested source review](../reviews/P04-P07-nested-decision-review.md). It is prospective:
the admission foundation is implemented, but no Process/Workflow activation is claimed.
The preliminary [unsupported-policy authority correction](../validation/2026-10-19-process-policy-authority.md)
is now implemented and validated. [Local Process budget persistence](P07-process-budget-persistence.md)
also has [41 passing cases](../validation/2026-10-19-process-budget-persistence.md).
The [generation lifecycle refinement](P07-process-decision-generations.md) now has
[29 passing pure cases](../validation/2026-10-19-process-decision-generations.md), and
[atomic generation storage](P07-process-generation-persistence.md) has
[101 passing selected cases](../validation/2026-10-19-process-generation-persistence.md).
Actual effect-generation fences, inherited allocation and governed runtime work remain
prospective; storage does not authorize effects through the existing owner refusals.
The [child publication condition](P07-process-child-publication.md) now has
[108 passing selected cases](../validation/2026-10-19-process-child-publication.md).
It supplies stable occurrence identity, exact lookup and an enlisted root/generation
check. Actual effect callers and source/manager authority still need to compose it;
the existing policy guards remain closed.
The [versioned execution admission design](P07-process-execution-contract.md) binds an
exact adapter identity/version/owner while preserving schema 1 launch bytes, with
[114 selected passing behaviours](../validation/2026-10-19-process-execution-contract.md).
No executor activation follows from this declaration.
The [inherited material and child runtime refinement](P07-inherited-material-and-child-runtime.md)
records the remaining shared-scope, budget, exact reservation and journal-version work
before actual producer activation.
Its first [immutable material value checkpoint](../validation/2026-10-19-process-inherited-material-value.md)
passes 44 selected cases for original capture age and exact scope/content integrity.

## Source facts and rejected shortcuts

The current Agent Process executor supplies auto-approved tools and a
`ProcessStepOutcomeResult` schema. Agent planning instead requires explicit approval,
its own task-plan schema, a Project Structure source and a latest interactive chat.
Its Workbench admission reads Workspace/Agent preferences itself. Setting PlanningOwner
on the existing Process request would violate all four contracts. Manufacturing a chat,
changing SourceKind to ProjectStructure, or removing those checks is rejected.

`ProcessAgentExecutionAdmission` reserves by actual source plus Process run/step.
Claim rotation intentionally finds the same Agent execution. Workflow assignment lookup
and intent comparison similarly identify the original child. A semantic successor must
extend all of reservation, persisted child binding, lookup and fingerprint with the same
owner-issued generation; changing an idempotency string alone is incorrect.

Agent publication and mapped Workflow mutation/continuation already take the same
PostgreSQL root advisory transaction lock as `EfProcessRuntimeUnitOfWork`. They check
actual claims, readiness, source and Project lifetime. None yet checks decision generation.
The runtime unit of work starts its own SQL transaction; an outer service cannot assume
its decision update and a later step-state command are atomic. Agent journals are files,
so their acknowledgement and the SQL owner effect cannot be made atomic by a new flag.

The Workbench module already references Process Application and Agent Core. It can own
the actual task-plan adapter without adding a dependency from Process Runtime/Application
to Workbench, MAF or Workspace. The Process module must not copy Workbench material or
receipt codecs. Process Application owns neutral execution contracts; composition wires
the business participant. Keep the existing ordinary executor unchanged until an explicit
supported governed contract selects the new path.

## Ownership and lifecycle

| Owner | Responsibility |
| --- | --- |
| SharedKernel | Owner-neutral policy and allocation arithmetic; no runtime, storage, source authority or generation coordination. |
| Process Runtime | Immutable decision/generation references, bounded state transitions and child enrollment using the shared arithmetic; no provider, UI, EF or business JSON interpretation. |
| Process Application | Admission/manager orchestration and neutral business-owner ports; exact source expectations, explicit supported-contract selection and resumable decision references. |
| Process Persistence | Decision heads/history, child enrollments, reservations and work allocations in the canonical dataset; CAS, root fence and atomic step/decision transitions. |
| Process module | Actual Agent/Workflow dispatch adapters, retained child recovery and result translation into the Process outcome contract. |
| Workbench | Task basis, output schema, permission checks, effect publication and historical receipt interpretation. |
| Agent/Workflow runtime | Its own durable provider/effect journal and actual child occurrence; consumes validated inherited input and budget without substituting current settings. |

Decision identity is independent of claim/attempt identity. A logical decision has one
owner-issued ID, an append-only positive generation and a CAS revision. Retrying or
rotating a claim preserves all three semantic facts; a new revision does not itself
mean a new generation. A generation records policy provenance, material-scope/basis
reference, child bindings and terminal historical results. Immutable assignments retain
their original admission; do not mutate Prompt, LaunchVariables or policy to replan.

Start with separate indexed decision-head/history and child/allocation records. Do not
append unbounded child history to the frequently loaded runtime-state JSON. Each lookup
must address a root/decision/generation or exact child, with bounded scans for reconciliation.
Preserve absent legacy admission and its existing fingerprints. New execution contracts
need an enforced version/capability handshake, including stored definition/assignment,
before any new preference is selectable. Template compatibility labels alone are not a
runtime fence against an older writer silently ignoring fields.

## Supersession and cross-store effects

Use a two-phase quiescence protocol, not an unprotected preflight generation check:

1. Under the existing root SQL fence, CAS the expected active generation into Reconciling.
   This immediately rejects new publication/child enrollment for that generation. An
   effect already holding the fence may finish first; its receipt remains historical.
2. Release the transaction. Reconcile every enrolled child from its actual durable
   journal and business receipt. No provider, network, file-journal read, Project lease
   acquisition or user interaction occurs while holding the root SQL lock. Unknown
   effects keep the generation Reconciling. Cancellation is not proof of no effect.
3. Reenter the same root fence and compare the exact reconciliation revision and complete
   enrolled set. Only sealed accepted/definitely-no-effect outcomes permit a successor.
   Append the successor and its step-state change in one owner transaction. Keep old
   generations/results and all consumed/reserved allowance. A repeated manager command
   resolves its original transition; it must not create another successor.

Every covered Agent effect, Workflow dispatch, Workflow continuation and native review
publication must validate the exact enrolled generation inside that fence through commit.
Receipt reads remain separately authorized after supersession. A missing decision binding
on a policy-enabled assignment is refusal, not a legacy fallback. Admission already
refuses this policy on current executors and stays closed until these paths are proven.

Preserve existing lock order: catalog/source and Project/dataset leases precede the
effect transaction and root fence. Supersession never takes those outer leases while
holding root SQL locks. Reconciliation may gather immutable evidence outside the fence,
then persist only exact owner-verified outcomes after comparing the enrolled set. The
Agent file store and Process SQL store remain distinct owners with explicit uncertain
states; do not infer atomicity or release from cancellation/AsyncLocal state.

## Snapshot and budget inheritance

RunSnapshot captures one bounded declared material scope before dependent model work.
Enrolled children reuse that same material for the covered scope. StepSnapshot captures
at a new logical decision, not a retry. A child cannot silently replace inherited run
material with a fresh step read. A refresh of a shared run scope acts at the owning
parent decision and fences affected outstanding children. Completed child results remain
completed history; they are not executed again merely because the parent generation changes.

The existing numeric ceiling intersection is necessary but does not provide a shared
work ledger. Concurrent children need durable bounded allocations from the enclosing
decision before dispatch. Reserve maximum admitted work/replan allowance atomically,
settle unused allowance only from exact closed child evidence, and leave uncertain
allocations charged/reserved. Resume, worker restart and claim rotation never reset the
ledger. Preparation, provider work and publication count under the admitted rules;
waiting for a human releases active work but retains its consumed total. Clock reversal,
overflow, exhausted allowance and missing settlements fail explicitly.
The [allocation arithmetic foundation](P07-process-decision-budget.md) now has isolated
Unit proof. Its local Process ledger now commits with runtime state; actual child-owner
verification and enclosing allocation wiring are still required.

Allocations reserve a child-specific allowance rather than copying the parent's entire
remaining budget into every child. Full reservation can limit parallelism, so the
producer must expose and validate its bounded allocation plan before launch. Do not
silently shrink a requested allowance or let concurrent children each spend the same
remaining total. Capture/reconciliation/parent orchestration work also needs accounting;
provider-only timing is incomplete. Actual measured work can exceed a grant when a
provider ignores cancellation. Retain that overrun and block further work/publication;
do not reject settlement in a way that loses the consumed work or makes it available
again. Unknown work remains reserved until exact closed owner evidence is available.

The inherited scope, publication mandate and allocation must be part of the original
child fingerprint and actual provider-input validation. A parent budget supplied as
request metadata is insufficient. Unsupported nested/backend combinations must be
rejected before provider work until their actual owner can hold the required fence.

## Pause/review/resume and rollout

A governed executor returns a typed durable decision reference with Waiting. The manager
can read its original result after restart without acquiring a new claim or provider call.
Approval, refresh and cancellation carry the exact decision expectation and current
manager authority. A review cannot be derived from an unrelated chat's latest run.
Known acceptance maps through the owning business receipt into the Process outcome;
an unknown publication keeps its original continuation, rather than starting a new child.

The existing runtime command idempotency row stores outcome and reloads current runtime
state on replay (`EfProcessRuntimeUnitOfWork.FindCompletedCommandAsync`). That is not an
exact historical decision-transition receipt. New manager commands must retain their
original expectation, parameter fingerprint and exact resulting generation/outcome;
a retry after later transitions returns that original result, with current state only
as a separate observation. Never infer the original approved generation from today's head.

Review deliberately releases the original claim. Governed publication therefore needs
an exact decision/child continuation authority tied to the active waiting parent and
current manager action, distinct from initial provider dispatch authority. Do not make
the old claim appear current or drop claim checks from ordinary dispatch. This path must
retain source/project/dataset checks and the decision fence through the effect commit.

Generation and step-state transitions must enlist together in the existing unit of work;
use a narrow decision persistence collaborator rather than another runtime partial or
general-purpose hook framework. Transport retry continues the same decision. Manager
rework must reconcile/quiesce the enrolled generation before its new StepReady event;
the existing instruction-repair path cannot rewrite immutable policy or material.

Implement and prove the generation/child fence first, then actual supported execution,
producer settings, manager continuation and Workflow occurrence integration. Product
settings stay inactive until their real producer/executor/restart path passes. Legacy
ordinary execution remains explicit and does not claim governed snapshot behavior.

## Required proof and performance gates

Before adding generation storage, close the existing unsupported-policy authority gap.
Executor refusal alone is insufficient: direct Agent reservations and mapped Workflow
owner calls can reach their persistence guards independently. Keep historical reads
bound to the original source, but mark policy-bearing legacy dispatch non-current in
both authority readers. The enlisted mutation guards then reject it under the existing
root lock. Workflow continuation intentionally permits released claims, so it needs an
explicit unsupported-policy check on both the observed and locked current assignment;
requiring a current dispatch claim there would break legitimate legacy continuation.

This is a local correction within Process Persistence. No new interface, project
reference, fingerprint domain, SQL round trip or runtime partial is justified. Prove
admitted and late-substituted policies cannot reserve, publish or continue; prove the
historical authority remains readable and the original legacy path still succeeds.
Exercise the real mapped Workflow credential continuation with policy substitution
both before observation and between observation and its transaction. Existing negative
claim/source/lifetime cases remain part of the selected regression lanes. This guard
increment does not implement generation storage or enable policy settings.

Pure tests must prove generation CAS, immutable bindings, transition legality, idempotent
manager commands, all-child closure and shared allocation accounting without the old
executor/runtime. PostgreSQL tests must prove both publication/supersession orderings,
stale/null/foreign generation refusal, restart, rollback and atomic step/decision state.
Test unrelated roots progressing while one root is blocked. Use real Agent reservations
and Workflow child lookup to prove claim retry stability and semantic successor separation.

The real producer must capture original Workspace/definition/step preferences once;
later settings edits cannot alter them. Inspect provider input against the retained
basis, including nested scopes. Exercise crash points between enrollment, provider
entry, effect commit, journal acknowledgement and manager continuation. A seeded
consumer-only test cannot close these producer/lifecycle requirements.

Define bounded generation, enrolled-child, material-byte and allocation counts before
schema implementation. Measure admission, capture and lock-hold time separately. No
global mutex, whole-catalog revision or complete event-history scan substitutes for
the indexed decision fence. Run the focused owning lanes with confirmed discovery,
required migrations/static checks and actual manager browser proof before activation.

## Architecture baseline

CodeAnalytics `snap-20261019161427-d1ce2c09`: Application, Runtime, Persistence and Process
module; 946 types, 8,689 members, one recognized service registration, 22 entities,
319 findings, 190 diagnostics and zero cycles. Workspace-load blocking diagnostics
remain pre-existing; actual builds are still required. The focused query confirms
existing Workflow continuation checks the original deferred child/claim/event range.
Do not weaken those checks when adding decision generation. Compare the same scope
after the implementation and run the architecture closure gate.
