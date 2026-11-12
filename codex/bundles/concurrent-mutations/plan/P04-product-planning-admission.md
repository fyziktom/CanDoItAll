# Product planning admission and orchestration

Provider, saved publication and historical receipt boundaries now have actual registered
proof. Connect them to the existing Workspace execution entry and UI only after their
admission and continuation invariants are explicit. The first product operation plans
flat tasks for its original Project Structure source project. It cannot infer a different
target from the Agent's latest permissions or a currently selected UI project.

## Admission before the atomic run publication

Use an explicit typed planning owner selection on a new execution request. Ordinary
chats remain on their existing path. Planning requires an interactive, chat-backed,
current-schema Project Structure authority with an exact source project lifetime;
unsupported transient attachments or unrelated structured-output modes fail before
provider work. Never reconstruct missing authority from current grants.

The module participant prepares a small immutable admission before entering the file
store's synchronous atomic-start callback. Read the existing Root Workspace v2 settings
owner and require its persisted policy and head. Record the actual dataset namespace
and head revision, actual admitted Agent catalog revision and policy override, and the
module's capabilities/budget ceiling. An absent or invalid Workspace policy is an
explicit refusal, not an invented default. Use the selected catalog lease to verify the
prepared Agent revision, capture original grants, and apply the existing source mutation
authority and exact project-lifetime checks. Recheck the Workspace/profile context.

The actual reader is the existing `WorkspaceSettingsWriter.ReadV2Async` persistence
owner. Do not depend on `IWorkspaceDefaultsOwner`: that UI contract constructs provider
administration, whose runtime factories resolve registered planning participants and
close a service-construction cycle. The first actual integration candidate exposed this
deadlock despite an acyclic project-reference graph. The persistence owner already
checks its context/profile and dataset binding; admission also checks the caller's exact
profile/generation/fingerprint before and after capture. No service locator, lazy
resolution or new trivial interface is needed to hide the cycle.

Return a bounded pure session-binding function plus the frozen provenance. The existing
atomic chat start supplies the real run, chat and authority identities, checks its
existing catalog revision, and publishes schema six on the first run write. No SQL,
catalog reacquisition, provider work or asynchronous callback runs inside that file
publication. Preparation may retry only before any admission is published, using the
original requested project lifetime and authority. It must not refresh an old target.

The captured Workspace namespace also fences task-basis capture and restoration.
Restoring/importing a dataset must not combine an old policy/source identity with a
new task basis merely because public project IDs survived. This is separate from the
original SQL operation's namespace/caller and the current profile-generation fences.

## Small coordinator, existing execution entry

Keep planning transitions in one top-level Core coordinator composed with the existing
journal/owner, planning runtime, continuation runtime and registered participant. Core
does not reference Workbench/Projects or decode MAF checkpoints. No additional broad
partial class or general execution pipeline is justified. Existing execution methods
only route explicit planning requests/actions and retain their credential scopes,
activity ownership, cancellation registration, history, usage and final-result handling.

Carry the explicit owner through the existing chat options and execution request. Reject
background/same-source launches, attachments, unrelated output contracts and automatic
approval before planning preparation. At each permitted pre-admission catalog retry,
prepare the participant and original runtime digest from that attempt's actual Agent and
provider. Pass the prepared value into the existing synchronous start callback; bind
the digest to original run metadata and publish the planning journal together. Old
callers without a planning selection retain their ordinary execution route. A missing
planning port is an explicit unsupported-operation error when planning is selected.

Register the stateless MAF planning step runtime directly in the MAF composition
extension, with its provider factory and logger. It must not resolve the broad
`MafAgentRuntime` aggregate, tool providers, Memory adapters or participants merely to
construct the planning port. Actual ordinary execution regression testing exposed a
factory cycle through Memory when the first registration used that aggregate. The
workspace factory consumes the same narrow registration. Keep a construction tripwire
test as well as actual registered workspace execution proof; static project graphs
cannot establish that factory-based service activation is acyclic.

