# 17. Owner-operation protocol and multi-owner journeys

## General protocol

This is a semantic protocol applicable to CRM, Agents, Simple Chats, Resources, Scheduler, Structure, and other owners. It is **not** a universal mutable payload or all-domain operation bus.

**Resolve:** bind trusted actor/delegation, admitted scope/history, purpose, selected owner and exact typed action. Validate registered adapter/handler and data-use permissions. Model parameters cannot supply effective authority.

**Prepare:** resolve precise target/source IDs, safe editable fields, expected revisions, and command schema. Allocate/persist the logical intent ID before an automatically retried dispatch. Bind review/approval to normalized action and material payload.

**Authorize:** apply current actor/operation/resource/field policy and lifecycle. Verify approval where required. Enforce budgets, scope, size, and kind constraints. Recheck at resumed dispatch; approval is not an indefinite grant.

**Execute:** owner applies domain rules and writes its state. For retry-safe local commands, commit effect and unique receipt together. A multi-owner short SQL transaction has an explicit coordinator and shared transaction infrastructure; external I/O is not held inside it.

**Acknowledge:** return confirmed identity/revision/effects, durable accepted state, rejection/conflict, or observer uncertainty. Report post-commit projection/synchronization warnings without denying the saved entity.

**Deliver/reconcile:** complete separately owned effects using their own stable IDs/receipts. Notify views after commit. Recover by querying owner status rather than repeating a possibly completed create.

## Conceptual command and receipt fields

| Field | Rule |
|---|---|
| Owner action/schema | Fixed registered typed operation and version; never arbitrary table/method. |
| Intent identity | Stable caller/producer namespace plus logical origin/step/iteration/effect and correct scope/history. |
| Attempt identity | Diagnostic retry identifier only; changing it never creates another business effect. |
| Target/source references | Exact owner-issued IDs and meaningful revisions; no implicit current selection. |
| Semantic fingerprint | Normalized material action/payload/targets; exclude rotating auth token, lease, transport timestamp. |
| Authority reference | Trusted reconstructed context and admission ceiling; never grant-bearing JSON from the model. |
| Approval reference | Exact approved fingerprint/target/revision/purpose and permitted approver; current validation still applies. |
| Owner receipt | Intent, immutable result identity/revision at commit, applied effects, pending obligations, safe recovery/status handle. |
| Current object state | Optional separately queried state, not retroactive modification of the historical receipt. |

Keys must be protected against collisions between actors, owners, actions, scopes, and schema meanings. Use strong canonical hashing where a digest is used; do not treat random display names as identity. A replay can return an old receipt only if the caller may still see it. Same key with changed payload conflicts. New legitimate repeated actions get new logical intent identities; retries do not.

## Commit and retry state distinctions

| State | Meaning | Safe next action |
|---|---|---|
| Rejected/Denied/Conflict/Unsupported | Owner did not admit/apply the requested effect, as established by its result. | Correct or approve a new explicit intent; not silent target substitution. |
| AwaitingApproval | Exact prepared action awaits an eligible decision. | Resume only with valid decision and rechecked policy. |
| Accepted | Durable admission exists; execution/effects may remain pending. | Observe/control the admitted operation. |
| Committed / Existing | Owner confirmed effect or previously completed same intent. | Use receipt; do not repeat creation. |
| CommittedWithPendingEffects | Original write committed; named downstream work remains. | Retry/reconcile only pending effects. |
| Unconfirmed | Observer lacks acknowledgement; not proof of rollback. | Query same operation or reconcile before redispatch. |
| NeedsReconciliation / ExpiredUnknown | Safe automated determination is unavailable. | Bounded authorized operator/business decision; no invented exactly-once. |

Cancelled is an owner state-machine decision, not a disconnected socket. Data delivered to another domain may still be pending after technical run success. Required obligations remain visible until fulfilled, safely compensated, or legitimately waived; optional effects return explicit warnings. No global semantic enum is mandated, but adapters preserve these distinctions.

## Atomic receipt boundary

For a local create, a durable uniqueness constraint and owner effect/receipt transaction prevent concurrent duplicates and crash gaps. In-memory locks or an integration record saved afterward do not. If an existing owner API cannot participate, extend it narrowly or explicitly preserve uncertainty; do not label an adapter “idempotent” because it caches responses.

Simple Chats definition commands illustrate the issue: current request shapes do not contain a command ID [SRC-034]. HR create needs an owner-controlled retry-safe extension or proven shared transaction before automatic retries. Conversation-create remains its separate existing contract. CRM/schedule create paths also require actual inspection of uniqueness/transaction behavior before claiming the new protocol is implemented.

