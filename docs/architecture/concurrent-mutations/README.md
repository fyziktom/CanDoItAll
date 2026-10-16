# Concurrent mutations architecture

Status: architecture direction reviewed; P01a cleanup validated and committed; P02a
definition protection is in progress. This document specifies the target and does
not claim that the engine already provides every guarantee below. Implementation and
proof are tracked in the [execution bundle](../../../codex/bundles/concurrent-mutations/README.md).

## Outcome and scope

A person, agent or workflow must not overwrite a newer value using an older read. Work
based on superseded objectives must have an explicit execution policy. A lost response
must not turn a retry into a second effect. These are three distinct responsibilities:
target preconditions, decision-basis policy, and durable operation identity.

The architecture preserves the modular monolith. Each fact owner defines its mutation
boundary and its invariants. We will not add a global mutation service, universal
revision on every row, global write queue, mandatory realtime subscription, or locks
held during human/model work. Existing owner-specific versions and expected-state
contracts remain valid where their complete write perimeter is proven.

The initial closed vertical is Projects definition editing: scalars, ordered phases
and technology options. Workbench structure, canonical tasks, CRM/HR, definitions,
settings and runtime transitions follow their own owner contracts. Existing protections
are retained and regression-tested; missing protections remain explicit work, never
implied by the Projects pilot. External effects receive only guarantees supported by
their actual driver/provider protocols.

## Design evidence

Initial source: CanDoItAll `ead988abeb4cfb3e12341e618362bfa3dde5df29`, branch
`concurrent-mutations`, clean before this task. The architect's v0.1 ZIP is preserved
as reference input with its original proposed decisions. It is not a runnable work
order. Current source has materially evolved since that package:

- Projects UI is extracted and already preserves submission identity, row identity,
  typing after dispatch, and owner generations. Extend that seam.
- Workbench graph writes already carry original node/graph expectations and project
  admissions. Replacing these with a root project revision would create false conflicts.
- Process claims, source authority, prepared tool identity and immutable execution
  definitions already exist. A freshness policy must compose with them.
- Projects whole-aggregate saves still lack an original content precondition.
- The scalar-only agent project adapter uses a whole-replacement save, risking removal
  of phases/options. A token alone does not repair an incorrectly shaped command.
- Project search updates occur after commit and can publish an older payload later.
- SerializableMutationScope can leak its process semaphore when transaction disposal
  throws. Cleanup must release acquired locks even when another cleanup step fails.

CodeAnalytics snapshot `snap-20261016142537-4d8bda80` loaded four projects and 224
documents (Projects, Infrastructure, Infrastructure.Abstractions, SharedKernel). Its
module-cycle finding concerns existing Infrastructure submodules, not a newly added
project edge. Scoped snapshots do not prove the entire product graph. Exact source and
project references supplement them; diagnostic limitations are recorded in the inventory.

Detailed design:

- [Owner, persistence and transaction protocol](01-owner-transaction-design.md)
- [Agent, workflow and process policies](02-agent-process-policies.md)
- [Editor behavior and validation](03-editor-and-validation-design.md)
- [Performance, capacity and operational risks](04-performance-and-risk-plan.md)
- [Owner coverage and remaining write perimeters](05-owner-coverage.md)
- [Caller, dataset and operation-retention lifecycle](06-operation-lifecycle-contract.md)
- [Exact Projects contracts and caller cutover](07-projects-contract-cutover.md)

## Invariants

1. An update carries the expectation from the original read. A fresh read immediately
   before saving cannot silently replace that expectation.
2. A content revision identifies an owner-defined editable boundary, not authority,
   project incarnation, profile activation or execution claim.
3. Every write to that boundary participates, including child-only writes, agent tools,
   HTTP, imports and controlled maintenance. An enabled bypass prevents owner closure.
4. Root, children, accepted operation result and mandatory same-database projection
   effects commit atomically. An optional activity feed is not the durable audit record.
5. Coherent reads return content and revision from the same database snapshot. A root
   revision combined with children from a later read is not a valid editing basis.
6. Access, current claim and revocation are checked at effect time under the existing
   authority scope. Keeping an old information snapshot never preserves old authority.
