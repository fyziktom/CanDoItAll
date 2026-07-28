# Memory Provider API And Cognitive Memory Ownership

## Decision

`CanDoItAll` owns the provider-neutral Memory host integration:

- provider profiles and selection;
- provider protocol contracts;
- dispatch, ledgers, workers, and source gateways;
- isolated HTTP, MCP, mock, and Cognitive Memory remote-provider adapters;
- the `/memory` operator UI;
- the `/api/memory-providers` host-management API.

`CanDoItAll.CognitiveMemory` owns the native Cognitive Memory domain, persistence,
runtime, workers, management UI, deployment, and its authenticated `/memory` provider
API. The main host does not expose a Cognitive Memory route family and has no
compile-time dependency on the native repository.

Both products are work in progress. The main-host Memory provider module and its HTTP
API are experimental, and the standalone Cognitive Memory repository is not yet
published as a supported product.

## Responsibility Inventory

| Responsibility | Previous owner | Owner after this change |
| --- | --- | --- |
| Native Cognitive Memory implementation and UI | Duplicate legacy module in `CanDoItAll`, plus the standalone repository | `CanDoItAll.CognitiveMemory` only |
| Native Cognitive Memory HTTP operations | Standalone `/memory` API plus a retired main-host shim | Standalone `/memory` API only |
| Provider profile management | Main-host `/memory` UI only | Main-host `/memory` UI and `/api/memory-providers` |
| Provider context query | Generic Memory application services | Unchanged; the new API is a thin adapter |
| Provider operation status | Generic Memory application services | Unchanged; the new API is a thin adapter |
| Provider-specific transport details | Isolated driver projects | Unchanged |
| Legacy main-database export | Main-host migration compatibility infrastructure | Unchanged until a separate data-migration decision |

The legacy PostgreSQL migration identifiers remain historical reconciliation metadata.
They are not native runtime ownership.

## Boundary And Dependency Direction

```text
CanDoItAll.Web API
  -> CanDoItAll.Modules.Memory management validation
  -> CanDoItAll.Memory.Application
  -> CanDoItAll.Memory.Abstractions

CanDoItAll.Memory.Http / Mcp / Mock / Drivers.CognitiveMemory
  -> CanDoItAll.Memory.Application + Abstractions

CanDoItAll.CognitiveMemory.Service
  -> standalone Cognitive Memory Application/Runtime/Contracts
```

The main host and the standalone service communicate only through the versioned Memory
protocol over an explicitly configured provider transport. Neither repository references
the other's native implementation projects.

## API Scope

The experimental main-host API uses `/api/memory-providers` and exposes:

- list provider profiles;
- read one provider profile;
- create or replace one provider profile using the same validated management model as
  the operator UI;
- execute a synchronous or supported asynchronous context query against an explicit
  provider;
- read an operation owned by the same API subject that created it.

The API does not expose provider ingestion, feedback, event acknowledgement, operation
cancellation, or native Cognitive Memory management. Although provider-neutral ports
exist for future work, the currently shipped drivers do not have complete authorized and
durable execution paths for those operations. Publishing routes before those paths exist
would create a false contract.

Profile writes store credential references such as environment-variable names, never
secret values. Query and status operations use `MemoryOperationCaller.ApiEndpoint`, and
the authenticated token subject becomes the durable requester id. With local
authorization disabled, the requester is the explicit local API identity.

## Pattern Selection

The selected pattern is an HTTP adapter over existing Memory application contracts.

- A second Memory runtime or API-specific provider registry was rejected because it
  would duplicate selection, dispatch, ledger, and ownership rules.
- Direct calls from the API to HTTP, MCP, or Cognitive Memory clients were rejected
  because they would bypass the driver catalog and durable operation ledger.
- Reusing UI action methods for query/status was rejected because it would persist the
  wrong `UiAction` caller and requester identity.
- A new project was rejected because the API adapter has no independent SDK or lifecycle;
  the existing Web feature folder is the narrowest real boundary.
- The isolated Cognitive Memory remote driver remains justified as an adapter. It maps
  provider-specific configuration onto the generic protocol without importing native
  implementation code.

## Testability And Acceptance

The change is complete only when:

1. no main-host route or OpenAPI tag contains `/api/cognitive-memory`;
2. the legacy `CanDoItAll.Modules.CognitiveMemory` project and its native tests no longer
   compile in the main solution;
3. generic Memory and the isolated remote-provider adapter remain buildable and tested;
4. provider profile list/get/save routes work through the real profile store;
5. invalid profile ids, unsafe transport configuration, raw credential migration, and
   unsupported capability claims fail explicitly;
6. query dispatch uses `MemoryOperationCallerKind.ApiEndpoint`;
7. operation status denies a different API subject;
8. OpenAPI describes only the implemented provider operations;
9. the standalone repository remains independently buildable and documents its WIP
   status and canonical API ownership;
10. CodeAnalytics and direct project inspection show no new inward dependency or cycle.

The composition smoke is the rebuilt main host plus live OpenAPI and provider-route
requests. The standalone service build is the independent ownership smoke.
