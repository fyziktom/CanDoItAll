# P19 retire synchronous root cancellation

Status: qualified by [173 passing cases](../validation/2026-10-23-legacy-cancellation-cutover.md), including the repaired real MAF provider-wait cancellation.
Proof tier: Behavioral with exact source/output and resource records reused from this bundle.

## Finding and decision

`ProcessRuntimeOperatorApplicationService.RequestCancellationAsync` still accepts a root,
loads all cancellable descendants, runs up to five inline passes, signals observers and
finalizes the root in the request. That path bypasses finite admission/availability and
competes with durable recovery. Its old response cannot represent accepted versus completed
cleanup. The generated SPA client expects a synchronous 200; its Process detail is a stub.

Retire the root branch at the Application owner, including its hierarchy dependency,
inline retry/cascade helpers and fixed-pass loop. Loading a root through the old method
raises a typed `ProcessRootCancellationAdmissionRequiredException` before any command,
observer or projection. This also closes direct callers of the broad projection client;
an HTTP-only check would leave an enabled bypass. Keep the selected-child transition and
observer behavior unchanged. It never resolves or cancels the root or siblings.

The legacy HTTP route catches that typed refusal as 409 with a stable migration error
code and a Location pointing to the selected root's `/cancellation` resource. It does not
redirect/repost a mutation, synthesize a completed result or fall back after failure. The
new endpoint remains the only product root admission route, requiring current read and
execute grants and an enabled registered worker. Keep its existing 202/200/uncertain
contract. Update fluent OpenAPI metadata and maintained API guidance; remove stale XML
comments rather than generate new XML documentation. The legacy child route retains its
current contract and authorization. Remove its redundant second projection catchup.

The one real MAF acceptance caller using a root moves to finite admission and the
registered recovery coordinator with a bounded test deadline. Its fixture opts into
availability explicitly; it never changes production defaults. The Project Structure
test cancels an actual child and remains compatible. Old root-cascade unit expectations
are superseded by refusal tests; current finite recovery tests already own cascade,
restart, acknowledgement and fairness behavior. Do not keep unreachable root code merely
to preserve tests for a retired contract.

## Boundaries, risks and validation

No new project or project reference. Application owns the typed refusal; Web maps the
transport; Runtime and Persistence retain their existing mutation/recovery owners. This
removes a coordination path and does not add a generic mutation layer. Reuse current
Application/Web CodeAnalytics snapshots as before evidence and inspect after deltas.

The breaking behavior is deliberate and visible: old root clients receive a failure
before writing. Existing clients must explicitly migrate to accepted/pending semantics.
There is no compatible way to label pending cleanup as old synchronous success. SPA work
stays on `codex/concurrent-mutations`; no SPA main merge or push is authorized. Its future
generated-client refresh is separate from enabling a new Process UI.

Unit validation covers root refusal in each runtime status, no commit/observer/projection,
selected-child cancellation and observer diagnostics, plus existing rework behavior after
constructor cleanup. HTTP validation uses PostgreSQL to require unchanged root/barrier,
children, events and queue on refusal with recovery enabled or disabled, precise migration
metadata, missing/empty identity handling and selected-child preservation of siblings.
Re-run native-client and bounded-admission API regressions and the six real MAF provider
scenarios. Confirm discovery counts after edits, then use the narrow topic filters.

Build changed Application/Web directly and refresh dependent Release outputs serially.
Reconcile existing resource ownership before tests and reuse isolated PG18. Run full
portability, reviewed baseline enforcement, documentation and artifact checks. Record
failures and exact repairs. Broad Stable remains reserved for the integrated freeze.
Manager-chat create/rename concurrency and default recovery capacity remain open next.

## Real producer finding and bounded repair

The first 56-case run passed 55 cases; the MAF provider-wait cancellation timed out.
An unchanged-production diagnostic reproduced it: the original run was selected,
its live registration was signalled, then the durable owner reported `UnresolvedEffects`.
The MAF adapter journals hosted provider-tool dispatches, but ordinary model calls with
only local function proposals deliberately have no provider dispatch record. Recovery
cannot distinguish that safe case from a historical invocation with missing evidence.
A terminal status alone must not erase this distinction.

Persist an immutable, typed provider effect scope on each new provider-restart segment:
unknown historical scope, local proposals only, or hosted tools. MAF owns classification
from its composed tool contract and must reject a hosted-tool wire request against a
local-only segment before invoking the provider. Core stores that contract with the
existing checkpoint write; Models validates it and the transition guard prevents later
replacement. Historical segments keep their unknown default and cannot acquire new
evidence through recovery. Effects-only segments cannot claim a provider contract.

The ordinary background stopper may ignore the missing provider-dispatch record only
for a segment explicitly constrained to local proposals. It still acquires the original
cross-instance dispatch lease, checks current Process authority, and refuses unresolved
local effects or hosted dispatches. A local model response cannot execute a function:
the admitted local proposal and its original effect owner remain mandatory. This says
nothing about whether a remote model request stopped or incurred usage. Requests with
hosted tools retain their existing durable request/response and uncertain-effect rules.

No new project, reference, interface or partial file is needed. Existing Models/Core/MAF
dependency direction remains unchanged. The small contract augments the existing journal
and adapter owners; the broad execution service does not gain another responsibility.
No extra durable publication or full-history read is added per provider request. Existing
provider-only retry behavior remains unchanged. Older readers omit the optional default
field and remain conservative; mixed old/new workers are still unsupported by the bundle.

Qualification adds scope immutability/invalid-value/historical-wire tests, independent
stopper tests for local-only versus unknown/hosted segments, and adapter rejection of
hosted-tool contract drift. Re-run the real six MAF scenarios with the unchanged 60-second
cancellation deadline, original dispatch/proposal identities and no second wire request;
hosted-tool journal and native-loop regression topics cover the unsafe alternative. Keep
both failed runs as evidence and refresh Models/Core/MAF architecture snapshots, affected
Release outputs, full portability, source identity and resource reconciliation.