The coordinator resolves one registered participant by typed owner, verifies the original
runtime contract before opening work, and starts with the exact original expectation.
Each admitted response either advances one bounded step, closes active work for its
original approval, or settles a known refusal. Keep actual usage from every completed
provider step, including a refusal or later exception. A provider/owner failure without
acknowledged response state retains its work anchor; do not infer a successful terminal
result or reset elapsed work. The outer execution entry must skip ordinary automatic
approval for planning. Return a typed acknowledged run outcome alongside each completed
planning response so ordinary response completion cannot overwrite refusal or cancellation
as success. Unknown failures retain completed usage through the existing usage-bearing
exception path. Attach each step's history evidence before combining its usage; do not
attribute every step to the last provider invocation.

An exact expected decision admits one step under its existing run lease. Stale concurrent
start/Refresh requests must fail before opening another work interval or invoking the
provider. Capture according to RunSnapshot/StepSnapshot, use the configured planning
port, and persist a renewed approval for every newly prepared publication. Approval wait
does not consume active work. Do not invoke ordinary automatic-approval continuation on
a planning checkpoint with no SDK conversation checkpoint.

Interpret saved publication through a pure module outcome classifier bound to the
original decision/binding/operation. Only an acknowledged definite-no-effect BasisChanged
result can pause or trigger bounded Replan. Dispose the old work/dispatch lease before
Refresh and a new generation. Unknown effects or lost work anchors permit receipt
observation only. Refresh/Cancel use exact displayed expectations and current read checks.
Retain the existing owner ceiling and total spent work across every generation.

Saved action commands carry the exact displayed chat, run revision, journal revision,
decision and generation. Approval additionally carries the original approval ID and
proposal binding. Require runtime support before acquiring mutation authority. After
dispatch, compare the returned acknowledgement with the actual retained proposal and
revisions; the owning module classifies its original operation's result. A missing,
inconsistent or unknown result retains the work anchor. A confirmed BasisChanged result
obeys Fail/Pause/Replan policy. Release the old dispatch lease before using its exact
settled expectation for automatic Refresh, so a concurrent action cannot be upgraded to
the latest generation. A cleanup failure prevents a new generation.

Refresh prepares and validates the original runtime contract before retiring approval.
Use the existing execution finalization path without admitting another run or appending
another user prompt. Carry prior approval audit into finalization. The current provider
configuration observed for new computation must match the original contract; saved
publication and cancellation do not require a provider connection. Every new generation
retains policy and charged work and requires renewed publication approval.

The existing workspace facade and current-profile wrapper expose one typed saved-action
request. They pin its original chat and activity operation; the execution entry routes
to the coordinator and uses the existing cancellation registration. Release both the
dispatch lease and cancellation registration before a permitted automatic Refresh.
Refresh also checks total step capacity before retiring the displayed approval, avoiding
a new generation that cannot start. Reuse the existing finalization method with prior
approval audit, usage/history and transcript; do not append a second user prompt.

## Provider configuration versus saved publication

Bind new computation to its originally selected provider identity/model and runtime
options; use the existing provider configuration fingerprint and a bounded adapter-owned
contract for options/schema. Store only a non-secret digest in owner metadata. A changed
provider contract refuses a fresh step/Refresh explicitly. It does not block receipt
review or publication of an already prepared original proposal under current authority.
Do not pass raw provider secrets or full configuration into a module target envelope.

The MAF port must check the contract at actual provider entry as well as preparation.
Current permission checks can narrow the original grant, never enlarge it. Provider
cleanup or acknowledgement failures preserve the original dispatch and any recorded
response; they cannot allocate a replacement decision or silently reset work.

Use an adapter-owned contract digest on the explicit planning-step request. Compute it
from the prepared Agent/provider, resolved model, effective bounded chat options and the
participant prompt/schema. Persist the digest in original run metadata at admission;
Refresh reads it unchanged. This keeps MAF serialization and provider options out of
Core's decision schema. The port recomputes and compares it before creating a provider
and again at the final admission boundary; a bare provider ID/model comparison is
insufficient. Existing direct runtime fixtures must supply their originally captured
digest explicitly. No null/default contract silently opts a planning run out.

