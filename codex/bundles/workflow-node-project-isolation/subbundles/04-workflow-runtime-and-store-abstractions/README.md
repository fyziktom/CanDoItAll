# SB04 - Workflow Runtime And Store Abstractions

## Status

- `Completed`

## Objective

Extract workflow runtime, run lifecycle, checkpoint, artifact content, event, backend catalog, external request, and persistence-facing contracts into workflow-owned runtime/store projects so execution state is not anchored in MAF or catch-all Core.

## Success Criteria

- Runtime contracts and implementations are separated from persistence-specific storage.
- Workflow run lifecycle behavior, checkpoint writes, artifact content handling, event payloads, external request handling, and backend catalog selection remain compatible.
- Runtime, store, artifact, checkpoint, external request, cancellation, and failure event paths carry the typed diagnostic envelope when applicable.
- Existing in-memory/runtime store behavior is covered by focused tests before persistence adoption.
- Service registration is explicit and dependency-directed.

## Covered Inputs

- R05, R06, R11, R13, R14, R15, R17.
- Architect note that workflows and nodes need own projects for maintainability and testability.

## Prerequisites

- SB01 accepted.
- SB02 completed.
- SB03 can be developed in parallel only if shared contracts are stable; final SB04 proof must be taken after SB03 interfaces are settled.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Workflows.Runtime\WorkflowRuntimeManager.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Workflows.Runtime\WorkflowExternalRequestRuntime.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Workflows.Runtime\WorkflowArtifactContentStores.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Workflows.Runtime\WorkflowEventPayloads.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Workflows.Runtime\WorkflowNodeExecutionProgress.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Workflows.MafAdapter\MafInProcessWorkflowExecutionBackend.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Hosting\AgentFrameworkServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence`

## Deliverables

- `CanDoItAll.AgentFramework.Workflows.Runtime` for runtime manager, run lifecycle services, event payload abstractions, typed failure event payloads, backend catalog contracts, external request runtime, and checkpoint/artifact contracts.
- `CanDoItAll.AgentFramework.Workflows.Persistence` for persistence-specific workflow stores when current persistence code is moved.
- Explicit registration extensions for runtime and store services.
- Tests for lifecycle, checkpoint/artifact content, external request, cancellation, failure diagnostics, partial persistence behavior, and backend selection behavior.

## Dependency Impact

- Executor invokers in SB06, MAF adapter in SB11, API/UI adoption in SB12, and final regression in SB14 depend on stable runtime contracts. Weak proof here can break run status, event feeds, artifacts, approvals, and process integration.

## Validation Depth

- `Critical foundation`
- Unit, integration, service-composition, and persistence-boundary proof.

## Implementation Steps

1. Identify runtime contracts that belong in abstractions versus runtime implementation.
2. Move runtime manager and lifecycle services into workflow runtime project with minimal behavioral changes.
3. Keep persistence-backed stores behind explicit interfaces and move only when dependency direction is clean.
4. Add tests for successful run, failure run, cancellation, checkpoint recording, artifact content, external request state, store failure, artifact write failure, checkpoint persistence failure, and backend catalog selection.
5. Verify event payload names, serialized shapes, and typed failure payload compatibility remain compatible.
6. Update hosting registrations to compose runtime services through workflow runtime extension methods.
7. Update inventories, traceability, and proof.

## Scope Exceptions

- The MAF backend implementation is isolated in SB11 after executor/template boundaries are stable.
- UI event rendering and API adoption are SB12.

## Do Not Do

- Do not make runtime services depend on concrete MAF executors.
- Do not hide missing store failures behind in-memory fallback.
- Do not change persistence schema without explicit migration analysis.
- Do not publish generic failure events without diagnostic payload context when the runtime knows run, node, backend, artifact, checkpoint, or operation details.

## Acceptance Checklist

- [x] Runtime project compiles with allowed dependencies only.
- [x] Store abstractions are explicit and testable.
- [x] Lifecycle, cancellation, failure, checkpoint, artifact, and external request tests pass.
- [x] Event payload and typed failure payload compatibility is asserted.
- [x] No MAF-owned runtime service remains registered as the primary workflow runtime.

## Execution Notes

- Added `CanDoItAll.AgentFramework.Workflows.Runtime` for runtime/store contracts, runtime manager, in-memory runtime store, artifact content stores, event payload helpers, external request runtime support, node execution progress scope, runtime diagnostics, and runtime DI registration.
- Moved `WorkflowContracts.cs`, `WorkflowRuntimeManager.cs`, `WorkflowExternalRequestRuntime.cs`, `WorkflowArtifactContentStores.cs`, `WorkflowEventPayloads.cs`, and `WorkflowNodeExecutionProgress.cs` out of `CanDoItAll.AgentFramework.Core\Workflows`.
- Added `WorkflowRuntimeServiceCollectionExtensions` with `AddWorkflowRuntimeServices()`, `AddInMemoryWorkflowRuntimeStores(...)`, and `AddFileWorkflowArtifactContentStore(...)`.
- Updated Hosting to compose runtime services and in-memory stores through runtime extensions.
- Updated `CanDoItAll.Modules.AgentFramework` to compose runtime services through the runtime extension while keeping `PersistentWorkflowRunStore` registered in the module because it is still tied to the module persistence DbContext.
- Added `WorkflowRuntimeFailureDiagnosticMapper` and wired typed diagnostics into unregistered/durable-backend start failures, cancellation events, and approval-denied failure events.
- Preserved existing namespace `CanDoItAll.AgentFramework.Core` and a temporary runtime project reference to `CanDoItAll.AgentFramework.Core` because executor approval/redaction contracts move in SB06.
- Added explicit runtime project references to direct runtime consumers: Workflows.Core, MAF, Hosting, Persistence, AgentFramework module, SchedulerPlanner, Workbench, plugin consumers, and tests.
- Added focused SB04 coverage in `tests/CanDoItAll.Tests.Unit/WorkflowRuntimeExtractionTests.cs`.

## Proof Required

- `proof/SB04/manifest.md` with changed file hashes, build/test transcripts, store migration notes, and service registration proof.
- `proof/SB04/semantic-invariants.md` covering run lifecycle compatibility, event payload compatibility, explicit store failure, and no hidden fallback.
- Semantic Adequacy Gate proof including adversarial store/backend failures, positive run lifecycle proof, and anti-stub audit.

## Browser Validation Logging

- `N/A`. Browser-visible event rendering is validated later through SB12 and SB14.

## Progression Gate

- SB05 cannot start until runtime and store extraction proof is complete. SB06 cannot begin until SB05 confirms the workflow foundation is clean.

## Suggested Agent Prompt

```text
Implement SB04 only. Extract workflow runtime and store boundaries into workflow-owned projects, preserve lifecycle/event/artifact behavior, add tests and service registration proof, and capture Semantic Adequacy Gate evidence. Do not isolate MAF backend or UI/API adoption in this subbundle.
```
