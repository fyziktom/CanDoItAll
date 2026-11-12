# P23: admit an explicitly selected Process planning policy

Status: qualified, 2026-10-23. [P23 validation](../validation/2026-10-23-process-policy-producer.md)
passes 251 distinct backend cases across verified test-source repairs and 62 SPA tests,
with source/output, architecture, full portability and resource gates. This is a public producer slice, not
whole-engine policy activation. Agent definition, Workflow, shared refresh/replan and
multi-activity lifecycle remain separate acceptance gates.

## User outcome and current gap

The user asked for deliberate behavior when business facts change, including retaining
starting facts or taking a fresh basis for subsequent work. Workspace already persists a
versioned policy and Process assignments/root admissions retain typed policy provenance.
However the public Process launch producer creates none of those admissions. Existing
planning tests construct the initial admitted state themselves. P21 connects dispatch;
it does not prove an operator can prepare that state through the normal launch flow.

Ordinary Agent steps return `ProcessStepOutcomeResult`; the Workbench task planner returns
a reviewed task proposal. A default policy must never silently switch those business
contracts. Select the exact execution contract explicitly on a new launch intent. No
selection retains ordinary launch behavior. Unknown contracts are an explicit refusal.

The initial selectable owner is the existing Workbench project-task planner, limited to
one executable activity, an Agent executor, a current Project lifetime and task-write
authority. No artifact, tool, host-capability, branch, subprocess or additional executable
obligation may be discarded. Multi-step roots are rejected before material/provider work
until failed-root and mixed-contract finalization are qualified.

## A discovered change-response constraint

`AgentPlanningInheritedOrigin.ResolvePolicy` deliberately makes an inherited child keep
its granted basis and pause on change. It never spends a parent's semantic replan budget.
P21 maps that child to manager review; it does not implement the parent's Fail or Replan
response. Advertising those responses now would silently change requested semantics.

Therefore this first producer supports RunSnapshot and StepSnapshot with RequireCurrent
publication and PauseForReview only. Replan, Fail and PinnedObservation requests must
fail explicitly before preparation is saved. Read mode changes when material is captured,
not the business operation or publication authority. A single-activity run cannot prove
refresh between multiple activities; subsequent lifecycle proof must establish that.
The later parent change-response owner must precede exposing further choices.

## Minimum ownership changes

Application owns an optional typed launch selection containing the existing immutable
execution contract and an optional `DecisionDataPolicyOverride`. It does not own project
task semantics, Workspace persistence, Agent SDKs or catalog access. Null selection is
omitted from serialization and leaves legacy fingerprints unchanged.

Add one narrow Application admission-provider boundary selected by exact contract. Its
result contains only the root admission and per-step policy admissions; it cannot replace
the plan, runtime mutation, source authority, prompt, assignments or launch identity.
An Application coordinator checks exact assignment coverage, definition/step bindings,
root identity and parent budget provenance before attaching the returned admissions.
Duplicate provider registrations fail composition. Missing providers fail explicitly.
Do not extend `IProcessDecisionStepExecutor` with launch/configuration responsibility.

Workbench implements that boundary. Its existing references already permit reading
`WorkspaceSettingsWriter`, the Agent catalog and Project authority. Capture Workspace V2
once through its owner; require a saved policy and real head revision. Preserve its
namespace, revision and policy in both root and child admissions. Resolve typed requested
preferences through the existing pure resolver with the actual supported owner ceiling.
An inherited child retains its parent's policy; it never applies the Agent's standalone
definition policy as a fresh default or grants itself new parent work.

The producer initially accepts authenticated/local operator roots only. Agent-tool and
subprocess callers cannot obtain fresh root budgets by choosing this option. Their future
producer must inherit original funding and source identity. Require the selected Agent's
current task permissions and the original source's CanCreateTasks permission; the normal
admission and publication owners still recheck current authority after preparation.

No new project reference, Core-to-Runtime dependency, partial class, service locator or
schema migration is expected. Keep the new business code outside the existing large
launch service. That service calls the small coordinator at its existing durable
preparation boundary after plan/assignments exist and before `store.PrepareAsync`.

The concrete responsibilities are `ProcessLaunchDecisionOptions` (versioned requested
semantics), `ProcessDecisionLaunchAdmissionCoordinator` (exact provider selection and
immutable result binding), `IProcessDecisionLaunchPolicyProvider` (the product-owner
boundary), and Workbench's `ProjectTaskPlanProcessLaunchPolicyProvider` (supported
operation, coherent settings read and current source/executor eligibility). The provider
result contains typed admissions only. Unit tests construct the coordinator directly;
full launch-service construction is reserved for its actual producer integration proof.
This strategy boundary is justified by product ownership. Putting Workspace/Agent reads
into the launch partial or adding a Workbench reference to Application is rejected.