7. A known successful commit stays successful when subsequent cleanup, feed or readback
   fails. A lost commit acknowledgement is explicitly unconfirmed until reconciled.
8. Exact retry uses the same operation identity and semantic payload. Replanning,
   changing a proposal or intentionally creating a new effect uses a new identity.
9. No lock, transaction or DbContext spans a human wait, model call, remote provider call,
   filesystem transfer or process execution.
10. A dirty editor remains dirty on conflict; refresh/rebase is explicit and preserves
    the user's intent. Late responses cannot replace another editor generation.
11. Validation is bounded by the affected aggregate, owner batches and explicit limits.
    No portfolio scans or per-item cross-owner calls in the ordinary save path.
12. Missing basis, incompatible persisted policy, unknown effect and denied authority
    fail explicitly. No implicit force-save, automatic token refill or unbounded retry.

## Responsibility and dependency map

| Responsibility | Owner | Allowed dependencies | Forbidden coupling |
| --- | --- | --- | --- |
| Definition stamps, commands, outcomes | Projects.Contracts | Existing neutral contract dependencies | EF, runtime or UI types |
| Conditional project writes and operation journal | Projects module | Projects contracts, Infrastructure, SharedKernel | Workbench/MAF/runtime implementations |
| Transaction participation and cleanup | Infrastructure | EF/Npgsql and existing infrastructure abstractions | Product conflict/merge/approval policy |
| Scalar project tool adaptation | Workbench owner adapter | Projects contract/owner seam and existing source-authority bridge | Direct Projects table mutation |
| Basis policies and checkpoint representation | Existing provider-neutral execution contracts/Core | Typed neutral policies and owner-provided facts | Projects/CRM implementations or provider SDKs |
| Material facts and membership witness | Fact-owning module | Its own queries and typed consumer ports | Generic runtime discovering database aggregates |
| Runtime/tool/claim binding | Existing runtime and module adapters | Neutral policies plus existing authority protocols | Moving product rules into MAF |
| Form, local draft and rendering | Projects.UI | Projects.Contracts, approved Components | Infrastructure, DbContext, service locator |
| Acquisition, submission, recovery and conflict actions | Native Projects host | Owner services and renderer contracts | Hidden renderer writes or automatic dirty reload |
| Canonical schema and data lifecycle | App composition/migrations/control plane | Existing full model and owner configurations | Independent owner migration history |

Existing broad ProjectsService code must shrink as mutation protocol grows. Extract a
cohesive top-level project definition writer and directly test it; keep the legacy
facade thin. Do not add another partial file containing the new protocol. A separate
project is justified only by dependency direction or independent implementation lifecycle,
not by the size of a file. No new project references are required by the cleanup repair.

## Transaction and outcome design

Use the existing application-managed GUID mechanism for the Projects definition
revision. Set EF's original token from the submitted precondition and always advance
the root for a child-only mutation. Include the original lifetime/profile identity.
Scalar-only commands preserve collections; full replacement validates distinct,
owned, existing child identities before touching tracked state. A missing child ID
means create; an unknown nonempty ID means conflict/invalid intent, never recreate.

Preserve source authority before owner locks, then acquire the complete bounded owner
key set in the existing deterministic order. Do not nest independently sorted sets and
claim that global ordering follows. A Serializable snapshot taken before an advisory
wait may be stale; current row locking/conditional writes must still detect that case.
Do not downgrade isolation globally to improve a benchmark. The measured baseline
exposed SSI index-page conflicts between disjoint Projects writes even with indexed
root-only queries. The definition-specific ordinary path therefore uses Read Committed
under complete ordered project/identity guards, mandatory original root CAS and existing
database uniqueness constraints. Other scope consumers and source-authorized mutation
isolation remain unchanged. See the owner design for the invariant proof and diagnostic.

Use the existing coordinated transaction for the small same-database Projects search
projection, subject to measured bounded cost. If that cost fails the release gate, the
alternative is a completed durable ordered rebuild protocol, not best-effort indexing.

