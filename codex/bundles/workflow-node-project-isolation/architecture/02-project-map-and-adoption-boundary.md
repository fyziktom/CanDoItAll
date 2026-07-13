# Project Map And Adoption Boundary

## Base-Up Dependency Rule

Implementation must build from contracts to adoption:

1. Models and compatibility constants.
2. Workflow abstractions and builders.
3. Workflow core services.
4. Workflow runtime/store abstractions.
5. Foundation hardening.
6. Executor abstractions/helpers.
7. Default executor implementation categories.
8. Plugin executor adapters.
9. Executor hardening.
10. Template loader/materializer.
11. MAF adapter.
12. API/UI/Workbench adoption.
13. Adoption hardening.
14. Final regression and cleanup.

No downstream adoption phase may start until the prior critical/hardening gates pass.

## Current To Target Mapping

| Current location | Target owner | Adoption rule |
| --- | --- | --- |
| `AgentFramework.Models/Workflows` | Keep in Models initially, or move only if proven necessary | Preserve JSON and value object compatibility. |
| `Core/Workflows/WorkflowContracts.cs` | `Workflows.Abstractions` and `Workflows.Runtime` | Split contracts by runtime/store/catalog responsibility. |
| `Core/Workflows/WorkflowExecutorContracts.cs` | `WorkflowExecutors.Abstractions` and `WorkflowExecutors.Core` | Interfaces move before invoker/catalog implementation. |
| `Core/Workflows/WorkflowDefinitionValidator.cs` | `Workflows.Core` | Move after builder/factory primitives exist. |
| `Core/Workflows/WorkflowCatalogServices.cs` | `Workflows.Core` | Preserve API behavior and component validation. |
| `Core/Workflows/WorkflowRuntimeManager.cs` | `Workflows.Runtime` | Preserve run state and event semantics. |
| `Core/Workflows/WorkflowArtifactContentStores.cs` | `Workflows.Runtime` or `Workflows.Persistence` | Keep file path scope protections. |
| `Core/Workflows/WorkflowFailureDisplayFormatter.cs` and exception-derived failure handling | `Workflows.Core` plus shared failure diagnostic contracts | Replace brittle exception-string parsing with typed diagnostic envelopes before UI/API adoption. |
| `Modules.AgentFramework/Catalog/WorkflowTemplatePackLoader.cs` | `Workflows.Templates` | UI consumes a service, not YAML DTOs. |
| `Maf/Runtime/Workflows/*Executor.cs` | `WorkflowExecutors.Standard.*` | Split by logical category. |
| `Maf/Runtime/Workflows/BuiltInWorkflowExecutorDescriptors.cs` | `WorkflowExecutors.Core` plus category descriptor factories | Descriptor construction is executor-owned. |
| `Maf/Runtime/Workflows/MafWorkflowCompiler.cs` | `Workflows.MafAdapter` | Keep Microsoft Agents dependency isolated. |
| `Maf/Runtime/Workflows/MafInProcessWorkflowExecutionBackend.cs` | `Workflows.MafAdapter` | Backend adapter consumes runtime abstractions. |
| `Modules.Plugins/Catalog/PluginWorkflowExecutorDescriptorSource.cs` | `WorkflowExecutors.Plugins` | Preserve grant/source/trust metadata. |
| `Modules.Plugins/Catalog/PluginPackageServices.cs` runtime executor registration | `WorkflowExecutors.Plugins` plus package service adapter | Preserve installed package support. |
| Bundled plugin workflow executors | Plugin projects referencing executor abstractions | Migrate after compatibility bridge exists. |
| Module-provided workflow executors such as Cognitive Memory | Owning feature modules referencing `WorkflowExecutors.Abstractions` plus executor hardening tests | Do not force domain executors into default executor category projects; prove they no longer depend on MAF/Core executor-contract ownership. |
| `Web/Api/WorkflowsApi.cs` | API stays, uses workflow services | Adopt only after service contracts stabilize. |
| Workflow Blazor UI | UI stays, consumes isolated contracts/services | Adopt after templates and runtime are stable. |
| Workbench workflow nodes | Workbench stays, consumes runtime/catalog services | Adopt after API/runtime compatibility proof. |
| Workbench agent workflow tools | Workbench stays, consumes isolated workflow/project-structure services | Adopt with SB12 and harden in SB13 because these tools expose workflow node creation/start/status outside the Blazor page. |
| Scheduler workflow input option services | Scheduler/Composition stays, consumes workflow template/input contracts | Adopt with template extraction and API/UI adoption; do not leave scheduler consumers tied to UI-owned template DTOs. |

