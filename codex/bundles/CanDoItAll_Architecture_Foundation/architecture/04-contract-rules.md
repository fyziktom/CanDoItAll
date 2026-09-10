# 4. Contract semantics and dependency direction

## Two legitimate ownership directions

A **published API** belongs to the provider: Agents catalog/commands, CRM Party/Staffing, or Structure graph/contributions. The owner defines fields, invariants, errors, and compatibility. A **required port** may belong to the consumer: CRM's narrow AI-resource requirement can be implemented by an adapter using Agents catalog. That port does not own technical agents. Likewise, Structure owns the contributor extension point, not its sources.

```text
Crm.Application              -> Crm.Ports
Crm.AgentsIntegration        -> Crm.Ports + Agents.Contracts
Agents.Application           -> Agents.Contracts
Crm.AgentTools               -> Crm.Contracts + neutral Tooling contracts
Hr.SimpleChatsIntegration    -> SimpleChats application/contracts + neutral Tooling
Workflow.CrmIntegration      -> Workflow executor port + Crm.Contracts
Process.CrmIntegration       -> Process step port + Crm.Contracts
Composition                  -> concrete implementations and registrations
Presentation                 -> presentation models + scoped application contracts
```

These describe responsibility, not a mandatory new project for every line. Existing integration/composition projects can initially hold adapters if no cycle results. Reusable UI must not transitively import persistence/runtime through a feature-hosting package. A complete-feature registration package is not a presentation library.

## Types that cannot cross a public domain boundary

Do not export EF entities, DbContext/DbTransaction, IQueryable, IServiceProvider, tracked graphs, Razor components, live SDK runtime objects, or resolved secrets. An IReadOnlyList of foreign EF entities is still a leak. JSON extension metadata needs a bounded, versioned owner schema, not an open persistence/configuration bag.

## Shared semantic envelope C0

Reuse compatible existing DTOs; this is not a required base class.

| Concern | Required meaning |
|---|---|
| Context | Trusted boundary resolves actor, delegation, purpose, profile/incarnation, and domain scope. Payload-supplied claims are validated, not trusted. |
| Identity | Stable owner references; preserve current NodeKey formats. Names and paths are not identities. |
| Revision | Require relevant expected versions. An independent note need not require a global graph version; graph invariants still need a coherent check. |
| Operation | New retry-safe mutations use scoped stable intent identity and normalized semantic fingerprint. Do not retroactively label a non-idempotent API safe. |
| Result | Distinguish committed/admitted work, identities/revisions, required/optional effects, and uncertainty. No bare boolean/message substitute. |
| Diagnostics | Stable code, safe text, correlation, permissible current revision, and safe retry class; no raw credential/PII exceptions. |
| Compatibility | Version durable payloads; preserve historical defaults and reject unknown destructive values. |
| Cancellation | Cancel pre-admission work where possible; after admission query/control the durable operation. Lost waiting is not rollback. |

## Results are not one compulsory enum

A synchronous owner command distinguishes relevant validation, denial, not-found, conflict, unavailable capability, and confirmed commit. Long-running admission distinguishes rejected from durable Accepted/Existing. An observer may be Unconfirmed after losing acknowledgement; the owner state is queried using the same operation identity. A batch may intentionally report per-part partial results.

Committed-with-pending-projection means original data was saved. ExecutionSucceeded plus WritebackBlocked does not mean not started. Closing a window does not confirm cancellation. Preserve these distinctions through existing UI, HTTP, and tool receipt shapes.

## Query contract

Define scope, deterministic order, pagination, limits, coverage/freshness, and field sensitivity. Batch queries use explicit IDs; an absent/denied result is not evidence of a complete empty catalog. Snapshot and canonical current sources are explicit. Queries do not create native objects, price lists, or runtime work. Move existing read-side reconciliation into a named refresh/projector without removing the delivered update behavior.

Read access does not automatically permit provider disclosure or republication into a task/thread [architecture 16]. Large content is a separate bounded access operation, not a hidden detail field in every search.

## Events and dependency graph

Contract packages never depend on implementations. Logical cooperation in both directions need not create contract cycles: exchange minimal stable references rather than whole aggregates. Do not expand SharedKernel into an all-domain schema union. Source owner publishes its event schema; neutral envelopes contain identity, not every module's data model.

Events announce committed facts; commands request owner action and have an outcome; transient UI notifications request rereading and grant no rights. Receiving an event does not authorize writing the source master. Product-owned policy remains outside neutral MAF machinery.

## Declaration, implementation, and data authority

The catalog separates contract_owner, caller_modules, implementation_modules, extension_implementers, and data_authority. A consumer-owned source port is not the source-data owner. Composition wires adapters but does not acquire domain data.

CON-044 context capture belongs to the Agents application adapter, not neutral conversation components. CON-046 coordinates project lifecycle; CON-056 coordinates database transfer. CON-031/054/055 preserve separate Process/Agent/Workflow result ownership. Executor extension ports CON-072/073 own execution integration requirements, not the target entities they change.

[catalogs/contracts.json](../catalogs/contracts.json) is a semantic inventory, **not a list of mandatory new interfaces or enabled tools**. Target caller lists describe potential legitimate integrations. Each installed surface must separately demonstrate registration, purpose, authorization, invocation policy, actual handler, and tests [SRC-028].

## Conceptual typed operation shape

```csharp
// Conceptual shape only; retain an existing compatible owner API where possible.
public interface IStructureContributions
{
    Task<ContributionOutcome> EnsureAsync(
        ContributionRequest request,
        ResolvedOperationAuthority authority,
        CancellationToken cancellationToken);
}
```

Authority here is an internally verified capability context, not an editor field deserialized from arbitrary JSON. Generalize its safety semantics to other owner commands, not the Structure business API itself. A descriptor may advertise a command; it cannot grant it or replace the typed handler.