An owner-local operation record is necessary because creation compensation receipts
and agent journals do not atomically prove ordinary UI/API saves. Its unique identity
includes dataset, caller scope and operation ID; store a versioned semantic payload
digest and minimal accepted outcome. Check current disclosure authority before replay.
Reusing an ID with different payload/target/precondition is a conflict. Do not store
secrets or whole model contexts in the journal.

The public result distinguishes rejected-with-no-effect, committed, replayed and
unconfirmed. Transport timeout or cancellation after dispatch does not imply rollback.
An owner lookup by exact operation identity resolves uncertainty without resubmission.
Retain compact deduplication evidence for the advertised replay horizon; expiring detail
must not silently make an old operation executable again. Retention and dataset lifecycle
must be implemented before advertising restart/restore-safe replay.

## Information policy

The operator selects how a run reacts to changed material inputs, separately from
how it publishes. Supported semantics are pinned computation, refresh at an explicit
safe point, bounded replan on a stale basis, or stop for review. Pinned computation
continues using the admitted snapshot; publication still requires current target,
authority and any owner-mandated material basis. It does not mean overwrite newer data.

Persist the effective policy and admitted basis with the execution/checkpoint. A live
setting change affects new admissions by default. Switching an active run requires a
new admitted policy revision at a safe point. A changed approved proposal needs a new
approval. Refresh must invalidate all dependent planning artifacts, not merely replace
one JSON field while retaining decisions derived from its old value.

Membership is material where a plan depends on a set (for example the relevant task
set). Per-row versions alone cannot detect a new member. The owning module provides
the bounded query witness and validates it atomically at publication. Nonmaterial logs,
telemetry and unrelated tasks must not invalidate the plan.

## Compatibility and rollout

Missing original preconditions on existing-object writes become explicit errors. Create
and append operations retain separate contracts. Use typed body edit stamps for the
initial API; do not present them as strong ETags for unrelated dashboard fields.
Existing 409 mappings remain unless an endpoint introduces a documented new contract.

The API-only SPA lives in the sibling CanDoItAll.SPA repository. Its current generated
serializer drops existing lifetime metadata and its editor constructs a tokenless save.
It cannot remain an accepted old writer after enforcement. No server adapter may fill
its token. Client conversion or an explicitly unsupported old-write response is a
cutover requirement; sibling changes require their own scope and validation record.

Canonical and owner EF models must agree. Use additive migration and distinct backfilled
local revisions. Imports receive new target lifetimes/revisions; imported operation and
claim records remain history. Host restart is not destructive restore. The current
canonical runtime generation is not proven to be a durable restore fence; that claim
requires a maintenance-owned activation identity outside the restored data, or an
explicit maintenance procedure that retires all previous request namespaces.

After protection is active, rollback means a compatibility-safe build or paused writes.
Running an old unconditional writer against the additive schema is unsafe. Keep
unresolved receipts and do not delete them to make rollback appear successful.

## Completion gates

Architecture review precedes implementation. Each owner closes only with its complete
enabled write perimeter, schema/contract compatibility, positive and deterministic
negative cases, authoritative commit/recovery proof, and measured costs. UI changes
require native browser interactions at 1920x1080, including two independent editors.

Build affected production projects and discover the exact test filters before execution.
Use an explicitly isolated PostgreSQL 18 fixture for persistence/race proof; InMemory
does not establish PostgreSQL atomicity. Run final portability enforcement without a
baseline write. A shared persistence/schema/public-contract change triggers one broader
stable checkpoint after the candidate is frozen, not repeated broad runs after every edit.

## External mechanics checked

EF compares original concurrency values during update/delete, while transaction
participation across contexts requires the same connection and transaction. These
mechanics support the design but do not discover business aggregate boundaries.
See [EF concurrency](https://learn.microsoft.com/en-us/ef/core/saving/concurrency)
and [EF transactions](https://learn.microsoft.com/en-us/ef/core/saving/transactions).

PostgreSQL transaction isolation and transaction-scoped advisory locks do not make an
arbitrary previously read proposal fresh. See
[transaction snapshots](https://www.postgresql.org/docs/18/sql-set-transaction.html)
and [explicit locking](https://www.postgresql.org/docs/18/explicit-locking.html).
