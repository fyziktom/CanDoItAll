# Agent, process and workflow decision policies

Status: original architecture recommendation for the concurrency design freeze, 2026-10-16.
The implementation note below records later changes; the remaining design is not a
claim of complete runtime activation.
The architect's September archive is reference material; its proposed decisions and test
catalogue are not evidence that current production behavior already satisfies them.

## Implementation note, 2026-10-19

Typed Workspace and Agent policy settings are persisted. The shared policy mapper and
explicit saved-effect MAF adapter have passing focused proof: the latter restores one
original module effect under current authority without a provider continuation. See
[runtime adapter validation](../../../codex/bundles/concurrent-mutations/validation/2026-10-19-saved-effect-runtime.md).

The implemented schema-six contract adds a planning decision to the existing
tool journal. Models owns its strict bounded graph/codec; Core owns its transitions and
central persistence checks. Original input, policy provenance, bases and publication
origins are immutable. RunSnapshot reuses one basis per generation; StepSnapshot uses a
distinct basis per logical step. Existing schemas 1–5 cannot acquire a fabricated decision.

Work accounting retains one active interval and cumulative spent work. A new logical
step settles/reopens at the same instant in one publication. Only an explicitly closed
approval/review wait is excluded; refresh and automatic replan retain prior expenditure.
Clock rollback and overflow refuse further activity. Cancellation preserves an open
interval rather than inventing a settlement. The live owner now refuses a fresh start
when a restart has lost that interval's monotonic anchor; copied persisted data is not
execution authority. It records late responses after cancellation, invalid publication,
budget exhaustion or clock rollback without creating new effects. Clock failure preserves
the original accounting anchor and reports an explicit refusal. See [the source card](../../../codex/bundles/concurrent-mutations/plan/P04-planning-decision-journal.md)
and [the 114-case live-owner proof](../../../codex/bundles/concurrent-mutations/validation/2026-10-19-live-planning-owner.md).

The configured MAF provider port now uses a fresh SDK session for each step and sends
only the original request/target and selected basis. The actual transport boundary
checks input identity, current read permission, capacity and remaining active work.
Invalid output cannot become an effect; bounded late responses remain recorded.
The [provider proof](../../../codex/bundles/concurrent-mutations/validation/2026-10-19-planning-provider.md)
covers real SDK/file owners and an intercepted configured HTTP transport. Product
admission, saved actions and UI activation remain required.

Workbench publication v2 now requires a typed guard on the held authority for active
work. It checks before staging, after a project wait and immediately before commit;
material currentness and basis age still apply. V1 receipts retain their original
wall-time interpretation, and mixed versions are refused. The
[47-case publication proof](../../../codex/bundles/concurrent-mutations/validation/2026-10-19-active-work-publication.md)
qualifies that perimeter. The saved-action owner now opens the approval continuation
interval atomically, preserves work across Refresh/replan and checks the live capability
against the held selected publication snapshot. See its
[146-case proof](../../../codex/bundles/concurrent-mutations/validation/2026-10-19-planning-saved-actions.md).
The module participant and product admission still need their integrated
provider-to-publication proof and user-facing activation.

## Decision

Keep three independently enforced contracts: the facts used to decide, permission to
perform the effect, and the target state the effect may replace. A start snapshot is a
legitimate computation input. It never grants permission to overwrite newer target
state, to use a retired Process claim, or to reuse an approval for a different effect.

Implement the first governed planning publication in the Workbench owner, through typed
Projects reads and publication checks. Reuse the current Agent tool admission journal,
semantic digests, owner receipts, Process claim fences and immutable executable
definitions. Do not add a universal mutation coordinator, a second approval system, a
global read-dependency collector, or another partial file on the large execution service.

The product must expose both a data policy and the response to a detected change. A
single `ReloadWhenChanged` boolean cannot express the difference between continuing an
analysis, safely publishing a result, and redoing an approved action.

The initial governed-planning default is `StepSnapshot`, `RequireCurrent`,
`PauseForReview`, with zero automatic replans. A stale owner publication is always a
typed no-effect rejection. `PauseForReview` describes what the durable execution host
does with that rejection: retain the proposal and enter a review-required state after
releasing every lease, transaction and connection. It does not suspend an HTTP request
or keep an execution claim locked. This is preferable to discarding expensive planning
work, and avoids both automatic overwrite and unexpected provider spending. An
explicitly selected `Fail` policy makes that same rejected publication terminal.
A stateless operation without a durable review contract supports `Fail` only and must
reject a requested `PauseForReview`; it must not pretend to have persisted a pause.

## Current evidence and limits

All source paths below are relative to the repository root. Line anchors identify the
inspected October branch, and must be refreshed when implementation moves a symbol.

