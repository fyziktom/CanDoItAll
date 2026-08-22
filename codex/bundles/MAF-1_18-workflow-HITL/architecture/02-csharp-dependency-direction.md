# C# Dependency Direction

## Current relevant graph

```text
Models
  ^
Workflows.Abstractions       WorkflowExecutors.Abstractions
  ^                                      ^
Workflows.Core <------- WorkflowExecutors.Core
  ^
Workflows.Runtime
  ^
Workflows.MafAdapter (+ MAF SDK)
  ^
Modules.AgentFramework (+ EF/Npgsql) -> Infrastructure
```

The complete inspected reference set is recorded in `00-csharp-current-state-inventory.md`. `AppDbContext` discovers module configurations through `AppDbContextModelRegistry`; Infrastructure does not reference the module.

## Target graph and dependency inversion

No new production `ProjectReference` is expected. Neutral records and ports extend existing lower layers, while implementations remain in Runtime, MafAdapter, Executor Core, or Modules.AgentFramework.

```text
WorkflowExternalResponseService
  -> IWorkflowExternalRequestAuthorizer
  -> IWorkflowExternalResponseValidator
  -> IWorkflowExternalResponseOperationStore
  -> IWorkflowResumeBoundaryStore
  -> IWorkflowCatalogService
  -> IWorkflowExternalResponseBackend

WorkflowExternalResponseAuthorizationGrantFactory
  -> persisted operation actor / AcceptedAtUtc / protected payload
  -> persisted boundary scope / lifetime / policy fingerprint / response contract

Web response adapter + safe projection mapper
  -> IWorkflowExternalResponseService
  -> trusted Web actor-context resolver

Blazor page adapter + agent-tool adapter
  -> IWorkflowExternalResponseService
  -> trusted page/agent actor-context resolver

MafWorkflowExternalResponseDriver
  -> IWorkflowBackendCheckpointPayloadStore
  -> Microsoft.Agents.AI.Workflows

WorkflowExecutorInvoker
  -> IWorkflowExecutorInvocationDeduplicationStore
  -> IWorkflowExecutor
```

Persistent implementations satisfy lower-layer ports from the existing composition root.
The SB05 facade, authorizer, validator, and grant factory use the existing projects and
ports. Scope/lifetime/policy data evolves inside existing `OriginJson` and
`AuthorizationPolicyJson`; existing operation columns supply actor, accepted time,
fingerprint, and protected payload. No project or relational schema edge is added.

## Allowed dependencies

- Models may use BCL JSON and cryptographic primitives, but no MAF, EF, ASP.NET, or API types.
- Workflows.Abstractions may use Models for HITL contracts.
- Workflows.Core may implement pure state/topology policy over Models/Abstractions.
- WorkflowExecutors.Core may consume workflow-owned approval/dedup ports from Workflows.Abstractions.
- Runtime may coordinate neutral ports and backend contracts.
- MafAdapter may consume neutral contracts and the MAF SDK.
- Modules.AgentFramework may implement neutral ports using EF/PostgreSQL and register them.
- The migration host may discover module configurations through existing composition.
- Web may bind ASP.NET principal/body/header values into neutral commands and map neutral
  results into safe HTTP DTOs; it does not own authorization or continuation policy.
- Module-owned Blazor and agent-tool adapters may create trusted caller context from the
  authenticated session or agent governance snapshot and call the neutral facade.

## Forbidden references

- Models or a workflow-neutral project to the MAF SDK.
- Models/Abstractions/Core/Runtime to EF Core or Npgsql.
- Runtime/Core/Abstractions to MafAdapter.
- MafAdapter to Modules.AgentFramework, Web, or EF entities.
- WorkflowExecutors.Abstractions to Workflows.Core, Runtime, or MafAdapter.
- Infrastructure to Modules.AgentFramework.
- Persistence entities to MAF request/checkpoint types.
- Web/API DTOs to MAF or EF types.
- Workflows.Abstractions/Core/Runtime to ASP.NET claims, `HttpContext`, or Web DTO/result types.
- Web, page, or agent-tool callers to raw runtime-manager response submission or the
  compatibility submission coordinator.
- Any new project-reference cycle.
- A new partial class used to disguise retained responsibility coupling.

Approval authorization and invocation-key records therefore live in Models or Workflows.Abstractions. Placing them in Workflows.Core would invite a `Workflows.Core <-> WorkflowExecutors.Core` cycle.

The concrete persisted-scope authorizer belongs in Modules.AgentFramework because it can use
canonical profile and workspace services already owned there. Its neutral decision/context
contract remains in Workflows.Abstractions. Web owns only principal extraction and mapping;
it must not become a second authorization implementation. The repository lacks an
authoritative per-user project-membership ACL, so no dependency or claim is invented for one.
Unavailable trusted assignment evidence is a fail-closed decision.

## Cycle risk and contract-project decision

Focused CodeAnalytics snapshot `snap-20260820220112-5cb38069` reports an acyclic scoped project graph. Two named pre-existing non-project cycles form the baseline ceiling: `CanDoItAll.Modules.AgentFramework <-> CanDoItAll.Modules.AgentFramework.Hosting` and `ImageGenerationAgentRuntimeToolProvider <-> ImageGenerationToolBuilder`. Their count and identities must remain unchanged. No new contract project is needed. If a project cycle appears, the first remedy is relocating a neutral record/port downward, not adding a bridge assembly or service locator.

## Before-change proof

- Snapshot and dashboard result are recorded above and in the inventory.
- Existing `.csproj` edges were inspected for Models, workflow layers, executor layers, MafAdapter, Modules.AgentFramework, Infrastructure, Web, and the migration host.
- Source scans establish the current MAF SDK containment in MAF-owned projects.
- Inventory records the current sizes and responsibilities of the compiler, backend, manager, invoker, and persistent-store cluster.

## Required after-change build and test proof

After SB03, again after SB04, and at CP-WB3 after SB05:

- capture a fresh CodeAnalytics snapshot and prove no new cycle relative to `snap-20260820220112-5cb38069`, no project-level cycle, and the two named baseline cycles unchanged;
- diff all affected `.csproj` files and prove no unplanned reference edge;
- assert no MAF type in Models, Workflows.Abstractions/Core/Runtime, or executor contracts;
- assert no EF/Npgsql type outside persistence/infrastructure;
- assert no new production partial type;
- prove compiler/backend/manager/invoker delegate to directly tested focused types and have not grown into larger clusters;
- build affected production projects in Release using the frozen Components/FileTools roots;
- build and run the declared focused tests with exact expected and actual discovery counts;
- prove persistent implementations are reached through neutral ports;
- prove SB04/SB05 can consume stable contracts without a reference to MafAdapter internals.
- prove all three production response callers reference the common facade and no longer
  reference raw manager/coordinator response mutation;
- prove safe Web run/request/operation projections do not reference domain checkpoint,
  protected payload, origin, artifact-path, or persistence types;
- prove background recovery reconstructs and validates authorization through neutral
  operation/boundary data before backend/executor invocation;
- diff EF entities/configurations/model snapshot and prove SB05 made no relational schema
  change and generated no migration.

Any violation blocks the active subbundle and its dependent work.
