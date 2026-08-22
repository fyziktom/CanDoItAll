# C# Current-State Inventory

## Re-anchor and analysis evidence

- Repository: `C:\repositories\CanDoItAll`
- Execution branch: `maf-update-and-hil`
- Preparation source baseline: `5cdf1666dbafdcea975909101c1854773f5f3556`
- Re-anchor HEAD: `af425ac371b251447f9858b15476092531c686da`
- The re-anchor commit changes only bundle material; the relevant `src` and `tests` trees are identical to the preparation baseline.
- Re-anchor CodeAnalytics snapshot: `snap-20260820203442-90bdd166` (364 documents, healthy dashboard).
- Focused architecture snapshot: `snap-20260820220112-5cb38069`.
- Snapshot result: no blocking diagnostic and the scoped project graph is acyclic. Two named pre-existing non-project cycles form the non-regression ceiling: `CanDoItAll.Modules.AgentFramework <-> CanDoItAll.Modules.AgentFramework.Hosting` and the unrelated nested `ImageGenerationAgentRuntimeToolProvider <-> ImageGenerationToolBuilder` type cycle. Their count and identities must not change.
- SB01/SB02 proof resolves the entire active product and test graph to stable MAF `1.18.0` and preview `1.18.0-preview.260818.1` and makes tool invocation serial explicitly.

## Source files inspected

- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Workflows/MafWorkflowCompiler.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Workflows/MafInProcessWorkflowExecutionBackend.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Workflows/MafWorkflowEventNormalizer.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Workflows.Runtime/Runtime/WorkflowRuntimeManager.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Workflows.Runtime/Runtime/WorkflowExternalRequestRuntime.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Workflows.Runtime/Persistence/InMemoryWorkflowRunStore.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Workflows.Abstractions/Runtime/WorkflowRuntimeContracts.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Models/Workflows/WorkflowModels.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.WorkflowExecutors.Core/Runtime/WorkflowExecutorInvoker.cs`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Persistence/PersistentWorkflowStores.cs`
- `src/Modules/CanDoItAll.Modules.AgentFramework/AgentFrameworkModuleServiceCollectionExtensions.cs`
- `src/App/CanDoItAll.Web/Api/WorkflowsApi.cs`
- `src/Modules/CanDoItAll.Modules.AgentFramework/AgentTools/WorkflowAgentRuntimeToolProvider.cs`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor.cs`
- `src/Infrastructure/CanDoItAll.Infrastructure/Persistence/AppDbContext.cs`
- `src/Infrastructure/CanDoItAll.Migrations.PostgreSql`
- focused workflow, executor, adapter-isolation, lifecycle, and persistence tests under `tests/Unit` and `tests/Integration`.

## Current project ownership and references

| Project | Relevant outbound references | Current responsibility |
|---|---|---|
| `CanDoItAll.AgentFramework.Models` | Capabilities.Abstractions, Memory.Abstractions, Infrastructure.Abstractions, SharedKernel | Workflow definitions, run/request/checkpoint records, executor descriptors, and strongly typed IDs |
| `CanDoItAll.AgentFramework.Workflows.Abstractions` | Models | Workflow runtime, store, catalog, backend, and service contracts |
| `CanDoItAll.AgentFramework.WorkflowExecutors.Abstractions` | Models | Executor catalog, executor, invoker, and approval-gate contracts |
| `CanDoItAll.AgentFramework.WorkflowExecutors.Core` | Models, Executor Abstractions, Workflows.Abstractions, SharedKernel | Executor invocation, approval enforcement, retry, timeout, and side-effect policy |
| `CanDoItAll.AgentFramework.Workflows.Core` | Models, Workflows.Abstractions, Executor Core, AgentFramework.Core, SharedKernel | Catalog, launch, and runtime-neutral workflow services |
| `CanDoItAll.AgentFramework.Workflows.Runtime` | Models, Workflows.Abstractions/Core, Executor Core, Infrastructure.Abstractions, LLM runtime | Run orchestration, lifecycle transitions, in-memory stores, and legacy external-request capture |
| `CanDoItAll.AgentFramework.Workflows.MafAdapter` | workflow/executor layers and MAF SDK | MAF compilation, execution, and event translation |
| `CanDoItAll.Modules.AgentFramework` | workflow layers, MAF adapter, Executor Core, Infrastructure | Composition and AgentFramework EF entities/stores/configurations |
| `CanDoItAll.Infrastructure` | Infrastructure.Abstractions, SharedKernel, EF/Npgsql | `AppDbContext` and configuration discovery |
| `CanDoItAll.Migrations.PostgreSql` | Composition, Infrastructure | Repository migration host |

## Large classes, constructor seams, and current responsibilities

| Type/file | Approximate size | Constructor dependencies | Current responsibility and defect |
|---|---:|---:|---|
| `MafWorkflowCompiler` | 493 lines | 5 | Validates definitions, constructs all executors and edges, and embeds node execution. It has no native request-port topology, stable internal roles, or topology fingerprint. |
| `MafInProcessWorkflowExecutionBackend` | 483 lines | 6 | Compiles, runs, normalizes events, and creates run results/checkpoints. It uses non-streaming execution, catches an exception as pause, writes metadata-only checkpoints, and cannot resume. |
| `WorkflowExternalRequestRuntime` | 125 lines | static/AsyncLocal | Captures request state and turns approval into an exception. It has no immutable scoped approval authorization. |
| `WorkflowExecutorInvoker` | 243 lines | 4 | Resolves executors, enforces approval, retry, timeout, audit, and invokes side effects. Approval is discovered after topology compilation and there is no replay-dedup boundary. |
| `WorkflowRuntimeManager` | 748 lines | 6 | Starts, cancels, accepts responses, persists results, and finalizes failures. It consumes a request before backend resume and persists the resumed result sequentially, leaving a crash gap. |
| `InMemoryWorkflowRunStore` | 583 lines | 1 | Stores runs/events/requests and checkpoint metadata. It has no native checkpoint payload/index contract. |
| `PersistentWorkflowStores.cs` | 3,133 lines | multiple stores | Hosts catalog/runtime/idempotency persistence in one file; `PersistentWorkflowRunStore` has 38 members. New checkpoint, operation, lease, and dedup responsibilities must not be appended here. |

Additional affected hotspots that must not grow as the implementation mechanism are `WorkflowsApi.cs` (905 lines/19 members), `WorkflowAgentRuntimeToolProvider.cs` (556 lines), and `WorkflowsPage.razor.cs` (2,413 lines/249 members).

There are no relevant handwritten production partial classes today. New production partials are prohibited for this initiative; generated EF migration and designer partials remain allowed.

## Direct instantiation and composition points

- Production composition resolves compiler, backend, runtime manager, invoker, and stores through the existing MAF/runtime/module registration extensions.
- `MafWorkflowCompiler`, `MafInProcessWorkflowExecutionBackend`, `WorkflowRuntimeManager`, and `WorkflowExecutorInvoker` are directly instantiated across `WorkflowFoundationTests`, `WorkflowExecutorTests`, `WorkflowRuntimeLifecycleRedGateTests`, `WorkflowRuntimeExtractionTests`, `WorkflowPreviewSimulationTests`, `WorkflowCatalogTests`, `MafWorkflowEventNormalizerTests`, and focused integration tests.
- Those direct construction sites are a compatibility constraint: new constructor dependencies must have intentional defaults or all sites must be migrated visibly. Service location is not an acceptable compatibility bridge.

## Current execution paths

Human input:

`MafWorkflowCompiler -> WorkflowExternalRequestCaptureScope -> WorkflowExternalRequestPendingException -> MafInProcessWorkflowExecutionBackend catch -> metadata checkpoint`

Approval-required executor:

`MafWorkflowCompiler -> WorkflowExecutorInvoker -> WorkflowExternalRequestApprovalGate -> WorkflowExternalRequestPendingException`

External response:

`WorkflowRuntimeManager -> IWorkflowRunStore.TryAcceptExternalResponseAsync -> IWorkflowExternalResponseBackend.ResumeAsync -> sequential PersistBackendResultAsync`

The response path has a durable crash gap after `RespondedAtUtc` is committed and before recoverable continuation exists.

SB04 replaces that crash-gap path with the persistent operation/continuation boundary, but
SB05 entry inspection identifies three production mutation callers that still reach the raw
compatibility surface independently:

1. `WorkflowsApi.cs` accepts `ResponseJson` as an encoded string and calls
   `IWorkflowRuntimeManager.SubmitExternalResponseAsync`.
2. `WorkflowAgentRuntimeToolProvider.cs` accepts raw response JSON and does not carry its
   trusted agent-governance context into response submission.
3. `WorkflowsPage.razor.cs` forwards the response text area directly through
   `RespondToExternalRequestAsync`.

`WorkflowExternalResponseSubmissionCoordinator` currently synthesizes a
`Service("workflow-runtime-compatibility")` actor and an idempotency key. That bridge is not
an authorization boundary and must not remain reachable by any production caller after
SB05. Existing API run/detail projections also expose domain-owned origin, event/request/
response JSON, artifact paths, and checkpoint identifiers that require a safe Web projection.

Closure resolution: SB05 removes the production raw-manager/coordinator response surface,
routes exactly those three callers through one `IWorkflowExternalResponseService`, and
replaces public run/event/request/artifact/checkpoint/operation output with explicit
Web-owned safe projections. This section remains the pre-SB05 inventory, not the current
production path.

## Existing foundations to reuse

- `PersistentWorkflowCatalogService.GetDefinitionAsync(workflowId, versionId)` loads an exact workflow version.
- `PersistentWorkflowLaunchIdempotencyStore` demonstrates unique insert, fingerprint conflict, lease expiry/takeover, CAS completion, and replay.
- LLM chat-operation persistence demonstrates row locking, concurrency tokens, owner/epoch, and bounded leases.
- `WorkflowExecutorDescriptor.SideEffects` identifies external writes, retry capability, required commit idempotency, and receipt policy.
- `AppDbContextModelRegistry` lets module-owned configurations participate without an Infrastructure-to-module reference.
- `IWorkflowRunStore.TryTransitionRunAsync` already combines a run-state CAS and lifecycle event atomically.

## Current tests and missing proof

Existing characterization coverage includes `WorkflowFoundationTests`, `MafWorkflowAdapterIsolationTests`, `MafWorkflowEventNormalizerTests`, `WorkflowRuntimeLifecycleRedGateTests`, `WorkflowExecutorTests`, and `WorkflowRuntimePersistenceLifecycleTests`.

Missing proof:

- real MAF request/checkpoint/rehydration;
- disposed first run followed by a fresh resume run;
- native request/checkpoint correlation in both event orders;
- topology and format compatibility rejection;
- durable response-operation CAS, leases, and crash recovery;
- executor side-effect replay deduplication;
- migrations applied to a real PostgreSQL database;
- direct unit tests for each extracted responsibility;
- an architecture negative that rejects shallow partial/helper extraction.
- one authorized response facade used by all three production mutation callers;
- fail-closed reconstruction of an authorization grant during background recovery;
- safe API run, pending-request, and operation projections that cannot serialize native
  checkpoint identity or governed payloads.

## Risk notes

- Adding more behavior to the compiler, backend, manager, or persistent-store file would deepen existing god classes.
- Cross-boundary records placed above Workflows.Abstractions could create a `Workflows.Core <-> WorkflowExecutors.Core` cycle; neutral values belong in Models or Workflows.Abstractions.
- Runtime must never reference or cast to the concrete MAF adapter.
- Neutral contracts must not reference MAF, EF, Npgsql, ASP.NET, or API DTO types.
- Application request IDs and MAF session/request/port/checkpoint IDs are distinct identities and must not be conflated.
- A host-local active-run registry is not a multi-host claim.
- Existing metadata checkpoints remain legacy inspection evidence, not resumable payloads.

## Partial-class policy

Do not split `MafWorkflowCompiler`, `MafInProcessWorkflowExecutionBackend`, `WorkflowRuntimeManager`, or any persistent store into partial files. Extract independently constructible top-level classes with direct tests. Generated EF migration/designer partials are the only allowed exception.