| Owner / source anchor | Observed behavior | Consequence for this design |
| --- | --- | --- |
| `src/MAF/Common/CanDoItAll.AgentFramework.Core/Context/AgentExecutionSourceAuthority.cs:10` and `:53` | Source-keyed providers derive authority from durable product configuration; observed UI scope is not a grant. | Freshness settings cannot become a new authority source. |
| `src/MAF/Common/CanDoItAll.AgentFramework.Models/Execution/AgentExecutionGovernanceSnapshot.cs:20` | Captures original profile/generation, scope, source lifetime, operation/capability ceilings and policy identity. | Keep original authority and current authorization checks even for pinned data. |
| `src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ContinuationGuards.cs:435` | Recovery checks original versus current authority, source lifetime, policy fingerprint, capability and external-target sets; background dispatch uses the journal owner. | Do not silently replace a checkpoint's grants with today's grants. |
| Same file, `:397` | Completed-result disclosure rechecks current read access. | Historical basis and receipts remain access-controlled after completion/revocation. |
| `src/MAF/Common/CanDoItAll.AgentFramework.Models/Execution/AgentToolAdmissionModels.cs:125` and `:131` | Typed recovery distinguishes `RevalidateAndRead`, `OwnerReceipt`, `ReconcileBeforeRetry`; payloads bind semantic version, digest and optional source preparation. Source preparation is limited to 65,536 UTF-8 bytes. | Extend the owner-specific prepared source; do not introduce parallel replay truth or put full dashboards in it. |
| `src/MAF/Common/CanDoItAll.AgentFramework.Models/Execution/AgentToolJournalModels.cs:105`, `:117`, `:152` | Approval binds batch, business intent, semantic version and digest; proposals retain dispatch/effect/result state in a revisioned journal. | Bind basis and policy into the existing semantic digest. Replanning creates a new proposal/intent. |
| `src/Modules/CanDoItAll.Modules.CrmHr/AgentTools/CrmPlanningAgentRuntimeToolProvider.cs:22`, `:71`, `:166`, `:201` | CRM planning reads bind original admitted interactive session, capability, workspace source and governance, recheck read authority and restrict redisclosure. | A useful integration example, but these are read tools: they neither reserve capacity nor provide a task-publication decision basis. |
| `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectAgentSourceMutationAuthority.cs:35` | Checks both captured and current Agent grants, exact project lifetimes, reservation identity and execution ceilings. | Preserve this source check; a content revision must supplement it. |
| `src/Modules/CanDoItAll.Modules.Projects/ProjectMutationAuthority.cs:46`, `:66`, `:80`, `:113` | Acquires source authority before starting the owner transaction; validates source before ordered owner locks and again before commit. | Add source-content validation within this established protection/order, not a disconnected preflight query. |
| `src/Processes/CanDoItAll.Processes.Persistence/EfProcessWorkflowDispatchAuthority.cs:22` | Enlists the actual PostgreSQL owner transaction, takes a root advisory lock and row share locks; checks exact assignment/plan/claim. At `:227`, active step attempt and nonexpired claim are required. | Reuse the existing fence. Cancellation tokens alone are insufficient. |
| `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectProcessExecutionMutationService.cs:68` and `ProjectWorkflowMutationService.cs:18` | Native mutations acquire source before the owner transaction, then guard source/targets and recheck at commit. | The integration already has a boundary for owner-validated publication. Do not hold it during model/provider calls. |
| `src/Processes/CanDoItAll.Processes.Application/ProcessExecutableDefinitionResolver.cs:9`, `:82`, `:110`, `:134` | Captures and validates immutable definition publications/dependencies and retains selected captured closures; closure is bounded at 256 definitions. | Keep definition pinning separate from refresh of live business facts. |
| `src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Definitions/WorkflowExecutionPolicyRules.cs:18` | External-writing executors cannot enable retry without supported idempotent behavior/capability. | A freshness replan must not bypass this existing retry restriction. |
| `src/Processes/CanDoItAll.Processes.Runtime/ProcessRecoveryClassifier.cs:21` | Recovery has bounded replay-scope review and repeated-diagnostic budgets, excludes policy/capability denials, and emits persistent recovery receipts. | Add freshness reasons to the existing recovery classification; count semantic replans across restart and nested runtime retries. |
| `src/MAF/Common/CanDoItAll.AgentFramework.Core/Context/AgentContextTransitionClassifier.cs:13` | Classifies source/view/selection transitions; it does not validate business-content revisions. | Following an application surface is not the data freshness policy proposed here. |

CodeAnalytics snapshot `snap-20261016142521-bf7089bf` loaded four scoped projects
(`AgentFramework.Core`, `Processes.Application`, `Processes.Contracts`,
`Processes.Runtime`) and 361 documents. It reported 187 diagnostics and blocking
workspace errors, including NuGet audit errors for cryptography packages. Positive
symbol/source evidence was usable; absence of edges or symbols is not dependency
closure proof. Direct project-file inspection confirms that Processes.Runtime does not
reference MAF, product modules or persistence, and AgentFramework.Core does not reference
product modules. Architecture acceptance uses these concrete source boundaries with the
diagnostic limitation recorded. A healthy evaluated graph of the actual changed
projects and their direct dependencies is required before a new reference is added and
again at that integration gate; a full-product snapshot is not a prerequisite to the
independent transaction-cleanup repair. None is claimed by this investigation.

## Settings and behavior matrix

Use validated enums and records, with explicit serialization versions and defaults.
These names describe proposed semantics; they are not existing public API promises.

| Setting | Values | Meaning |
| --- | --- | --- |
| `ReadBasisMode` | `RunSnapshot`, `StepSnapshot` | Capture the declared, bounded business inputs once per run or once per admitted logical step/turn. A transport retry, executor retry or exact-intent replay retains that same basis. A later logical step captures a new basis in `StepSnapshot`; never refresh partway through a model call. |
| `PublicationFreshness` | `PinnedObservation`, `RequireCurrent` | Permit publication explicitly derived from the captured observation where the owner supports that semantic, or require all owner-defined material inputs to remain current at publication. Both require current authority and conditional target state. |
| `OnBasisChange` | `Fail`, `PauseForReview`, `Replan` | Return a final no-effect rejection, preserve the proposal and wait for an explicit decision, or capture new inputs and generate a new proposal under a bounded budget. |
| `ReplanBudget` | Count, elapsed-work deadline and provider cost/token ceiling when available | Limits semantic rework independently of SQL transport retry and executor retry. The effective allowance is bounded by every enclosing run/step budget. |
| `MaximumBasisAge` | Positive bounded duration or an owner-approved immutable-input exemption | Admission freshness limit. Time alone is not a revision check, and immutable-definition retention is a separate concern. |

