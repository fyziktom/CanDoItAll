# Root material preparation before Process dispatch

Status: accepted for implementation after signed capture ownership `0cea03fa4`.
Proof tier: Behavioral with exact retained results, identities and owned-resource
reconciliation. Scope is the actual root RunSnapshot producer; Agent child execution,
StepSnapshot production and manager continuation remain separate required follow-ons.

## Source findings and ownership

Shared material admission already rejects a new dispatch claim until its scope is Active.
Therefore root capture belongs before claim creation in ExecuteReadyAsync. It cannot
be delegated to an Agent executor that is reached only after a claim. A descendant may
consume Active parent material but cannot capture or refresh for its root.

The accepted-source projection separates a small immutable header from original step
assignments. The header contains the original admission, runtime/plan/definition,
Project lifetime, source authority and preparation fingerprint. Root preparation needs
that header, not an arbitrary step, invented execution ID or whole prepared-launch body.
Ordinary execution authority continues to deny governed dispatch without its real child.

| Responsibility | Owner | Change |
| --- | --- | --- |
| Root source, policy, scope and balance selection | Process Application port / Persistence query | Bounded immutable header and existing scope validation |
| Header decoding | Existing Persistence accepted-source codec | Reuse canonical hash/identity validation without an assignment |
| Root capture orchestration | New cohesive Process Application service | Reservation, one capture invocation, exact completion/recovery |
| Coherent task-plan material | Workbench capture port implementation | Existing ProjectTaskPlanBasisCapture and source-read authority |
| Before-claim integration | Existing Process dispatcher | Delegate once after runtime lifecycle checks, before claims |
| Registration | Existing module composition roots | Required production services; explicit legacy test composition |

No project reference is added. Application remains provider-neutral and does not gain
Agent Core or Workbench dependencies. Workbench references Application but not Process
Persistence or Modules.Processes. The capture port and source-query port represent real
business/storage ownership boundaries. The coordinator itself needs no interface.
Do not add a runtime partial or new generic workflow/transaction framework.

Entry review `snap-20261020221807-6bd2f38c` covers five projects, 2,267 types and
19,717 members. Its seven project edges have no project cycle. The six existing
Workbench module/type cycles, 753 findings and 208 diagnostics remain baseline findings;
this is not a clean whole-repository claim. Review the same scoped graph after edits.

## Bounded root selection

Select the original root budget and admission for the requested runtime. No shared root
admission means no root-capture action, not authority to execute a child. If an admission
declares a shared scope, require the exact stored scope, funding and canonical profile.
Use the existing scope owner's validation rather than a second interpretation of its rows.

For capture, require the accepted OriginalProjection version and decode only its header.
Validate admission, accepted marker, run/root/plan/hash, definition version, original
Project lifetime and source authority against the runtime and root admission. Reuse the
codec's canonical bytes, digest and bounds. A missing/changed row is corruption.
Legacy projection absence is an explicit preparation refusal; never fall back to
decoding a multi-megabyte legacy prepared launch on this new producer path.

Root selection must not read material payload, all assignments, child journals or source
prompts. Scope material metadata suffices to reuse an Active snapshot and check age.
Record actual SQL count/selected bytes with small and many-step accepted roots. Preserve
the historical legacy reader; conversion remains a separate explicit migration concern.

## Reservation and invocation identity

Initial shared capture uses the existing scope expectation and root allocation.
Derive versioned deterministic reservation-command, allocation and material identities
from the original root/budget/scope and the observed AwaitingCapture scope revision.
Concurrent callers of the same logical attempt must derive the same identities.
Do not derive them from the current wall clock or caller display text.

Only the invocation receiving CommittedNow may call the business capturer. A durable
Applied receipt, read-before-write absence or successfully recovered acknowledgement
cannot grant another invocation. Lookup the original receipt before composing a replay.
If scope is already Capturing, return pending without changing its claim or reservation.
A changed budget/runtime revision can be reread in a bounded retry; it cannot replace
an unfinished capture's identity or invoke the business port again.

Use at most three optimistic attempts before returning pending. Return a typed
preparation disposition to the dispatcher: ready, capture pending, review required,
stopped or original source projection required. Public diagnostics derive from that
disposition; exception text and source payloads are never returned to callers.

An Active scope reuses its material. An expired basis or Reconciling/terminal scope
returns an explicit review/stopped disposition. This initial producer must not silently
refresh material, spend a replan or construct manager consent. Descendant requests
observe their root's readiness and never start capture on its behalf.

## Work and transaction boundaries

Reserve a finite capture grant bounded by the original remaining work and a documented
maximum capture duration. Capture uses a linked cancellation deadline for that grant.
Measure actual closed capture duration with a monotonic TimeProvider; do not substitute
the grant for actual consumed work. Invalid negative/unsupported time observations fail.

