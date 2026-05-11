# Phase 1 Architecture Review

## Status

- `Passed for downstream UI/API work`
- `Production durability gate remains open for subbundles 07 and 08`

## Scope

- Reviewed subbundle 01 wrapper/model work and subbundle 02 runtime-manager work after implementation.
- Verified the result against the durable workflow article guidance captured in `analysis/03-article-and-performance-review.md`.
- Focused on whether later catalog, UI, canvas, process, and app-integration work can safely build on the foundation.

## Reviewed Evidence

- `src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowModels.cs`
- `src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowContracts.cs`
- `src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowDefinitionValidator.cs`
- `src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowRuntimeManager.cs`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowCompiler.cs`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafInProcessWorkflowExecutionBackend.cs`
- `src/CanDoItAll.AgentFramework.Hosting/AgentFrameworkServiceCollectionExtensions.cs`
- `tests/CanDoItAll.Tests.Unit/WorkflowFoundationTests.cs`

## Decisions

| Topic | Decision | Rationale |
| --- | --- | --- |
| Workflow domain ownership | Accepted | Workflow definitions, typed identifiers, graph nodes/edges, LLM components, runtime policies, events, artifacts, and external requests are product models in `CanDoItAll.AgentFramework.Models`, not raw MAF types. |
| MAF boundary | Accepted | `CanDoItAll.AgentFramework.Core` exposes product contracts and has no MAF runtime dependency. `CanDoItAll.AgentFramework.Maf` owns compilation/execution mapping. |
| Runtime wrapper | Accepted | MAF provides execution primitives, but CanDoItAll still needs product run state, observations, artifacts, external requests, audit/authorization hooks, process references, and explicit backend policy above MAF. |
| In-process backend | Accepted with policy | The in-process MAF backend is suitable for tests, previews, local development, and short non-durable runs. It must not become the default production/long-running backend. |
| Durable backend | Deferred, not rejected | The article and MAF source point to DurableTask/DTS as the production direction. Current code has abstractions ready, but no product DurableTask host/client registration exists yet. Subbundle 07 must wire or explicitly block it with stronger evidence. |
| Product persistence | Deferred risk | Runtime state currently uses singleton in-memory stores. That is acceptable for the foundation and UI preview proof, but production/process integration cannot close until workflow definitions/runs/events/requests/artifacts are persisted or a deliberate storage exception is approved. |
| Human-in-loop model | Accepted as foundation | Product external-request records and response APIs exist. Durable RequestPort/status endpoint alignment remains for subbundle 07. |
| Concurrency | Accepted for foundation | Runtime manager assigns run ids per run, the in-memory store uses concurrent collections, and MAF preview compilation creates run-local workflow execution. Durable concurrency must be re-proven when the durable backend is added. |

## Blocking Findings

- No blocker for continuing to subbundles 03, 04, and 05.
- Production closure is blocked until subbundle 07 or 08 proves DurableTask/DTS registration and persistent product workflow storage, or records a reviewed alternative. This is not optional because the architecture requires restart-resilient, observable workflow runs for process integration.

## Required Follow-Ups

- Subbundle 07 must configure the selected durable host path using `ConfigureDurableOptions` when agents and workflows are hosted together, or document why a workflow-only host/other boundary is correct.
- Subbundle 07 must decide whether Azure Functions generated run/status/RequestPort/MCP endpoints are used, wrapped, or rejected behind CanDoItAll product APIs.
- Before subbundle 08 closure, workflow catalog/run/event/external-request/artifact state must be persistent enough for restart-resilient product behavior. If this remains in memory, final closure must fail.
- Durable backend implementation must repeat the performance review for serialization, event/status polling, streaming, external-request response, and orchestration replay safety.

## Gate Result

- Phase 1 foundation is clean enough for workflow catalog, page, and canvas work because the model/runtime abstractions are typed and isolated.
- Phase 1 is not production-complete. The durable/persistence items above remain tracked as explicit downstream gates rather than hidden assumptions.