| Scenario | Recommended read mode | Required publication behavior | Change response |
| --- | --- | --- | --- |
| Analysis/report of an explicitly selected historical snapshot | `RunSnapshot` | `PinnedObservation`; output says which snapshot it describes. Current authority still applies to result disclosure and storage. | Finish from that snapshot; fail if permission/lifetime is lost. |
| Ordinary interactive agent planning for the current project | `StepSnapshot` (one proposing turn) | `RequireCurrent` for objective, applicable mandate and bounded relevant task membership; conditional target writes. | `PauseForReview` by default. Replan is an explicit setting. |
| Automated process step planning current work | `StepSnapshot` | `RequireCurrent`, plus current Process dispatch claim and attempt. | Default pause/manager action; optional bounded `Replan` only for a proven replay-safe planning step. |
| Long pipeline where later steps intentionally use new input | `StepSnapshot` at each newly admitted logical step | Every proposal records its own coherent basis; `RequireCurrent` for mutable decisions. | Replan/pause according to admitted node policy. Earlier completed steps remain recorded effects. |
| Reproducible process with immutable published definition and pinned documents | `RunSnapshot` for selected business inputs; definition closure always remains pinned | Owner-specific publication contract. Current project changes still reject strict publication even though computation may finish. | Preserve result; explicit re-admission/replanning before current-state publication. |
| Workflow node replacing an existing record | Read original target and required source basis at node admission | Expected original target state plus owner minimum source freshness. | Conflict is a typed no-effect outcome, not a transient executor retry. |
| External action that may already have happened | Preserve the exact dispatched proposal | Provider conditional/idempotent contract and receipt reconciliation. | Unknown outcome goes to reconciliation; never replan or blindly replay. |

`PinnedObservation` is not a force-write setting. For example, an agent may finish an
analysis of yesterday's objective, but an owner operation named “publish the current
project plan” must reject a configuration attempting to weaken its mandatory
`RequireCurrent` requirement. A distinct explicit historical-report operation may
accept the pinned basis. The owner advertises supported modes so the editor can reject
invalid combinations before launch, and the owner repeats validation at execution.

The initial contract has no separate `RefreshBeforeProposal` enum value. Refreshing
before the next logical step is already `StepSnapshot`; refreshing a rejected proposal
is an explicit semantic replan with a new basis, intent and approval. A future distinct
mode needs a tested server-owned proposal admission boundary before becoming a setting.
The concrete P04/P07 cards are in
`codex/bundles/concurrent-mutations/plan/P04-P07-runtime-policy.md`. They distinguish
the Agent filesystem approval checkpoint, durable Workflow backend checkpoint and
Process runtime recovery state; none can be substituted by metadata-only checkpoints.

## Policy resolution and configuration ownership

Resolve policy once at admission, persist the effective value and policy version, and
include them in the proposal identity. Resolve operator preferences in this order:
workspace default, Agent definition or Process/Workflow definition default, explicit
step/node override, then authorized invocation override. This is preference precedence,
not permission precedence: every selection must satisfy the operation's allowed mode
set, owner minimum freshness, current authority ceiling and enclosing execution budget.
Reject unsupported combinations rather than quietly changing the requested behavior.

All API/editor/model-supplied preferences are untrusted requests. The server resolves
the operation's mandate and supported policy independently from durable owner data;
a request cannot supply its own mandate, effective policy, approval scope, required-fact
list or “owner supports pinned” flag. Persist the server-resolved result, then validate
it again at publication. Missing policy on a new admission resolves through the
documented conservative default; missing trusted basis on a protected mutation rejects.
These are distinct cases and must have distinct compatibility handling.

Operator defaults apply to future admissions. An existing run retains its admitted
policy and immutable definition; edits do not silently rewrite saved checkpoints.
Current safety/authority restrictions apply immediately at the actual owner boundary.
If a policy change makes the captured contract inadmissible, pause/reject with a typed
reason and require explicit re-admission. Do not use “settings changed” to widen an old
approval or reset a spent replan budget.

Agent editing belongs in the Agent owner and its existing configuration normalization,
catalog concurrency and editor contract. Process definition/step values belong in the
provider-neutral Process model and compiled assignment, with authoring validation and
publication hashing. Workflow definition/node values belong in Workflow models and
definition validation alongside executor policies. Product launch adapters translate
these into the exact admitted planning contract. Never encode behavior in arbitrary
prompt instructions, free-form launch variables, or model-produced JSON strings.

A settings UI must show “keep starting data”, “read again for each step/proposal”, and
what happens if publication finds a change. It must also show mandatory owner rules and
the allowed replan budget. Existing Process/Workflow immutable version selection and
data freshness are separate controls. Renderer contracts carry data and commands only;
they must not reference owner DbContexts, Agent execution machinery or persistence.

## Bounded decision basis and lineage

Start with one named operation: prepare and publish a bounded project task plan from a
project objective. Workbench owns its required semantic inputs. Projects owns the
coherent project definition snapshot/content revision and exact project lifetime.
Workbench owns canonical task facts, target conditions and a membership witness for the
relevant task set. The mandatory objective and task-membership dependencies are inserted
by trusted code, not selected by the model. Additional model-requested reads can be
attached if they are actually supplied to that proposing call and admitted by the owner.

The basis records profile/dataset identity, project lifetime, scope, typed fact kinds,
owner-issued revisions or immutable content identities, set witness, captured time,
policy version and proposing invocation identity. It also records completeness: a
truncated query cannot claim to describe the whole task set. A predicate witness must
cover additions/removals and any updates changing predicate membership; individual row
versions alone do not detect a new matching task. Prefer one owner collection revision
first; narrow only after measured overinvalidation proves it necessary.

