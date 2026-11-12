# Live decision ownership

Reviewed after the schema-six 94-case checkpoint. Implement the Core live owner first,
with actual file-owner tests; the MAF/module connections below remain subsequent work.
No product activation is claimed by this source card.

The implemented Core candidate uses the journal's own `TimeProvider`, so durable UTC
observations and the owner-issued monotonic handle cannot accidentally use different
clocks. Basis capture starts after work admission and timestamps the observation before
the awaited read. RunSnapshot reuses the exact saved basis without calling the reader;
StepSnapshot calls it again. A handle also rejects a monotonic observation below its
previous high-water value, including a positive but regressed elapsed value.

Known provider output is retained when the work limit expires, UTC rolls back, or the
prepared publication has an invalid effect contract. The response result explicitly
reports the refusal; no proposal is added and the generation fails with its accounting
interval still open. A clock failure does not invent a settlement time: the output uses
the previous valid accounting observation, and settlement remains refused until a
valid clock observation exists. Cancellation retains its original state. The live
terminal settlement and approval-wait publication save the run state and journal in
the same file-owner mutation. No saved-effect resume or user-action activation is
included in this candidate.

The first 112-case file-owner run found one actual integration defect: after clock
rollback, the known response kept its last valid work observation but the journal
publisher still moved the enclosing run's `UpdatedAtUtc` backward. The file owner
correctly refused the write. The repair retains the previous run timestamp only on the
explicit outcome-recording path; work admission/settlement still refuses invalid time.
The response carries the typed clock refusal, so this is not a silent accounting reset.

Cancellation now uses the existing execution merge's `RejectUndispatchedOnCancellation`
entry for schema six. It retires prepared publications as known not committed, cancels
the decision (including an already failed decision), and preserves spent/open work.
Its pure planning provider dispatch remains available for the original late response;
schema six prohibits native approved provider calls. Schemas one through five retain
their existing provider-cancellation disposition. The follow-up file tests exercise
this actual merge helper, not a test-only replacement journal.

The dependency audit `snap-20261019044516-daf4d496` covers the same five AgentFramework
projects (526 documents, unchanged project-reference edges). It identifies one new
local type cycle: the decision owner constructs a live work handle, and that handle
stores the exact owner solely for reference-identity comparison. The handle never
invokes the owner. This is the reviewed capability boundary, not a module dependency
cycle, service locator, registry or new locking layer; the four prior type cycles are
unchanged. The source candidate has no new partial class or project reference.

## Owner and file perimeter

`AgentPlanningDecisionOwner` belongs to Core beside the pure transitions. It consumes
the existing `AgentToolAdmissionJournal` and its real run lease, plus `TimeProvider`.
It must not create another file store, lock, grant registry or execution-service partial.
The journal supplies one narrowly scoped internal planning mutation operation using its
existing reader/writer, original session/profile checks and durable atomic run publisher.
User actions carry exact displayed run/journal/decision/generation expectations. Live
provider work validates its owner-issued handle and exact open interval, not a new UI
expectation or caller-supplied elapsed value.

The initial Core entry points are start, attach basis, enter provider, admit response,
advance step and settle. They require the bound original dispatch lease. The journal
checks the original run/session/profile/source before the pure callback and central
persistence validation afterwards. A separately marked outcome-only path can retain a
late response/settlement after cancellation; it cannot start work, create proposals for
a cancelled generation or change the original authority. Saved-effect resume, user
Refresh/Cancel/Approve and Workbench publication remain explicit later branches, not
permission inferred from this live handle.

Creating a planning journal is allowed only before the first new-run publication. Reuse
`CreateForNewRun`'s new-run, original input and execution-authority validation, then create
schema six in memory before storage. No stored schema is promoted. The module admission
must supply a real Workspace policy observation and Agent override; a copied provenance
record is data, not authority. Root policy absence refuses planning instead of selecting
a default. A later settings edit cannot re-resolve an already admitted decision.