## Original intent, replay and compatibility

1. Validate the selection before legacy/preview routing. An explicit selection requires
   the durable preparation path, a trusted operator source and a caller intent. It must
   never fall through to the legacy path because a dependency is absent.
2. Bind the selection and exact override into the producer input and launch fingerprint
   using a conditional new hash domain. Preserve byte-for-byte legacy hashes when absent.
3. Look up original preparation before reading current Workspace defaults or Agent policy.
   A retry restores the saved selection, root funding, scope, policy and original input.
   Changed selection/override with the same intent conflicts. A new intent may capture new
   settings. Refreshing an authentication expiry does not create a new policy generation.
4. For a fresh preparation, allocate original root budget/scope identities once and build
   one step admission with the root budget as its enclosing ceiling. RunSnapshot receives
   the shared content scope; StepSnapshot has no shared scope. Existing codecs validate
   the exact policy/contract/provenance. Preparing does not allocate provider work.
5. Persist selection, admissions and original plan together. A concurrent same-intent
   preparation returns the original winner; no losing identity may appear in accepted
   runtime rows. Existing acceptance then creates the original budgets/scope atomically.
6. Add a preparation hash domain for the new launch selection, following existing policy
   and shared-scope domains. Old readers must reject it instead of ignoring the option.
   New readers preserve old preparations. The selection is not permission supplied by a
   client; public JSON still cannot inject authority, root funding or accepted policy.

The HTTP request passes the optional typed selection before computing producer input.
Return a typed validation refusal for unsupported semantics, distinct from an original
intent conflict. The launch review must show the effective read/change/publication policy
and bounded work, rather than echoing only requested values. Native and SPA consumers must
preserve that review through accept/retry; SPA changes stay on `codex/concurrent-mutations`.
UI integration requires its own component-seam and browser proof before calling the
operator flow complete. Process definition defaults need a separately versioned authoring
and template-pack contract; do not add an unversioned JSON field to simulate them here.

## Cost, failure and validation

Fresh governed preparation adds one coherent Workspace read and one selected Agent read
lease. No material capture, provider call, history scan, global workspace lock or retained
database transaction is introduced. Same-intent replay reads the saved preparation and
current authority through existing paths; it does not reread defaults or recapture facts.
No catalog lease may span the Workspace SQL transaction or a provider request. Profile or
Project lifetime changes cause a refusal, not a retry with a different source.

Before implementation, capture scoped dependencies for Application, Persistence and
Workbench and inspect evaluated references. Continue to report collector errors honestly.
The relevant proof is:

- Pure request/fingerprint coverage: null selection preserves a fixed legacy fingerprint;
  changed read mode, contract version, override and caller produce the expected conflicts;
  invalid options and duplicate/missing owners fail; returned admissions cannot replace
  unrelated launch state or omit assignments.
- Real HTTP/durable preparation: an operator selects each read mode, captures the actual
  saved Workspace policy, accepts the exact review, dispatches through P21, reviews one
  actual MAF proposal, and observes one task publication and P22 terminal accounting.
- Change Workspace settings between preview/accept and retry through a new service scope;
  the original preparation retains its exact policy, namespace/revision and funding. A
  new intent uses the new setting. Concurrent same-intent previews admit only one winner.
- Missing policy, unknown contract, multi-activity obligations, unsupported change response,
  insufficient Agent/source authority, changed Project lifetime and inherited callers
  cannot create a funded runtime or invoke a provider. Test legacy launches unchanged.
- Persisted compatibility tests reject the old selection-free hash domain and substituted
  options/admissions, and read unchanged historical preparations.
- Serial affected Release builds, exact build-backed discovery, current source/output
  identities, full portability enforcement, resource reconciliation and maintained docs.
  Broad Stable belongs to the final frozen contract checkpoint, not every local iteration.

The public producer is not qualified by manually populated consumer fixtures. Native/API
producer evidence and actual downstream execution must agree before enabling its UI.

## Public-producer finding: the managed execution port

The initial P23 candidate compiled successfully and passed 36 unit cases, but the actual
HTTP producer rejected its intended single activity. The standard compiler preserves
the selected driver's `ManagedProcessAdapter` host requirement in both the plan and
runtime step. P21's manually admitted consumer fixtures had an empty step host contract,
so its blanket prohibition did not expose this integration gap. The initial HTTP lane
passed six cases and failed five; later regression lanes were not run. Its source/output
identity was verified and process/database/temporary-family reconciliation was clean.