## MSBuild Concerns

- New projects must be added to `CanDoItAll.slnx` and referenced explicitly.
- Avoid project cycles by keeping abstractions below implementation projects.
- Directory.Build changes are not required unless implementation finds duplicated project metadata; do not centralize for its own sake.
- Test projects should reference the lowest project needed for each test. Avoid adding broad references to MAF in tests for workflow core behavior.
- Add boundary tests that fail if workflow abstractions reference MAF/Web/Modules.
- Add boundary tests that fail if feature modules with workflow executors still reference MAF/Core executor-contract ownership after executor abstractions move.
- Add diagnostic contract tests that fail if validation/runtime/executor/plugin failures lose node id, executor id, source/plugin/package/tool context, retryability, redacted technical detail, or repair hints.
- Add file-size/responsibility checks at checkpoints. Moving `SourceIngestionWorkflowExecutor`, `ProjectStructureWorkflowExecutor`, `WorkflowTemplatePackLoader`, or `MafInProcessWorkflowExecutionBackend` without splitting helpers/services must fail the checkpoint.

## SB02 Execution Boundary

- `CanDoItAll.AgentFramework.Workflows.Abstractions` references only `CanDoItAll.AgentFramework.Models`.
- `CanDoItAll.AgentFramework.Workflows.Builder` references `CanDoItAll.AgentFramework.Models` and `CanDoItAll.AgentFramework.Workflows.Abstractions`.
- No MAF, UI module, plugin implementation, plugin abstractions, persistence implementation, or web project reference is allowed from either SB02 project.
- SB02 intentionally did not move existing workflow model/value-object contracts out of `CanDoItAll.AgentFramework.Models`; downstream subbundles must preserve this compatibility unless they add explicit migration proof.

## SB03 Execution Boundary

- `CanDoItAll.AgentFramework.Workflows.Core` owns workflow validation, in-memory catalog services, routing compilation, preview simulation rendering, payload policy, failure display formatting, validation diagnostic mapping, process bridge, workflow test runner registration, and in-memory catalog registration.
- `CanDoItAll.AgentFramework.Workflows.Core` references `CanDoItAll.AgentFramework.Core` during the transition because runtime/store/executor contracts still live there until SB04 and SB06. It must not reference MAF, Blazor modules, plugin implementation projects, persistence implementation projects, or web projects.
- Hosting and `CanDoItAll.Modules.AgentFramework` consume workflow core through `AddWorkflowCoreServices()` instead of owning ad hoc validator, payload, process bridge, and workflow test runner registrations.
- Runtime manager, run/checkpoint/artifact/external-request stores, runtime backend implementation, MAF adapter, and executor contracts remain outside SB03 and are blocked from downstream adoption until their own subbundle gates pass.

## SB04 Execution Boundary

- `CanDoItAll.AgentFramework.Workflows.Runtime` owns workflow runtime/store contracts, runtime manager, in-memory run store, event sink, checkpoint factory, external request runtime support, artifact content stores, event payload helpers, runtime diagnostics, node execution progress scope, and runtime DI registration.
- `CanDoItAll.AgentFramework.Workflows.Runtime` must not reference MAF, Blazor modules, plugin implementation projects, persistence implementation projects, or web projects.
- `CanDoItAll.AgentFramework.Workflows.Runtime` temporarily references `CanDoItAll.AgentFramework.Core` because executor approval/redaction/audit contracts remain there until SB06.
- Hosting composes runtime services through `AddWorkflowRuntimeServices()` and in-memory stores through `AddInMemoryWorkflowRuntimeStores(...)`.
- `CanDoItAll.Modules.AgentFramework` composes runtime services through `AddWorkflowRuntimeServices()` but keeps `PersistentWorkflowRunStore` in the module because the store is currently coupled to module persistence and DbContext ownership.
- SchedulerPlanner and Workbench now reference `Workflows.Runtime` directly because they consume runtime manager/store/backend contracts outside the AgentFramework module.
- The MAF in-process backend remains in `CanDoItAll.AgentFramework.Maf` until SB11. SB04 only moves the runtime contracts that backend implements and the runtime manager that dispatches to it.