For the first operation, define the material collection as the selected project's
canonical planning-task set, not an arbitrary model-written query. Its completeness and
maximum supported size are explicit. Tasks in other projects, activity and logs cannot
invalidate this witness. If a future operation intentionally depends on only a subset,
its contract must either accept documented conservative invalidation by that broader
collection or supply a narrower maintained witness; it cannot claim that unrelated
changes never invalidate while checking a global task revision.

Store the bounded canonical basis once and refer to its server-issued identity/digest
from proposal preparation. Do not ask the model to copy tokens or approve a newly read
token on behalf of the original proposal. Do not attach every read from a long chat.
If a referenced fact is deleted/recreated, its object/lifetime identity must prevent an
equal-looking replacement from satisfying the old basis. A source unavailable during
validation is not “unchanged”. Remote observations carry their weaker pinned/conditional
provider contract; local SQL cannot promise they stayed current at SQL commit.

## Publication and approval protocol

1. Capture authorized, coherent, bounded source facts and original target conditions.
2. Produce the proposed effect outside a SQL transaction. Retain exact add/update/remove
   intentions, basis identity, admitted policy and original authority/claim identity.
3. Canonicalize and bind effect, basis, target conditions, policy and authority scope
   into the existing versioned semantic digest. If approval is needed, display the
   actual proposed changes and material source observations before approval.
4. Acquire the existing original-source authority lease before starting the owner
   transaction. Preserve existing source/claim-before-owner lock classes; sort complete
   keys within their class. Do not replace that order with one alphabetical global list.
5. Within the same physical owner transaction, revalidate current authority and claim,
   protect/validate all material `RequireCurrent` facts and membership witnesses through
   typed owner ports, then enforce target expected-state conditions. Every conflicting
   source writer must participate in the same protection protocol.
6. Apply the bounded atomic batch and owner receipt in that transaction. Recheck required
   temporal claim/admission conditions at commit using existing owner guards. No LLM,
   HTTP/MCP, filesystem delivery, provider call or UI interaction occurs inside it.
7. Preserve known commit provenance before cleanup/readback/event delivery. Failure after
   a confirmed commit cannot be reported as a safe rejection or trigger re-execution.
   Lost acknowledgement requires the same owner operation identity and receipt lookup.
8. If a material basis changed, commit no partial batch. Return changed owner/fact
   categories without private content; follow the persisted failure/pause/replan policy.

Replanning means new read, new model/computation invocation, new proposal/business-intent
identity and new approval where required. It does not mean replacing expected versions
on the old payload and trying it again. An unchanged approved effect still requires new
approval after a material basis change by default. Any future approval-equivalence
optimization needs an explicit owner proof and understandable approval scope; it is
outside the initial pilot.

The existing tool journal remains authoritative about dispatch and tool approval.
Owner receipts remain authoritative about committed domain effects. A planning-basis
record does not become either of those. Result redisclosure continues to require current
read access even when computation, approval and the owner write completed earlier.

## Attempts, invalidation and human control

Change notifications are a latency/cost optimization. They can mark an in-flight basis
stale and request cooperative cancellation at a safe boundary. Delivery can be missed,
duplicated or reordered; final owner publication checks are still mandatory. Keep only
bounded subscriptions for active bases, remove them on completion/cancellation, and
batch/coalesce hints by owner scope. Avoid per-fact polling and full-workspace refresh.

Do not mix new source facts into an already running model call. A replan starts a new
attempt after retiring the old one. Reuse Process claim token/attempt and existing Agent
dispatch lease identities as fences; do not create an unrelated “generation” counter.
Publication by a late worker must fail at the owner even if cancellation is ignored.
When two contexts share one physical enlisted connection, await participants serially;
`Task.WhenAll` on that connection is not a query optimization.

Explicit domain operations distinguish: edit while automation remains active; pin an
automation-owned value; pause a specified rule/run; and take over a specified task or
run. Each has an expected policy/ownership state and scope. A policy/override revision
is a material basis input when it controls publication. A human edit is not a global
pause, and pausing one planner must not stop unrelated chats. Initial behavior is
conflict-and-pause, not automatically overwriting either the human change or the proposal.

Automatic retry classes must stay separate:

| Condition | Permitted action |
| --- | --- |
| Known rolled-back serialization/deadlock failure, original intent unchanged | Bounded fresh-transaction retry, repeating authority/basis/target validation with the original identities. |
| Semantic source/target conflict | Fail/pause or a bounded new proposal; never classified as transient SQL retry. |
| Current permission revoked, old claim retired, lifetime/profile mismatch | Stop/reject; no automatic permission reacquisition or identity substitution. |
| Commit/effect outcome unknown | Reconcile the original operation; do not allocate a new operation to escape uncertainty. |
| External executor failure | Existing driver retry/idempotency policy remains mandatory; whole-step replay requires all prior effects to be safe. |

## Durability, compatibility and bounds

Persist effective policy, basis reference/digest, required owner schema versions, exact
proposal lineage, authority/claim binding, consumed replan count, elapsed-work deadline
and last structured invalidation with the durable admission/checkpoint. The Process
compiled definition and Workflow version remain immutable; business-fact refresh creates
a new basis, not a rewritten executable history. Recovery can inspect old snapshots
without being permitted to publish their effects.