For remote effects use supported remote idempotency/status lookup where available. A lost reply from a non-idempotent provider is uncertain; local outbox cannot guarantee exactly-once. Restore/incarnation changes do not legitimize new remote keys for old effects. Receipt retention must cover retry/replay; expired old intents require reconciliation, not default recreation.

## Workflow/process checkpoint gap

Persist logical effect identity with the step plan before dispatch. Derive it from admitted run, concrete step occurrence/iteration, action and effect slot, not a new LLM call ID or retry counter. When the owner commits but the workflow/process checkpoint fails, recovery queries the owner receipt and records the existing result. It does not create the contact, schedule, task, or chat again.

A new intentional loop iteration gets a new effect slot. A changed payload under an existing slot conflicts and requires explicit revised intent/approval. The owner of execution records checkpoints; the destination owns effects. Per-step query decisions retain minimal reference/version evidence with current disclosure policy.

## Multi-owner coordination and compensation

Prefer one owner command for one fact. Use live queries for small composition. Short multi-context transactions can preserve an existing local invariant with a fixed order. Long operations use a named business coordinator and durable per-effect state; no central module owns every business entity.

For example, agent creation can commit before CRM enrollment or a task assignment. Report that partial state and repair the enrollment/assignment. Do not delete a successfully created or subsequently edited entity as a hidden rollback. Compensation must be explicit, owner-authorized, revision-checked, and limited to what that intent created. Pre-existing CRM contacts/resources and later human edits must survive downstream failure.

Prevent synchronous owner callback loops and event echo loops with distinct operation roles, causal lineage, dedupe, bounded retries/hops, and declared lock ordering. Limits never grant authority. A read, UI notification, or result projector cannot initiate the original execution again.

## JRN-13 — HR administers technical agents

Resolve actual managed HR identity, safe options and target. Existing typed HR paths remain [SRC-029, SRC-030]. Review an allowlisted create/update, enforce protected-capability policy and concurrency, call Agents owner, and return technical receipt. CRM projection/team warnings are separate effects. No new write path through CRM or Structure, no self-escalation, and no loss of existing avatar/usage/review functionality.

## JRN-14 — HR or CRM specialist creates a contact

Use safe CRM search only as an optional duplicate suggestion; exact duplicate/merge policy belongs CRM. Prepare a nonsensitive typed Party creation through the already-used ICrmPartyCommandService, review under current policy, and preserve canonical audit/index/lifecycle [SRC-029]. Return the Party reference and receipt. Add a supported contact point/affiliation as an explicit owner action if not included in the atomic command. Broad contact-point support is not inferred from Party creation. Neither agent holds CRM entities/context.

An ordinary workflow can perform the same approved business action through a separately registered executor and delegated principal. It does not impersonate HR, and an optional specialist does not create another CRM store.

## JRN-15 — HR administers Simple Chats definitions

Search permitted definitions, retrieve safe settings/token, prepare exact fields and approval, then call existing Simple Chats owner API. Changes create the owner-defined revision and preserve admitted-turn history. Create uses proven owner-atomic intent receipt before automatic retries. Return definition/revision without opening or reading conversations. Floating ordinary-chat rendering remains tool-free and unaffected by external administration.

## JRN-16 — Read CRM and Resources, create a task

Use separate safe CRM identity and Resources metadata/content queries, validate egress and destination audience, resolve exact references, and call Work Management. Recheck current party/source eligibility and mandatory capacity rules. No cached full CRM entity in a task, no resource filename used as authorization, and no CRM copy of task assignment. A failed task create does not delete a previously existing contact.

## JRN-17 — Workflow/process writes outside Structure

The admitted step selects a registered typed CRM/Resource/Simple Chats/etc. action. Restore its trusted scope, enforce approval, dispatch the persisted logical effect, and checkpoint the receipt. Crash after owner commit is recovered through receipt lookup. On target unavailability retain pending/blocked state; no browser required. Optional outputs may warn; required outputs cannot be silently dropped. Each destination is direct through its owner, not via a surrogate Structure node.

## JRN-18 — Scheduler and curators

Scheduler assistant discovers the exact workflow/version and writes a canonical plan; later firings are distinct stable intents, under current execution policy. Workflow-only managed support is retained; no invented process tool. Prompt/workflow/capability curators call their owners, preserving versioning, validation, activation, and protected grants. A descriptive catalog record is never sufficient proof of executable handler or permission.