## SB05 Execution Boundary

- `CanDoItAll.AgentFramework.Workflows.Abstractions`, `CanDoItAll.AgentFramework.Workflows.Builder`, `CanDoItAll.AgentFramework.Workflows.Core`, and `CanDoItAll.AgentFramework.Workflows.Runtime` now have an explicit allowed project graph guarded by `WorkflowFoundationHardeningCheckpointTests` and `proof/SB05/transcripts/architecture-check.txt`.
- SB05 did not start executor extraction. Executor approval/redaction/audit contracts remain SB06-owned, so the temporary Core references documented in SB03/SB04 remain until executor boundaries move.
- Mixed-responsibility foundation files were split where the move had left unrelated public owners in the same implementation file. Remaining larger files are cohesive validators/catalog services under the SB05 line budget and are guarded by file-size/responsibility tests.
- Foundation diagnostics remain typed through `WorkflowFailureDiagnosticEnvelope`, validation/runtime diagnostic mappers, redacted technical detail, repair hints, and no loose `Dictionary<string, object>` diagnostic payloads.
- The SB05 performance scan found no critical or moderate open foundation issues; informational LINQ/list allocation candidates are deferred to SB14 profiling only if measured hot.

## SB06 Execution Boundary

- `CanDoItAll.AgentFramework.WorkflowExecutors.Abstractions` owns executor interfaces, descriptor sources, catalog/invoker/approval contracts, execution context, and executor audit contracts. It references only `CanDoItAll.AgentFramework.Models`.
- `CanDoItAll.AgentFramework.WorkflowExecutors.Core` owns executor catalog composition, invoker, policy limits, side-effect retry safety, payload policy, redaction, observability helpers, JSON/settings helpers, descriptor factory, typed executor diagnostic mapping, and executor DI registration.
- MAF no longer owns executor contracts or the shared `WorkflowExecutorJson` helper. It still owns concrete default executor implementations and built-in executor registration until SB07.
- Built-in descriptors and Cognitive Memory executor descriptors now consume `WorkflowExecutorDescriptorFactory`; descriptor ids and JSON shapes remain stable.
- Hosting and `CanDoItAll.Modules.AgentFramework` compose executor catalog/invoker/observer services through `AddWorkflowExecutorCoreServices()`.
- Plugin and feature-module consumers now reference executor foundation projects directly where they implement, describe, or observe workflow executors.
- SB06 intentionally did not move concrete default executors, plugin package adapters, templates, MAF backend/compiler, API, UI, or Workbench surfaces.

## SB07 Execution Boundary

- `CanDoItAll.AgentFramework.WorkflowExecutors.Standard` composes the default executor categories and exposes `AddStandardWorkflowExecutors(...)`.
- Default executor implementations now live in category projects: Control, Transforms, Workspace, Network, Documents, Media, and ProjectStructure.
- MAF consumes standard executor registration through its built-in registration extension and keeps only adapter/compiler/backend workflow code in `Runtime/Workflows`.
- `CanDoItAll.Modules.AgentFramework` composes scoped standard executors through the aggregate registration instead of directly registering concrete default executor classes.
- Built-in descriptor compatibility and shared payload-text helper ownership now live in executor core, while category descriptor sources partition the stable built-in descriptor set.
- Source Ingestion and Project Structure implementations were split into category-local helpers by responsibility before downstream hardening.
- Plugin package adapters, template loading, MAF backend/compiler isolation, API, UI, and Workbench adoption remain downstream SB08-SB14 work.

## SB08 Execution Boundary