Legacy checkpoints missing a newly mandatory trusted basis remain readable history.
Undispatched legacy work requires explicit re-admission/replanning before a protected
mutation; dispatched/unknown work requires existing receipt reconciliation first. Do not
backfill provenance from today's rows, reinterpret a recorded denial as approval, or
silently treat absent policy as snapshot permission. A schema version must distinguish
new admissions from intentional legacy read/reconciliation paths. Mixed-version rollout
must drain/refuse old writers for the protected operation.

Proposed pilot limits below are conservative starting constraints, subject to the
performance acceptance review. They are recommendations, not measured capacities:

These candidate limits apply only to the new explicitly bounded planning operation.
They do not cap existing Projects editors, truncate existing tasks, or change the
current Process/Workflow limits. Before enabling the operation, preflight retained data
and measure the representative supported planning set. A project exceeding the selected
limit receives a clear unsupported-size result without mutation; a smaller supported
operation or an explicit staged plan is required. Until then, the numbers below are not
production defaults. This follows the existing-limit rule in
[the performance plan](04-performance-and-risk-plan.md#bounds-and-retention-decisions).

| Dimension | Initial proposal and enforcement |
| --- | --- |
| Atomic planning batch | At most 100 intentions against one project lifetime. Reject oversized batches before transaction; a later staged protocol must expose partial completion explicitly. |
| Material dependencies | At most 64 distinct dependency entries plus one task-set membership witness. Reject incomplete dependency capture rather than truncate. |
| Prepared source | Respect the existing total 65,536-byte UTF-8 envelope limit; budget at most 32 KiB for inline basis metadata and use an owner-stored reference for retained content. Check the final complete envelope, not just each child. |
| Replans | Default zero automatic replans; explicit opt-in up to two per admitted planning operation, with the enclosing Process/Workflow limit able to be stricter. Count persists across crashes and nested retries. |
| Planning work | Default ten-minute aggregate work deadline for the pilot, capped by the enclosing run deadline. Provider cost/token ceiling is enforced where accounting is available; do not invent a cost amount without configured pricing. |
| Transient SQL retries | At most two retries after positively known rollback, within the existing operation deadline. Reuse original operation/proposal; no provider call in this retry loop. |
| Live basis age | Default 15 minutes for current-project planning. Age expiry returns a typed stale-basis outcome; frozen historical inputs use an explicitly supported exemption. |
| Retention | Pin basis metadata/required content while any live admission, pending approval, unknown effect or recoverable checkpoint references it. Cleanup terminal content only under the existing execution/privacy retention policy and a documented supported replay horizon. Retain compact operation identity/digest/outcome for the entire supported deduplication horizon. |

Measure aggregate active planning work across attempts; a human approval/review wait
does not consume that active-work counter. Absolute enclosing run deadlines and basis
age continue advancing during the wait. A review action after basis expiry cannot simply
resume publication: it requires a new admitted basis/proposal. Process claim expiration
also remains effective; resumption goes through the Process owner to acquire legitimate
new execution eligibility, never by editing an old saved claim.

The exact terminal retention duration is an open operational decision: a universal number
would be invented without inspecting all current cleanup/recovery policies. Closure
requires a configured finite retention/replay contract and cleanup tests. Expiry of full
content may make replanning necessary, but never makes a previously used operation ID
safe to execute as new. A missing retained record must produce an explicit expired or
reconciliation-required result. Destructive dataset restore also needs the coordinated
dataset-incarnation fence defined by the foundation design; a process-local profile
generation must not be assumed to provide that guarantee without proof.

## Dependency and responsibility constraints

| Responsibility | Owner after change | Forbidden dependency / coupling |
| --- | --- | --- |
| Owner-independent option vocabulary, if shared by all three consumers | A small data-only contract in the existing neutral foundation layer after the concrete contract is agreed | No Projects, Workbench, EF, MAF SDK or UI references; no generic dependency graph engine. |
| Agent policy normalization/admission and durable tool identity | Agent Models/Core; storage implementation stays in its existing persistence owner | Core must not query product DbContexts or acquire Workbench services with service location. |
| Process definition/assignment policy and recovery reason | Processes Core/Builder/Runtime/Application as appropriate; product adapters under Modules.Processes/Workbench | No Process core reference to AgentFramework, Modules.Projects or infrastructure implementations. |
| Workflow node policy validation | Existing Workflow models/Definitions; execution scheduling in Workflow runtime | No Projects task semantics or EF persistence in the workflow definition model. |
| Required planning facts, basis/plan canonicalization, membership witness, publication | Independent cohesive services at the Workbench owner; data-only transport DTOs in its existing Planning.Contracts where appropriate | No additional partial on `AgentFrameworkWorkspaceExecutionService`, no CRM/Projects read via reflected strings, no generic owner registry controlling domain behavior. |
| Project source revision/currentness check | Typed Projects-owned read/publication port implemented by Projects | Workbench must not read/write Projects tables directly; Projects must not reference Workbench implementation. |
| Current Process/Workflow authority adapters | Existing product integration ports and implementations | Do not duplicate claim generations or approval records. |
| Presentation | Owning editor/host with existing renderer seam contracts | Renderer cannot perform policy resolution, tool admission, database reads or refresh side effects. |

The first basis implementation can remain a small group of independently testable types
inside the existing Workbench module. A new project is justified only when the actual
reference graph or independent implementation lifecycle requires it; moving three types
into a new assembly alone does not improve the design. Conversely, placing all policy
resolution, snapshot storage, publication and runtime retries in one large “concurrency
manager” is rejected. Pure policy/canonicalization tests must not construct that runtime.

## Minimal integration sequence and granular gates

Architecture acceptance approves the behavior and responsibility split below. It does
not pre-approve unsupported storage/replay guarantees, arbitrary numerical limits or an
incomplete writer perimeter. Each step has its own entry/exit evidence. The independent
Projects/transaction cleanup vertical can proceed before this later planning lane.
New policy controls must not be exposed as effective before their runtime consumer is
wired and proved; a persisted dropdown with no enforcing caller is not implementation.

| Gate | Minimal concrete integration | Exit proof before enabling the next behavior |
| --- | --- | --- |
| AP-0: owner contract | Identify the single planning command, all task publishers, all objective/task-set writers, source/claim/owner lock order and exact supported collection. Use Projects' definition snapshot/publication port and Workbench canonical task owner; no SQL through another owner's context. | Deterministic source-change/membership tests against real PostgreSQL; current target/authority fences retained; bounded batch and receipt atomicity established. |
| AP-1: neutral vocabulary | Add immutable validated policy options and typed change reasons to an existing neutral contract dependency shared by Agent/Process/Workflow, with a pure resolution function in a neutral core owner. Only data/options and policy composition belong here; no owner names, DbContexts or provider calls. Freeze defaults `StepSnapshot/RequireCurrent/PauseForReview/0 replans` for review-capable governed planning. | Affected evaluated dependency graph; serialization and policy-resolution tests, including untrusted override rejection. No new edge from Processes to AgentFramework or a product module. |
| AP-2: owner settings and editors | Agent: `AgentEditorModel` in `Models/Editors/EditorModels.cs`, `AgentDefinitionFactory`, existing `SaveAgentAsync` original-version path, typed configuration metadata codec, `AgentEditorDraftSnapshot`, `AgentEditorDraftPolicy`, `AgentEditorCommands` and the extracted Agent editor renderer/session. Workspace defaults: `WorkspaceSettingsModel`, its copy function, `Modules.Workspace/Models/WorkspaceModels.cs`, `WorkspaceDefaultsOwner`, `WorkspaceSettingsApi` and `WorkspaceSettingsSurface`; add an original-settings revision for changes to this new policy section rather than assuming the current whole-settings save protects concurrent edits. | Save/read/import round trip and unknown-enum rejection; original-version conflict; copy/dirty preservation; caller cannot set server effective mandate; two-editor host proof. Policy normalization must run in the server owner even when UI is bypassed. |
| AP-3: Process persistence | Add typed default/step override to the Process template/authoring model, validate through `ProcessAuthoringCodec` and authoring compilation, include it in immutable publication content/hash, carry resolved value through prepared launch and `ProcessRuntimeStepAssignment`. Extend `ProcessPreparedLaunchCodec` and `EfProcessRuntimeStepAssignmentStore` with explicit schema compatibility. Wire projection/editor settings through `ProcessDefinitionStepEditorProjectionService` and its renderer contract. | Published definition/child closure remains immutable; restart preserves resolved policy and budget; legacy missing basis is history/re-admission only. Codec schema changes include old-history tests rather than silently changing schema 1 meaning. |
| AP-4: Workflow persistence | Add typed definition/node policy alongside existing Workflow models and execution policy; validate in Workflow definition validation and `WorkflowExecutionPolicyRules`; serialize into immutable version and run checkpoint/assignment admission. Wire `WorkflowDocumentOwner`/canvas editor and existing settings renderer contract. Keep `MaxRetryAttempts` for execution failures distinct from semantic `ReplanBudget`. | Saved node policy survives version selection, restart and checkpoint recovery; unsafe external retry stays denied; current definitions do not overwrite a retained run's policy. |
| AP-5: planning owner/tool | Create small top-level Workbench planning basis capture, canonicalization/publication and storage services, data-only Planning.Contracts as needed, and a dedicated `IAgentRuntimeToolProvider` registered by `WorkbenchModuleServiceCollectionExtensions`. Existing `AgentTools/ProjectStructureAgentRuntimeToolProvider.cs` and CRM planning provider demonstrate the tool seam; do not add another responsibility to their broad implementations. Use `AgentToolPreparedPayload.SourcePreparation` for the authenticated bounded reference, existing journal intent/approval digest for dispatch identity, and the owner receipt for effect truth. Add canonical composition EF mapping/migration if a new retained basis store is required. | Actual tool/API round trip, not direct unit calls only; trusted mandatory facts cannot be omitted; substituted reference/digest rejected; receipt reconciliation returns original outcome after lost acknowledgement. Operation remains disabled until full source/target writer perimeter passes AP-0. |
| AP-6: runtime response | Introduce an independently testable neutral change-response coordinator called by the existing admission/continuation boundary. Persist pause reason, proposal lineage and consumed budget in durable execution state. Process adapters use the existing recovery classifier/manager action and exact claim guards; Workflow adapters use retained node/run identity. Replan generates a new provider invocation/proposal after positively known no effect. No new execution-service partial or second journal. | Pause releases every held resource; resume obtains legitimate owner eligibility; stale attempt stays fenced; unknown effects reconcile before any new provider call; budgets aggregate across restart and nested runtime retry. |
| AP-7: operator flow/closure | Expose supported settings and policy provenance, structured basis conflict, explicit replan/review/takeover, current-action eligibility and retained result. Finish retention cleanup, dataset lifecycle, mixed-version refusal and performance proof. | Real host/browser + PostgreSQL + two-process races; required exact discovered filters; final portability enforcement; broader frozen stable/platform checkpoint when the actual shared-contract/migration changes trigger it. |

The AP-2 Workspace revision is scoped to the policy settings owner; it is not a new
project-content revision. If Workspace settings concurrency is not ready, launch uses a
documented immutable built-in default and explicit owner-supported Agent/definition
values, while workspace editing of this policy remains unavailable. That is an explicit
capability boundary, not silently falling back after an invalid or failed settings read.
The full requested settings experience is not complete until AP-2 is closed.

The default replan budget is zero, so AP-6 can first deliver a complete durable
pause/review path. Automatic replanning is then enabled only after its separate budget,
approval, effect-reconciliation and claim tests pass. An explicit semantic replan requires
invalidation of every downstream derived artifact that consumes the replaced basis;
completed effects stay recorded, and independent branches may retain their own bases.

Proposed deterministic test names below define new implementation targets. They are
not existing or discovered tests and carry no passing-count claim:

| Proposed test class/method | Controlled scenario |
| --- | --- |
| `DecisionBasisPolicyTests.New_governed_planning_defaults_to_current_publication_and_durable_review` | Omitted preference resolves conservative default while mandatory basis remains required. |
| `DecisionBasisPolicyTests.Client_pinned_preference_cannot_weaken_current_owner_mandate` | Tampered requested policy/mandate cannot change server-resolved strict publication. |
| `DecisionBasisPolicyTests.Stateless_owner_rejects_pause_when_no_durable_review_contract_exists` | No false claim of a persisted pause. |
| `DecisionBasisPolicyTests.Nested_retry_and_replan_budgets_cannot_reset_consumed_work` | A child executor retry and restart consume one durable enclosing allowance. |
| `AgentDecisionPolicySettingsTests.Concurrent_editor_save_preserves_original_policy_revision` | Two independently captured settings edits conflict instead of replacing current settings. |
| `ProcessDecisionBasisPersistenceTests.Published_definition_and_effective_policy_survive_restart_without_recapture` | Change definition/defaults after launch; retained run stays pinned. |
| `WorkflowDecisionBasisPersistenceTests.Legacy_checkpoint_cannot_publish_without_trusted_basis` | Readable old history cannot acquire today's source tokens. |
| `ProjectPlanningPublicationPersistenceTests.Objective_change_after_capture_rejects_entire_batch` | Barrier after source capture, second connection updates objective, first publishes with no tasks/receipt committed. |
| `ProjectPlanningPublicationPersistenceTests.Task_membership_change_during_planning_rejects_complete_set` | Insert/remove matching task behind a barrier; per-row versions would miss the change. |
| `ProjectPlanningPublicationPersistenceTests.Task_change_in_another_project_does_not_invalidate_basis` | Positive independence control for owner-scoped collection witness. |
| `ProjectPlanningToolAdmissionTests.New_basis_requires_new_intent_and_approval_digest` | Replacing source token on an old approved proposal fails; properly admitted new proposal succeeds. |
| `ProjectPlanningRuntimeTests.Pause_releases_resources_and_preserves_proposal_without_provider_retry` | Rejected owner result leaves durable review state and zero additional provider calls. |
| `ProjectPlanningRuntimeTests.Unknown_effect_is_reconciled_before_replanning` | Lost commit acknowledgement prevents new identity/provider invocation until receipt resolution. |
| `ProjectPlanningPublicationPersistenceTests.Retired_process_claim_cannot_publish_after_takeover` | Second connection retires claim before publication and after flush; both paths preserve no partial effect. |
| `ProjectPlanningPublicationTwoHostTests.Source_change_is_rejected_without_shared_process_locks` | Independent host processes prove publication correctness does not depend on local gates. |
| `DecisionBasisRetentionTests.Live_checkpoint_reference_prevents_cleanup_and_expiry_cannot_revive_intent` | Cleanup races paused/recoverable work and terminal detail expiry. |
| `DecisionPolicyBrowserTests.Agent_and_process_settings_round_trip_and_stale_plan_requires_review` | Native settings save, two actors, persisted policy, preserved proposal, explicit review/replan. |

Use barriers and injectable clocks for ordering/age cases, plus real PostgreSQL owner
transactions for publication tests. A fake clock or InMemory context does not prove
distributed database exclusion. Production test hooks must remain narrowly scoped; do
not introduce globally enabled fault switches or timing sleeps to manufacture races.

## Inherited Agent execution

An inherited background planning occurrence uses journal schema 7 and decision schema 2.
It retains explicit parent provenance instead of constructing Workspace/Agent preference
provenance. The original material identity, digest and capture time stay fixed; its local
RunSnapshot covers all provider iterations, even for a parent StepSnapshot. The original
finite child allocation supplies work, with zero local replans. A child cannot refresh
parent material. A successor requires a new parent-issued occurrence after reconciliation.

Current background source authority is checked before new work and provider dispatch.
Revocation does not prevent recording a response already received or settling consumed
work. Interrupted or uncertain work remains reserved. The MAF adapter selects the original
background instruction explicitly and uses the retained basis without creating a chat.
The interactive schema and its approval/source rules remain separate.

Initial inherited admission now resolves the exact child execution ID under its existing
per-run lease before preparing input or a grant. Interrupted initial publication recovers
through the file owner's existing commit journal. The saved inherited runtime contract
uses explicit metadata v3; interactive metadata v2 is unchanged. Current source read
access and the business owner's original-parent check are mandatory on recovery. A
retained record grants no new dispatch or work interval. See the
[admission evidence](../../../codex/bundles/concurrent-mutations/validation/2026-10-19-exact-inherited-admission.md).

Inherited saved effects have a separate parent-authorized Core entry. Its owner-issued
lease retains the original parent source, material, policy and grant. Caller, parent and
registered background dispatch authority are checked again after lease waits and before
claiming an effect; generic and interactive saved entries continue to reject inherited
journals. Publication still requires the real claim, selected original snapshot, live work
and basis limits, plus the effect owner's enlisted business-source fence. Actual committed
or unknown outcomes can be recorded after authority is revoked, without granting disclosure
or another dispatch. Manager review, inherited cancellation and historical receipt access
are separate paths and are not enabled by this entry.

The runtime request has a named inherited factory; constructing it grants no dispatch
authority and the interactive constructor still rejects background sessions.
MAF restores this admitted effect with its original context scope and source, without
interactive governance. The registered provider must support governed Process automation;
the usual current Agent permissions, Process policy contributor, exact restoration,
receipt preflight and invocation authorization remain required. Core owns metadata parsing;
Tooling receives an explicit immutable inherited context without a dependency on Core or
product modules. The Workbench tool remains interactive-only until its inherited material
and transaction authority adapter and actual producer are integrated.

This journal and provider path is implemented and tested. Actual Process/Workflow
producers, inherited Workbench effects and current manager continuation are still gated;
the protocol alone does not enable their settings. See the
[implementation evidence](../../../codex/bundles/concurrent-mutations/validation/2026-10-19-inherited-agent-planning.md).

## Validation plan

These are required cases to map to exact tests during implementation. No counts or
passing results are inferred from historical test names.

| Layer | Required proof |
| --- | --- |
| Pure policies/codecs | Resolution precedence; incompatible modes rejected; enums/schema versions validated; stricter owner rules cannot be relaxed; exact canonical digest changes with effect/basis/authority/policy; order-independent canonical sets; size/batch bounds; immutable snapshot copying. |
| Runtime unit | Run/step/proposal capture timing; notification does not mutate active input; unchanged snapshot computation can finish; current publication rejects stale source; a new replan gets new identity; budgets survive restart; no reset through nested executor retries. |
| Approval/recovery | Original digest approved succeeds; substituted basis/target/policy fails; material change needs reapproval; saved denial stays denied; legacy missing basis cannot be synthesized; unknown commit reconciles before any replan. |
| Real PostgreSQL | Objective changes after capture; membership phantom insertion/removal; target row changed; source lifetime replaced; grant/claim revoked before write and after flush; bounded batch rollback has no partial tasks or receipt; duplicate same intent has one effect and receipt; same ID/different digest rejected. |
| Two processes | One process holds an admitted basis while another changes objective/claim; final publication remains protected without in-process semaphores. Include a positive fresh-publication control. |
| Actual Agent tool/API | Runtime, not model arguments, supplies mandatory basis; dropped/substituted basis fails; exact original prepared source survives approval and restart; CRM read access is not mistaken for reservation/publication authority. |
| Process/Workflow | Pinned executable definitions survive new publications; new business input creates a distinct basis; old attempts cannot publish after reassignment/takeover; mapped workflow claim expiry still rolls back admission; no whole-step replay after non-idempotent external effects. |
| UI/browser | Settings round trip through owning API; impossible settings rejected; two actors show preserved proposal/draft and structured change reason; pause/replan/takeover scope visible; no silent reload of user edits or implicit approval. |
| Performance/faults | Compare matched no-contention and same-project/different-project workloads; record SQL round trips, lock wait/hold, transaction time, basis bytes/allocations, rejection rate, replan provider cost and starvation. Cancellation/disposal failures release leases and preserve committed outcomes. |
| Retention/lifecycle | Restart preserves budgets/basis; cleanup cannot delete live references; expired terminal basis cannot revive an operation; restore/import cannot adopt another dataset's tokens; read revocation prevents historical result disclosure. |

Current regression discovery seeds include `CrmPlanningToolAdmissionTests`,
`CrmPlanningRuntimeIntegrationTests`, `ProcessInvocationSnapshotTests`,
`ProcessExecutionAuthorityPersistenceTests`, `ProcessCatalogAuthorityPersistenceTests`,
`AgentToolAdmissionJournalIntegrationTests`, `MafGenericToolAdmissionIntegrationTests`,
`MafPolicyDenialCheckpointTests` and `WorkflowStructureDeliveryPersistenceTests`.
Useful specific source cases already present include
`ProcessCatalogAuthorityPersistenceTests.Mapped_workflow_claim_expiry_before_or_after_owner_flush_rolls_back_run_and_started_event`,
`Mapped_workflow_stale_claim_and_changed_repair_content_cannot_admit_a_child`, and
`ProcessExecutionAuthorityPersistenceTests.Legacy_missing_admission_or_claim_requires_reconciliation_without_rewriting_history`.
Their presence is not execution proof. Use repository build-backed exact filter
discovery, isolated PostgreSQL 18, appropriate browser/two-process lanes and the mandatory
portability-static gate for protected edits. Broader stable/platform gates follow the
repository's named foundation/release invalidation triggers.

## Remaining evidence before implementation of this lane

1. Freeze the single Workbench planning operation and enumerate every enabled publisher
   and every source/task-membership writer. The existing CRM read tool is not that pilot.
2. Prove the Projects/Workbench source revision and collection witness are updated by
   every relevant child/import/bulk writer, with actual publication protection.
3. Validate lock-class order against Process, Workflow, Agent catalog, owner and storage
   paths. Existing source-first checks do not by themselves prove the full new order.
4. Inspect existing persistence cleanup/replay horizons and dataset restore generation;
   settle retention/expiry and migration contracts before promising resumability.
5. Measure proposed bounds and critical-section costs on the isolated PostgreSQL fixture;
   no unmeasured p95/throughput claim is accepted.
6. Obtain a healthy evaluated dependency snapshot and run boundary tests after the exact
   projects/types are selected. The diagnostic-heavy snapshot above is not closure.

Design freeze can accept these responsibilities and policy semantics before execution.
The publication lane cannot claim completion until its complete writer perimeter,
durability, current-authority and performance evidence exists.