Before repair, the design now distinguishes the required managed execution port from
additional business capabilities. Preserve `ManagedProcessAdapter` in the compiled plan,
accepted requirements and runtime state, and accept it in the single-activity admission
and result-completion checks. Continue to refuse every other required host capability.
The compiler and actual strategy dispatcher retain their existing availability checks;
no fixture may strip the capability or substitute a special driver to make planning pass.
Use one pure Runtime predicate shared by the Application and Workbench checks. This adds
no reference or service boundary. Cover both the allowed port and additional refused
capabilities, and rerun the public path through the real standard driver and dispatcher.

The initial missing-policy test also attempted a V2 write that correctly rejects a null
policy. Its replacement must explicitly seed the supported historical settings row with
no policy, then exercise public launch refusal. Negative HTTP cases must check their
specific public refusal message, so an unrelated plan refusal cannot count as policy proof.

Before snapshot `snap-20261023083017-64aa5311` covers Application, Persistence and
Workbench: 1,803 types, 15,961 members, 580 findings and 205 diagnostics. The scoped
project edges are Workbench-to-Application and Persistence-to-Application. Six existing
Workbench module/type cycles remain; no project-reference cycle is reported. Diagnostics
include 189 WS0002 errors, 12 DI0001 informational findings and four DI0002 warnings.
Direct Release/local-library ProjectReference evaluation passed for all three owners.
These are limited baseline observations, not a claim that the whole solution is healthy.

## Public execution observation

The managed-port repair passes all 37 selection cases and nine HTTP refusal/replay
cases. The two positive HTTP cases exposed a test coordination mistake: `Execute=true`
already enqueues the ordinary dispatch worker, including in the isolated MCP test lane.
Calling a second dispatcher and immediately reading state races the original claim and
snapshot capture. The completed run retains its failed results and verified source/output
identities; process, database and temporary-family reconciliation found no leftovers.

Observe the actual queued operation through bounded reads of its durable runtime state.
Do not manually dispatch, suppress the queue worker, substitute a driver or seed a result.
Fail with the observed runtime status if no planning result becomes durable within the
test deadline. This proves public launch delivery as well as the admission and consumer.
The production implementation is unchanged by this fixture repair.

The queued path then exposed the remaining executor request guard: it still rejected
every host capability after admission had accepted the managed execution port. Both
snapshot modes failed before child enrollment. Apply the same shared predicate to that
last boundary; preserve the actual driver, request and accepted capability. Add executor
refusals for an extra host requirement in both modes, and include safe result diagnostics
in the HTTP assertion. The queue-only candidate retains 46/48 passing executions, a
passing portability gate and clean source/output/resource verification. Its failures
prevent public-producer closure and invalidate any claim that P21 alone proved this path.

The next queued execution reached enrollment but closed startup. A diagnostic decorator
that forwards every call to the real workspace and rethrows the original failure identified
the retained publication fence: the authored activity lacked `ExecuteExternalAction`.
P21 consumer fixtures explicitly granted that operation; the new public fixture did not.
The operation grant must remain mandatory. Reject its absence in the Workbench launch
provider before preparation/funding, and publish the positive fixture with the explicit
grant. Add pure and HTTP negative cases proving that selecting the planner does not grant
an activity an operation its published definition forbids. Do not change the downstream
publication fence or rewrite the assignment's allowed operations during admission.

## Transport fixture isolation

The admitted child reached the real MAF provider but rejected its output. Narrow runtime,
preparation and retained-response diagnostics proved that the expected draft prepares
successfully and satisfies publication bounds. The saved provider envelope instead used
the Responses SDK with an empty message list while the fixture returned Chat Completions
JSON. Starting the second actual host normalizes the managed seed provider identity.

Give the public-launch fixture a separate provider identity and bind its selected Agent
to that profile before starting the second host. Assert the effective transport from the
second host before authoring or launch. Keep the normal registry, runtime adapter and
queued delivery; do not reinterpret empty responses, replace the planner or relax output
validation. Remove temporary response dumping after diagnosis. Both failed diagnostic
rows and their source/output/resource identities remain preserved.

## Prepared-launch transfer regression

The first broad candidate passes the 12 public cases and the earlier launch/fingerprint
topics, then fails two older prepared-launch target-inspection cases. Their real cancelled
roots retain unfinished cancellation recovery, which the existing transfer guard correctly
refuses. `ProjectTransferOwnerInspectionTests` already defines that refusal for both queued
and unseeded cancelled roots; terminal runtime status is not proof of quiescence.

Update those two assertions and the misleading allow-history name to the current contract.
Require the exact residue count, the explicit cancellation refusal and its source-transfer
block, plus the separate nonblocking mismatched-pointer finding when applicable. Keep the
unchanged-evidence and restart checks. Do not remove cancellation metadata or relax the
production transfer guard. Rerun the entire affected persistence topic and all previously
unexecuted topics; carry the already-passing unrelated topics with an explicit test-source
repair boundary rather than presenting both candidates as one run.