- `CanDoItAll.AgentFramework.WorkflowExecutors.Plugins` owns plugin workflow executor descriptor projection, source/trust mapping, runtime package executor wrapping, runtime package descriptor source registration, and plugin executor activation context.
- `CanDoItAll.Modules.Plugins` keeps plugin installation stores, grant stores, connection/OAuth services, package manifest extraction, load-context resolution, audit sink persistence, and UI pages.
- The module bridges `PluginGrantEvaluator` into the executor boundary through `IPluginWorkflowExecutorGrantEvaluator`; the boundary does not reference `CanDoItAll.Modules.Plugins`, EF, Infrastructure, Web, or MAF.
- Runtime package assembly scanning remains in `PluginPackageServices`, but discovered executor types are registered through `PluginWorkflowExecutorRuntimeRegistration`.
- Bundled plugin projects still compile against the existing public plugin contracts; SB08 does not change manifest schema or `IPluginWorkflowExecutor`.
- Combined plugin/default diagnostic classification, no-generic-error review, plugin activation redaction, and executor file responsibility passed the SB09 gate. UI/API display adoption remains SB12-SB13.

## SB09 Execution Boundary

- SB09 did not add new production projects; it hardened the executor/plugin boundary through focused tests and small executor-scope cleanup.
- `CanDoItAll.AgentFramework.WorkflowExecutors.Plugins` now exposes plugin activation failure kind, retryability, repair hint, and redacted technical detail without adding MAF, module, Web, EF, Infrastructure, or Persistence references.
- Default category, plugin boundary, runtime package, and Cognitive Memory descriptors are proven together before SB10 template materialization consumes the descriptor catalog.
- Gmail and Office365 workflow executors now use shared static serializer options per plugin workflow file.
- Template loading, MAF adapter isolation, API/UI/Workbench adoption, and browser validation remain blocked until their downstream subbundles.

## SB10 Execution Boundary

- `CanDoItAll.AgentFramework.Workflows.Templates` owns workflow template manifest parsing, YAML DTOs, input parameter materialization, graph materialization, preview simulation fixture loading, descriptor-aware validation, and template diagnostics.
- `CanDoItAll.AgentFramework.Workflows.Templates` references workflow builders/core, executor abstractions/core, models, and shared kernel; it must not reference the Blazor module, MAF, Web, module persistence, or plugin implementation projects.
- `CanDoItAll.Modules.AgentFramework` delegates template loading through `AddWorkflowTemplateServices()` and consumes `WorkflowTemplatePackLoader`; the deleted module-local loader file must not reappear as a fallback.
- Template graph materialization uses the SB02 builder APIs, preserving explicit workflow input/output port ids and existing template key/runtime/input/preview semantics.
- Descriptor validation consumes `IWorkflowExecutorCatalog` so SB11/SB12 bind template executor references to the same default/plugin/package/feature-module descriptor sources hardened in SB09.
- Visible workflow template selection behavior was not intentionally changed in SB10. Large-screen browser validation remains owned by SB12/SB13/SB14; small and medium viewport tests are skipped for this initiative per user instruction.

## SB11 Execution Boundary

- `CanDoItAll.AgentFramework.Workflows.MafAdapter` owns MAF-specific workflow compiler, in-process backend, event normalization, LLM component invocation, handoff workflow factory, adapter registration, compile-failure diagnostics, and backend helper responsibilities.
- `CanDoItAll.AgentFramework.Maf` no longer owns workflow compiler/backend/LLM/event/handoff implementation files and consumes workflow behavior through the adapter project.
- Workflow-owned projects, template services, executor abstractions/core/standard/plugin projects, and runtime/core projects must not reference `CanDoItAll.AgentFramework.Maf` or `CanDoItAll.AgentFramework.Workflows.MafAdapter`.
- Hosting and `CanDoItAll.Modules.AgentFramework` compose the MAF adapter through `AddMafWorkflowAdapterServices(...)`; they do not directly register `MafWorkflowCompiler`, `MafInProcessWorkflowExecutionBackend`, or standard executors.
- Standard/default executor registration is owned by `CanDoItAll.AgentFramework.WorkflowExecutors.Standard`; the old `AddBuiltInWorkflowExecutors` alias is removed.
- MAF compile failures now emit typed redacted diagnostics in runtime event payloads. Executor, plugin, tool, and MCP failures continue through executor core/plugin diagnostics instead of adapter-local generic backend errors.
- API, Blazor workflow page/editor, Workbench workflow nodes, and browser validation remain blocked until SB12. Small and medium viewport tests are skipped for this initiative per user instruction.