The initial maximum grant is ten seconds, further limited by remaining root work.
The business port is an awaited, local read whose completion guarantees all owned work
and leases have ended. It must not detach work. A returned refusal or thrown exception,
including cancellation after that task has ended, is known closed work. Record its
measured duration and attempt settlement with a separate five-second accounting token.
If that settlement cannot commit, leave the original reservation pending and surface
the failure. Do not treat an abandoned task or a process crash as that closed receipt.

Run the business read after the reservation transaction is disposed. Attach its canonical
material, settle the original allocation and complete the exact scope atomically in the
existing root unit of work. Validate the returned original namespace/profile/content
contract, payload bound and capture timestamp. The runtime update retains its identity
and advances a normalized timestamp; it must not manufacture a dispatch claim.

A known completed failed read can abort its original capture and settle measured work.
Overrun must settle through AbortCapture, never activate material or refund elapsed work.
An aborted, known-closed initial attempt may be retried at its new scope revision under
the remaining finite budget. The distinction from unknown work is explicit.

A crash, cancellation without durable closure or lost reservation acknowledgement leaves
the allocation pending. Do not infer zero work from absent material. Lost completion
acknowledgement is recovered through the original command/material receipt. No second
read or new material identity may be substituted. If current root cancellation or a
changed state prevents attachment, do not publish material; preserve or settle known
closed work through its accounting owner. Recovery must not weaken stopped-root checks.

Reservation and completion use distinct deterministic command IDs. Completion lookup
precedes recomposition after an acknowledgement failure. A successful completion receipt
may establish readiness but never invoke capture. Scope/accounting transitions remain
their own durable evidence; this slice adds no synthetic strategy result or dispatch event.

No Process root lock spans a Project read, catalog wait, model call or external I/O.
Project capture already uses a five-second transaction and two-second statement/lock
limits, Dataset activation and Project lifetime fences, bounded scalar/task reads and
repeated trusted local source-read checks. Reuse these limits and the existing isolated
test fixture. Root capture never invokes a provider or saved mutation tool.

## Workbench source read

Use the exact accepted root source authority and Project lifetime. Reuse
ProjectProcessLaunchAuthorityService.AcquireResultReadAsync and RequireHeldSource for
current operator/Agent source policy and profile checks. Do not acquire a second catalog
read lease while holding the first: writer preference could deadlock that pattern.
The source Agent's original project lifetime, when different from the target, must also
remain current. Pure read checks may be repeated inside the bounded Project capture.

Encode ProjectTaskPlanBasisCodec's canonical bytes directly as the shared content
payload, retaining its actual capture timestamp and namespace. Do not use the interactive
Agent wrapper that also carries a session-specific write context. Actual child binding
will obtain its own authorized background session and write context later.

## Dispatcher and activation boundary

The dispatcher calls the preparation coordinator before creating or reclaiming governed
dispatch work, while preserving ordinary no-shared-policy behavior. A pending/refused
preparation returns a safe diagnostic and current state without releasing another
caller's work. Required composition prevents a production optional-service bypass.
Fake legacy tests must explicitly select no shared root admission.

Do not lift AgentFrameworkProcessStepExecutor's policy refusal in this slice. The real
child producer, original target binding, saved-effect approval/refresh/cancel and atomic
waiting-step completion still need implementation. Keep settings and SPA activation
gated until those paths are proven. SPA remains on its own branch.

## Acceptance and test selection

Before implementation, complete the scoped architecture/dependency review for Runtime,
Application, Persistence, Modules.Processes and Workbench. Preserve their reference
directions and review composition-register changes.

Required behavioral proof:

- root capture succeeds before any claim, produces canonical real Project task material
  and allows subsequent normal claim admission;
- two callers invoke one business capture; duplicate and lost-ack paths never recapture;
- unknown work, exhausted/overrun budgets and aborted known work have distinct outcomes;
- root cancellation during capture prevents attachment; descendants cannot become owners;
- source revocation, profile or Dataset change, Project deletion/recreation and unsupported
  content/source version fail before material activation;
- the real registered Workbench capturer uses existing coherent capture and original read
  authority, with no Agent session or provider invocation;
- header reads remain bounded at many-step scale and refuse corrupt/missing originals;
- legacy dispatcher behavior, accepted-source compatibility and shared-scope transitions
  retain their owning regression selections.

Record exact owning class/topic filters and expected discovery before execution.
Build each changed production project directly, then refresh the test assemblies in
standard Release. Reuse the isolated PG18 endpoint and record ownership before resources.
Full portability, documentation, rendering-register, source/runtime identity and resource
reconciliation are required. No unfiltered suite merely for this slice; the final named
activation/closure checkpoint still owns broad Stable and sustained capacity.