The live work handle has an internal constructor, actual run lease, owner identity,
decision/generation/step/interval IDs and an in-memory monotonic anchor. Its optional
ambient binding is an adapter handoff, not an authority registry. It borrows the run
lease and cannot close accounting merely by being disposed. The owner opens the interval
inside the atomic admission callback (queue time before admission is excluded), before
awaited basis capture or provider work. Failed/unknown open acknowledgement does not
return a usable handle or start another interval. A fresh owner cannot reconstruct a
handle for a saved open interval. Current clock rollback refuses further activity;
cancellation and known outcome recording preserve that unresolved accounting anchor.

Basis attachment, provider entry and response admission each require that original
handle. Every provider request gets a persisted origin even when no native tools exist.
No native tools or ordinary workspace tools are exposed by the first planner. A drained
response either advances immediately to another step with an atomic zero-gap interval
transition, or closes work with the approval/review/terminal state. Raw response and
proposal bindings are one publication; cancellation before admission retains the actual
response with no new effect proposal and cannot revive the generation. Known entered
effect results remain independently recordable even after cancellation/budget failure.

## Actual MAF and module seams

Use the configured `IMafProviderAgentFactory`, not a fake provider/SDK checkpoint.
Installed Microsoft.Agents.AI 1.22.0 explicitly supports
`ChatClientAgentOptions.UseProvidedChatClientAsIs = true`, which suppresses automatic
function-invocation decorators. A fresh configured Agent/session per new logical step
receives only the immutable original request and the selected material basis. Same-step
transport retries keep identical input. Do not restore old factual conversation state
and label it current. The actual `MafToolAdmissionChatClient` boundary must verify and
persist the final input digest against the live step before transport entry.

A narrow Core participant port is justified by the real Workbench/Core boundary. The
Workbench implementation owns target validation, canonical basis capture, bounded task
response schema and pure proposal/source preparation. Core/MAF must not reference task
entities or parse product task fields. The provider output is retained as real output;
invalid output records a typed terminal no-effect result, not a fabricated function call.
The one proposed publication is an explicit task-planning effect subject to original
approval; it never runs automatically as an SDK function. Bound output/history limits
must reserve sufficient journal space before transport entry.

Connect existing interactive preparation/activity/governance rather than creating a
second general execution pipeline. `AgentFrameworkWorkspaceExecutionService` remains
an orchestration caller: new planning admission routes into the cohesive owner/runtime
collaborator. No new partial or extension dumping ground. Current profile/generation,
original project source and selected Agent/provider preparation remain required.

## Saved publication and remaining cutover

Schema-five saved dispatch remains unchanged. Schema six needs an explicit owner branch:
exact original proposal/step/generation, closed wait, current authority, original approval,
new live work interval and original receipt preflight. The current schema-five one-segment
check must not simply be removed. Publication holds the existing selected run/Agent
exclusion, rechecks the live work budget in the pure final fence, and releases it before
checkpoint/settlement. Dynamic work counters do not enter the immutable operation digest.

Workbench command/source v2 must explicitly select active-work interpretation while v1
wall-age semantics and receipt decoding remain unchanged. Automatic replan is internal,
only after a typed known no-effect changed-basis result and positive saved allowance.
Refresh/Cancel/Approve use one exact CAS winner, never replace uncertain effects, and do
not reuse an old approval. Native/API/SPA actions follow only after these actual owner,
transport and publication paths pass. SPA remains on its separate branch.

Proof must use real file owners, deterministic transport, actual two-step provider input,
current revocation, closed-wait resumption, lost open/close acknowledgement, clock
rollback, no-gap work, stale refresh/approval/cancel races and late results. Existing
ordinary provider and schema-five tests remain regression lanes. File/SQL max-size,
contention and sustained-capacity qualification remain distinct from these functional
proofs. Process/Workflow original assignment and child-key propagation follow as required
producer work, not as a completed consequence of this Agent slice.
