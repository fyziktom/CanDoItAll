# C# Boundary Map

Execution clarification: [SB04 concrete placement and edges](11-capture-implementation-decisions.md) amends the preliminary recorder/typed-adapter and batch ownership assumptions below.

## Project Roles

The three projects below are proposed, not already created. All live beneath
`src/MAF/ProviderHistory` and use existing framework/package versions.

| Owner | Owns | Must not own |
|---|---|---|
| CanDoItAll.AgentFramework.ProviderHistory.Abstractions | Typed IDs, stable partition and transient fence, capture/query/detail/policy DTOs and ports, source kind and price/coverage states. BCL-only. | EF, ASP.NET claims, SDK client types, concrete producer models, UI, file storage, provider configuration. |
| CanDoItAll.AgentFramework.ProviderHistory.Application | Capture state orchestration, validated policy, bounded query/detail orchestration, source adapter selection, retention eligibility and typed failure decisions. | HTTP execution, concrete canonical source stores, SQL/migrations, transcript parsing, bearer validation or rendered UI. |
| CanDoItAll.AgentFramework.ProviderHistory.Persistence | AppDbContext mappings, scalar index query/write, optimistic concurrency, policy record, same-context outbox staging, leased projection/backfill/cleanup scheduling and protected detail. | Provider invocation, identity-provider logic, source transcript ownership or UI. |
| Existing canonical persistence owners | Commit their own evidence, publish metadata change/delete intent, resolve an exact authorized source reference. | A second copy of transcript or a general scan during user search. |
| Existing provider/SDK adapters | Produce typed per-attempt observations, frozen price/caller/owner context and terminal evidence. | Search, retention, arbitrary payload reflection or altered retries. |
| Web host | Validate existing identity, map trusted caller snapshot, enforce interactive/resource policy, capture/recheck profile fence. | New per-person IDM inference, trusting access-context as permission, exposing raw credentials. |
| AgentFramework UI module | Reusable history panel/controller and two host scopes; provider form separation. | EF/source-file access, inferred authorization, provider calls while searching. |
| Workspace UI module | General history-policy editor on Settings; neutral policy/authorization ports. | Reference back to AgentFramework UI or a second policy service. |
| Composition + migration host | Register neutral services, existing owner adapters, protection/authorization, workers and EF configuration assembly. | Implement feature behavior in the composition root. |

Three projects are justified by two real boundaries: producer code must depend on contracts
without EF/policy implementations, and application orchestration must be tested without
a database. One large project would force every SDK/producer consumer to carry persistence;
one project per source would duplicate plumbing. Existing sources stay in their current
assemblies rather than being moved just to make the diagram symmetrical.

## Small Collaborators And Ports

Names are proposed. Final public names are locked in SB01; their roles and dependency
direction are fixed here.

| Contract / collaborator | Consumer and implementation owner | Test seam |
|---|---|---|
| IProviderHistoryRecorder | Typed invocation adapters consume; Application implements against a durable write port. Begin returns a typed attempt reservation; terminal transitions are idempotent. | Fake writer + TimeProvider; fail-before-dispatch and no replay on terminal write failure. |
| IProviderRequestHistoryQueryService | Both UI scopes consume; Application validates/authorizes; Persistence performs one bounded scalar query. | Reader spy + policy; SQL integration verifies predicates and Take before materialization. |
| IProviderHistoryDetailService | Explicit UI action; Application selects an owner adapter or protected history detail. | Denied/missing/expired owners and before-publish authority changes. |
| IProviderHistoryPolicyService | Workspace settings consumes; Application validates and Persistence versions the partition policy. | Invalid duration/quota and stale edit reject explicitly. |
| IProviderHistoryAccessPolicy / trusted host context | Application calls; Web supplies validated principal, resource policy and generation checks. | No HttpContext in core tests; missing authority denies. |
| ICanonicalProviderHistorySource | Existing agent/chat/workflow/relay owners implement typed metadata/detail/reconciliation operations. Registration is keyed by a closed source kind; duplicate registration fails. | Exact ID resolution and resource authorization; no all-owner search. |
| History read/write/policy ports | Application depends on focused contracts; Persistence implements existing EF transactions. | Explicit database boundary, not an interface around a trivial pure helper. |
| SharedProviderExecutionPricingResolver | ProviderManagement freezes known price/model/currency before dispatch. | Pure tariff/usage fixture; no catalog call during finalization/search. |
| SharedProviderInvocationAuditFinalizer | Existing finalizer extracted into one top-level file. | Buffered/streaming/cancel monotonic terminal behavior. |
| ProviderHistoryQueryController | AgentFramework UI state only: draft/applied filters, cursors, cancellation and stale-generation discard. | Component/controller tests without a provider or database. |
| ProviderProfileEditorForm | Existing mutation EditContext and footer shared only by editable provider panes. Sharing/History are outside. | Search Enter/click causes zero provider saves. |

Pure validation/calculation stays in small concrete functions/classes. Do not invent an
interface for each calculation. Avoid a service locator: composition supplies the finite
source adapter collection and validates its source keys once.

## Source And Integration Ownership

- Relay stays canonical in ProviderManagement. Its own small caller snapshot in
  SharedProviders.Abstractions preserves that project's independence; an outer adapter maps
  it to neutral history identity. Web extracts validated managed jti and issuer/subject.
- SimpleChats.Persistence stages outbox rows with its actual AppDbContext. Its invocation
  row can reference several actual attempts; it is not also an extra charged legacy row.
- AgentFramework.Persistence owns file evidence and its durable metadata mutation journal.
  Its journal/projector collaborator is top-level; no extra file-store partial.
- PersistentWorkflowUsageObservationStore retains workflow ownership and adds source
  version/intent hooks in its current module. Shared observation ID means another owner
  link, not another attempt.
- Llm.ProviderRuntime, Maf and Voice own their actual typed runtime adapters. Batch uses
  the existing balancer/item adapter. Provider handles carry typed context where needed
  but do not inspect arbitrary TResult/object payloads.
- Detailed input is bounded and shared only within a logical operation/input revision.
  Responses remain per attempt. Canonical content is never copied by an owner adapter.

## Composition And Lifecycle

A small top-level history service-collection extension registers the new services. The
existing host composition calls it once and registers concrete source, identity and
protection adapters in the outer host. Persistence supplies an EF-only same-context staging
helper that accepts the owner's actual AppDbContext; neutral ports never expose that type.

Register the history EF configuration assembly via the existing composition ModuleAssemblies /
AppDbContextModelRegistry path and in the PostgreSQL migration model. No reverse reference
from Infrastructure is needed. Validate host startup with the production composition path,
not a hand-built test-only registration list.

Workers obtain an existing database-profile lease/fence per bounded batch. They do not
retain scoped DbContexts, current principals or provider secrets in singleton state. No
worker invokes a model. Profile switching cannot redirect old writes to a new active DB.