## SB12 Execution Boundary

- API/UI/Workbench adoption now consumes isolated workflow, runtime, template, executor, and MAF adapter services without reintroducing direct MAF compiler/backend references in user-facing source paths.
- `WorkflowFailureDisplayFormatter` in `CanDoItAll.AgentFramework.Workflows.Core` is the shared user-facing display boundary for typed runtime event diagnostics and redacted legacy fallback text.
- `WorkflowsPage` consumes typed `WorkflowEventRecord.PayloadJson` diagnostics for event rows, event details, failed run summaries, and technical detail display instead of rendering raw event messages.
- `WorkflowCanvasEditor` routes workflow editing failure messages through the shared formatter while keeping canvas-specific rendering concerns in the Blazor module.
- Workbench workflow-node status now carries runtime event payload JSON and resolves failed workflow status from the latest typed error or executor-failed event before falling back to redacted legacy text.
- Workbench workflow add/create/start/status UI exception paths use formatter-redacted messages and do not reach into MAF workflow internals.
- SB12 does not delete obsolete paths or broaden UI styling. SB13 must harden adoption with no-fallback, no-generic-error, file-size/responsibility, and performance checks; SB14 owns final cleanup and documentation.
- Browser proof is large-screen-only for SB12, SB13, and SB14. Small and medium viewport tests are intentionally skipped per the current user instruction.

## SB13 Execution Boundary

- SB13 added `WorkflowAdoptionHardeningCheckpointTests` as an adoption-specific guard against API/UI/Workbench references to direct MAF workflow compiler/backend/event/LLM internals, the removed built-in executor alias, and direct Microsoft Agents workflow package APIs.
- SB13 corrected the stale executor hardening expectation that still allowed old MAF workflow files to remain. After SB13, both executor hardening and MAF adapter isolation expect the old MAF workflow folder to be empty.
- API/UI/Workbench diagnostic display remains centralized through `WorkflowFailureDisplayFormatter`; UI and Workbench adoption files do not deserialize typed diagnostic envelopes directly.
- Focused no-generic-error and no-fallback audits passed for adoption files, with the only legacy-message fallback intentionally contained inside the shared formatter.
- Focused performance scan found no critical adoption-scope defects. Existing large UI files remain approved SB13 exceptions because splitting them during a hardening checkpoint would be a broader UI refactor; SB14 must document this final convention and risk disposition.
- SB14 may start cleanup only from this state: SB12 adoption proof passed, SB13 hardening proof passed, and browser proof remains large-screen-only per user instruction.

## SB14 Final Execution Boundary

- No obsolete workflow/executor implementation files remain under `CanDoItAll.AgentFramework.Maf\Runtime\Workflows`; SB14 cleanup proof treats the empty folder as an absence marker and does not delete unrelated project layout without a tracked source change.
- Future workflow work must use the current owners documented in `docs/workflow-maf-hardening.md`: models for serialized contracts, workflow abstractions/builders/core/runtime/templates/MAF adapter for workflow behavior, executor abstractions/core/standard categories/plugins for executor behavior, and UI/API/Workbench only for orchestration/display.
- Host composition stays explicit through `AddWorkflowCoreServices()`, `AddWorkflowRuntimeServices()`, `AddWorkflowTemplateServices()`, `AddWorkflowExecutorCoreServices()`, `AddStandardWorkflowExecutors(...)`, `AddPluginWorkflowExecutorBoundary()`, and `AddMafWorkflowAdapterServices(...)`.
- API, Blazor workflow UI, Workbench workflow nodes, process integration, templates, default executors, plugin executors, and MAF adapter paths are closed by final regression proof in `proof/SB14/`.
- Browser validation remains large-screen-only for final closure. Small and medium viewport tests are skipped by request because this application target is large-screen desktop.
- Existing large UI page files and legacy Workbench project-structure orchestration services remain documented exceptions for their current responsibilities only. New diagnostic parsing, template materialization, runtime, adapter, executor, plugin, or domain behavior must be factored into focused services/helpers with tests.