Persist the original admitted workspace scope in the same versioned runtime binding.
Validate it against the journal session and authority before computation and runtime
audit setup. Planning does not retain or restore the ordinary chat's transient view;
its participant captures its own material basis. Never weaken the ordinary runtime's
scope resolver to trust arbitrary chat metadata. A scoped transient request must agree
with the admitted authority; its old view text is not planning input. Incompatible old
bindings refuse new computation while owner receipt inspection remains independent.

Disable the ordinary empty-completion corrective retry inside an admitted planning
request. It changes provider input after the immutable input check and would put two
different computations under one recorded dispatch. Admit the actual empty response and
let the participant reject it; retain its usage/history and emit explicit suppression
telemetry. Transport retries of the identical request remain subject to the provider's
own bounded contract and cannot justify changing the saved planning input.

Step-start acquisition must check the exact displayed decision under the real run lease
and atomically claim that lease and open the first work interval. Do not call ordinary
AcquireRunAsync and then replace the caller's expected revision with the latest read.
The latter would invalidate a legitimate first request and could upgrade a stale second
request. Reuse the saved-action original-run checks and current-read callback, then apply
BeginStep in the atomic owner update. No provider or SQL work belongs in that callback.

## Host and UI contract

Use the existing module/host seams for an explicit Plan tasks action and planning review
state. Show the captured policy, basis age, remaining work, generation, preserved draft,
typed refusal and exact available actions. UI drafts are never replaced by a notification
or delayed callback from a retired opening. Approval binds the original proposal;
Refresh creates a distinct generation and requires renewed approval. A read-only receipt
action must remain available when publication cannot resume, subject to current reads.
SPA work remains on `codex/concurrent-mutations` and is not merged into SPA main.

The review is an ephemeral projection on execution detail, including completed and
cancelled decisions. Before disclosure, Core checks the original profile/chat/authority
and the participant checks current source/target read access. It contains the exact
run/journal/decision expectation, saved policy, generation, basis timestamp, settled
work and remaining budget. An open work interval exposes no estimated remaining budget
and no mutation action. Available actions are advisory; the owner rechecks every action.
Only Workbench interprets task content for a bounded plain-text publication review.
Original source envelopes, private profile fingerprints and receipt payloads are absent.

The HTTP planning-action endpoint requires WriteAgents and no-store responses. It reads
at most 8 KiB before resolving the workspace owner, rejects unknown/duplicate members,
checks route identity, and accepts positive Int64 revisions only as decimal strings.
Approve carries the displayed original binding; Refresh/Cancel cannot carry approval.
Unknown outcomes retain the original action/run identity and never advertise retry.
Existing activity operation identity and admission handle duplicate HTTP requests.

Keep the review projector in the existing Core coordinator, the task description in its
Workbench participant, and transport in a focused Web API file. Add no project reference,
service registration or execution-service partial. Reuse the existing revision codec and
OpenAPI transformation. Test real registered detail reads before/after publication,
permission revocation, unknown work, stale actions, HTTP authentication/body limits,
lossless revisions and bounded dispatch. Native UI follows after this boundary passes.

## Proof and release order

First prove the actual persisted Root Workspace/Agent preparation: missing/invalid
policy, override inheritance, incompatible mode, actual head/catalog provenance, source
revocation, stale preparation, profile/dataset change and pure real-session binding.
Then prove coordinator ordering and existing execution-entry routing with configured
MAF transport, file owner and PostgreSQL, including two concurrent starters, stale
approval/Refresh/Cancel, provider-contract change, unknown commit and lost work anchor.
Use the existing native component/browser hosts for rendered controls and two actors.

Do not enable a writer whose required source/target perimeter still has an unresolved
bypass. Retain exact proof boundaries: registered module publication is already proved;
product actions, process immutable assignment/policy/claim fencing, workflow occurrence
and enclosing budgets, remaining owner writers, sustained capacity, broad stable gate
and resource cleanup are still required. Each implementation candidate uses standard
builds, confirmed discovery, dependency review, full portability enforcement, observed
execution and final source/runtime/resource verification.
