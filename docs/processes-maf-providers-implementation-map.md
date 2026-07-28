# Processes, MAF, And Providers Implementation Map

Last source review: 2026-06-29.

This is the current source-grounded map for the process runtime, Microsoft Agent Framework integration, provider runtime, and the next hardening-refactor roadmap. Historical bundle files remain useful evidence, but this document is the current orientation point for active docs.

## Executive Summary

The current implementation is a rebuilt generic process runtime integrated with AgentFramework-backed execution through module adapters. Process launch, dispatch, persistence, projection, and operator routes are now split across `CanDoItAll.Processes.*`, `CanDoItAll.Modules.Processes`, and `CanDoItAll.Web`.

MAF is the provider/tool/runtime adapter layer. It owns Microsoft Agent Framework composition, provider dispatch, capability/tool assembly, MCP/A2A integration, input attachments, structured-output/finalizer handling, and workflow adapter execution. Product-owned tools are supposed to enter MAF through registered `IAgentRuntimeToolProvider` implementations.

Provider execution is no longer a thin legacy gateway. It has provider descriptors, concrete provider drivers, runtime handle pooling, per-lane dispatch gates, streaming gates, batching, image generation, and provider-failure classification.

The current hardening note: some policy/test names still describe legacy or planned direct `processes_*` runtime tools and `ProcessAgentRuntimeToolProvider`, but that concrete provider is not present in the current source tree. Current process control is HTTP API plus governed process execution adapters and project-structure bridge tools.

Internal-agent skills, tools, MCP servers, and capability access policies are template-backed through `Templates/Capabilities`. Codex development skills under `codex/skills` or `%USERPROFILE%\.codex\skills` are not runtime template inputs for internal agents.

## Current Capability And Tool Model

AgentFramework composes executable capability surfaces from these sources:

- MAF built-in workspace and execution tools.
- Template-seeded skills from `Templates/Capabilities/skills.json`, with inline instructions and resources under `Templates/Capabilities/skills/`.
- Template-seeded MCP descriptors from `Templates/Capabilities/mcps.json`, such as Playwright Local MCP.
- Template-seeded internal or external tool descriptors from `Templates/Capabilities/tools.json`.
- Provider-native tools exposed by a provider driver.
- Registered `IAgentRuntimeToolProvider` implementations owned by product modules.

Capability templates seed catalog rows and policy metadata; they do not alone make a direct tool executable. A direct runtime tool must still be registered by MAF or returned by an `IAgentRuntimeToolProvider`, with policy classification and tests.

The current registered first-party runtime tool providers are:

- `ProjectStructureAgentRuntimeToolProvider` in Workbench.
- `ImageGenerationAgentRuntimeToolProvider` in the AgentFramework module.

There is no direct process runtime tool provider. Process steps execute through `AgentFrameworkProcessExecutionAdapter`, which builds a governed step prompt and lets MAF attach the permitted workspace, skill, MCP, provider-native, and registered runtime-provider tools according to assignment context and capability policies.

## Current Source Boundaries

| Area | Current owner | Source |
| --- | --- | --- |
| Runtime composition | Web host composition root | `src/App/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs` |
| Process module DI | Process module | `src/Modules/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs` |
| Process HTTP API | Web API | `src/App/CanDoItAll.Web/Api/ProcessesApi.cs` |
| Process launch | Application layer | `src/Processes/CanDoItAll.Processes.Application/ProcessLaunchApplicationService.cs` |
| Process dispatch | Application/runtime layers | `src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeDispatchApplicationService.cs`, `src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.cs`, `src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeScheduler.cs` |
| Process persistence | EF-backed process persistence | `src/Processes/CanDoItAll.Processes.Persistence/*` |
| Process projections | Projection contracts/projector/query services | `src/Processes/CanDoItAll.Processes.Projections/*`, `src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeProjectionQueryService.cs` |
| Process UI | Blazor module | `src/Modules/CanDoItAll.Modules.Processes/Pages/*`, `src/Modules/CanDoItAll.Modules.Processes/Components/*` |
| Process invocation snapshot | Processes agent-chat adapter | `src/Modules/CanDoItAll.Modules.Processes/AgentChat/ProcessInvocationSnapshot.cs` |
| Project-structure process bridge | Workbench module | `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureProcessNodeService.cs` |
| Project-structure runtime tools | Workbench runtime tool provider | `src/Modules/CanDoItAll.Modules.Workbench/AgentTools/ProjectStructureAgentRuntimeToolProvider.cs` |
| Typed agent activity and preparation | AgentFramework Core | `src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentExecutionActivityCoordinator.cs`, `src/MAF/Common/CanDoItAll.AgentFramework.Core/Preparation/*` |
| Canonical provider profile snapshot | AgentFramework module | `src/Modules/CanDoItAll.Modules.AgentFramework/Providers/ProviderRuntimeProfileSnapshotService.cs` |
| MAF runtime | AgentFramework MAF adapter | `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs` |
| MAF workflow adapter | AgentFramework MAF workflows | `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/*` |
| Provider runtime | AgentFramework providers and MAF provider gateway | `src/MAF/Common/CanDoItAll.AgentFramework.Providers/*`, `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Providers/*` |
| AgentFramework module facade | Current-profile agent workspace and provider gateway | `src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs` |

## Runtime Composition

`AddCanDoItAllRuntimeModules` registers the active modules in this order:

1. Security, workspace, projects, workbench, resources, prompts, factory, plugins.
2. Gmail and Office365 plugins.
3. Processes.
4. TestLab.
5. AgentFramework.
6. Qdrant RAG driver, Cognitive Memory, scheduler/planner, collaboration, CRM/HR, workflow input options.

Two details matter for hardening:

- Processes are registered before AgentFramework, but process execution still depends on AgentFramework abstractions through `CanDoItAll.Modules.Processes`.
- Runtime database profile readiness seeds provider bootstrap state in `AppDatabaseBootstrapper`, including the managed OpenAI provider profile and secret record when `OPENAI_API_KEY` is configured.
- `AppDatabaseBootstrapper` initializes the canonical immutable provider-runtime-profile snapshot after the active database profile is ready.
- The process-local typed agent-activity coordinator is singleton and bounded; its current-profile reader is scoped and rejects/cancels cross-profile-generation access.

## Process Module Registration

`AddProcessesModule` currently registers:

- `ProcessPersistenceDbContext` and factory with profile-aware InMemory or PostgreSQL configuration.
- EF-backed runtime stores: runtime state, run hierarchy, idempotency, event store/replay store, outbox, artifact ledger, projection store, instance plan store, and step assignment store.
- Runtime/projection services: `IProcessRuntimeProjector`, `ProcessRuntimeProjectionCatchupService`, `ProcessRuntimeProjectionQueryService`.
- Launch/dispatch/operator services: `ProcessLaunchApplicationService`, `ProcessRuntimeDispatchApplicationService`, `ProcessRuntimeOperatorApplicationService`.
- UI projection services for definition catalog, editor, role editor, canvas editor, step editor, template catalog, and workspace shell.
- AgentFramework integration adapters:
  - `IProcessStepBriefBuilder` -> `AgentFrameworkProcessStepBriefBuilder`
  - `IProcessExecutionAdapter` -> `AgentFrameworkProcessExecutionAdapter`
  - `IProcessExecutionObservationReader` -> `AgentFrameworkProcessExecutionObservationReader`
  - `IProcessRuntimeUsageTelemetryReader` -> `AgentFrameworkProcessRuntimeUsageTelemetryReader`
  - launch executor resolver, assignment repair, claim recovery, cancellation observer
- Standard process driver catalog and strategy factory resolver.
- Singleton in-memory process dispatch queue and hosted workers:
  - `ProcessRuntimeDispatchQueueWorker`
  - `AgentFrameworkProcessExecutionClaimRecoveryWorker`

The module does not currently register a concrete `ProcessAgentRuntimeToolProvider`.

## Process Project Map

| Project | Current role |
| --- | --- |
| `CanDoItAll.Processes.Abstractions` | Strong process ids. |
| `CanDoItAll.Processes.Contracts` | Contract version markers. |
| `CanDoItAll.Processes.Core` | Graph kernel, artifacts, branches, loop fingerprints, runtime events, state transitions, validation result. |
| `CanDoItAll.Processes.Builder` | Compile request, immutable plan, validation, plan hashing, persistence contract. |
| `CanDoItAll.Processes.Runtime` | Runtime state, commands, engine, scheduler, dispatcher, manager contracts, identifiers, branch/recovery/subprocess contracts. |
| `CanDoItAll.Processes.Persistence` | EF entities, mappings, DbContext, unit of work, event/outbox/artifact/projection/plan/assignment stores. |
| `CanDoItAll.Processes.Projections` | Runtime read models, projector, query contracts, shell/template/editor projection contracts, JSON codec. |
| `CanDoItAll.Processes.Templates` | JSON template documents, pack loader, hashing, migrations, merge, compatibility scans, summaries. |
| `CanDoItAll.Processes.Drivers.Abstractions` | Driver descriptors, packages, catalog, execution adapter and strategy contracts. |
| `CanDoItAll.Processes.Drivers.Standard` | Standard layered adapter descriptors and strategy factory package. |
| `CanDoItAll.Processes.Application` | Launch, dispatch, operator, manager control loop, projections, template kernel builder, compatibility decisions. |
| `CanDoItAll.Modules.Processes` | Blazor UI, DI, AgentFramework process adapter, dispatch queue worker, shell navigation. |

## Process Launch Flow

Current launch entry points:

- `POST /api/processes/launch/check`
- `POST /api/processes/launch`
- Process workspace UI through `ProcessWorkspaceShell`
- Project-structure process start through `ProjectStructureProcessNodeService`
- Project-structure runtime tool `project_structure_node_process_start`
- Subprocess runtime tool `project_structure_process_subprocess_launch`

`ProcessLaunchApplicationService.PreviewAsync` backs `launch/check` and returns a launch plan/readiness result without persistence. `ProcessLaunchApplicationService.LaunchAsync` performs the real launch:

1. Resolve the process definition from template pack data or explicit process definition id.
2. Load the standard process driver catalog.
3. Convert the template into a process kernel with `ProcessTemplateKernelBuilder`.
4. Compile a `ProcessInstancePlan` with `ProcessInstancePlanCompiler`.
5. Resolve executor bindings through `AgentFrameworkProcessLaunchExecutorResolver`.
6. Persist the plan.
7. Build initial runtime state and commit `ProcessRunCreated` through `IProcessRuntimeUnitOfWork`.
8. Save runtime step assignments.
9. Initialize the managed process artifact root through `IProcessLaunchArtifactInitializer`.
10. Activate the run, schedule ready steps, and catch projections up.
11. If `Execute=true`, enqueue a `ProcessRuntimeDispatchQueueRequest`.

Readiness now uses actual agent/provider state. `AgentFrameworkProcessLaunchExecutorResolver` queries workspace agents/providers, checks role aliases and live-run assignments, respects executor overrides, and blocks steps that lack active governed-output-capable provider bindings.

## Process Dispatch Flow

Current dispatch entry points:

- `POST /api/processes/runs/{runId}/dispatch`
- background `ProcessRuntimeDispatchQueueWorker`
- launch-time queue enqueue when `Execute=true`
- recovery queue polling when enabled

`ProcessRuntimeDispatchApplicationService.ExecuteReadyAsync` runs the dispatch loop:

1. Load current runtime state and instance plan.
2. Activate created runs if needed.
3. Expire claims and release stale pre-running claims.
4. Schedule ready work.
5. Calculate ready work with `ProcessRuntimeScheduler`.
6. Create and mark dispatch claims.
7. Enforce maximum dispatch attempts.
8. Resolve strategy factory from the step binding.
9. Invoke the strategy through `ProcessStrategyDispatcher`.
10. Submit strategy result with idempotency key.
11. Catch projections up after submitted work.
12. Block exhausted runs when no ready work remains.

The queue worker limits local parallelism to two immediate and two recovery dispatches. It also handles terminal child runs by releasing or reworking active parent claims and re-enqueuing parent runs.

## Process API Surface

The current source-grounded `/api/processes` route list is:

| Method | Route | Purpose |
| --- | --- | --- |
| `GET` | `/api/processes/contract` | Lists current process API routes and boundary note. |
| `POST` | `/api/processes/launch/check` | Compiles a launch plan and readiness findings without creating a run. |
| `POST` | `/api/processes/launch` | Launches a process from definition key or definition id. |
| `POST` | `/api/processes/runs/{runId}/dispatch` | Dispatches ready work for a run. |
| `POST` | `/api/processes/runs/{runId}/cancel` | Requests run cancellation. |
| `POST` | `/api/processes/runs/{runId}/steps/{stepInstanceId}/rework` | Requests step rework through operator service. |
| `GET` | `/api/processes/live` | Reads live process snapshots. |
| `GET` | `/api/processes/runs/{runId}` | Reads run detail projection. |
| `GET` | `/api/processes/runs/{runId}/history` | Reads timeline history projection. |

Definition authoring, template import/export, assignments, artifacts, escalations, direct messages, approvals, manager directives, and analytics are not currently exposed by `ProcessesApi.cs`. If those are required again, they need a deliberate API contract update and tests rather than doc-only resurrection.

Use `launch/check` for non-mutating preflight. `POST /api/processes/launch` creates and schedules a durable run when readiness allows launch; `execute: false` only prevents immediate dispatch queueing.

## Process UI Surface

Active process routes:

- `/processes`
- `/processes/live`
- `/projects/{ProjectId:guid}/processes`
- `/projects/{ProjectId:guid}/processes/live`

`ProcessWorkspaceShell` is projection-first. It uses application projection services rather than reading persistence directly. Current panels cover definition metadata, roles, steps/canvas, runs, graphs, analytics, exchange, template library, and manager chat UI state. The live dashboard is a projection consumer, not the dispatcher.

The process workspace and live dashboard publish a bounded
`ProcessInvocationSnapshot` from the `ProcessWorkspaceShellProjection` they already
hold. The snapshot includes per-component `NotRequested`/`Absent`/`Present` state,
source, absence reason, content fingerprint, freshness, and optional durable run-record
revision. Fields are copied only from provenance components marked `Present`.

Snapshot publication requires a ready shell refresh. Freshness is anchored to the
projection observation (`ObservedAtUtc + 5 minutes`), not extended merely because the
UI copied it again. A timer reevaluates publication at expiry and removes the
attachment unless the shell has received a newer valid source observation. The
snapshot is bounded to 32 runs, 6 recent events per run, and 32 active agents, with an
even smaller prompt projection. It is read context, not process mutation authority or
a replacement for the projection/application query services.

Hardening implication: UI tests should assert projection contracts and visible runtime states, not private persistence rows.

## Project-Structure Process Bridge

`ProjectStructureProcessNodeService` is the supported bridge between project structure and process launch.

`StartAsync`:

- validates project/node input;
- resolves linked process definition id;
- identifies the target project node;
- creates typed process launch variables from project structure context;
- launches through `ProcessLaunchApplicationService`;
- links the resulting process-run node back to project structure when a run is created.

`StartSubprocessAsync`:

- validates parent run id and parent step id;
- loads parent assignment and runtime state;
- requires the parent assignment to allow `ProcessOperationContractNames.ExecuteExternalAction`;
- enforces project scope from the parent assignment;
- builds subprocess identity variables;
- reuses an existing matching launch when possible;
- otherwise launches a child run with the parent root run id;
- links child and parent process-run nodes back into project structure.

`ProjectStructureAgentRuntimeToolProvider` exposes process-adjacent tools through Workbench:

- `project_structure_node_process_definition_link`
- `project_structure_node_process_start`
- `project_structure_process_subprocess_launch`

These are current direct runtime tools. They are not equivalent to a full direct process API surface.

## MAF Runtime Map

`MafAgentRuntime` implements `IAgentRuntime` and composes the runtime per execution:

- resolves runtime model and provider behavior;
- prepares input attachments and image-analysis model selection;
- composes Microsoft Agent Framework agent/session/run options;
- attaches built-in capabilities, skills, MCP servers, local/hosted tools, A2A tools, context providers, and registered runtime tool providers;
- handles tool approvals and approval continuations;
- streams provider output through `RunProviderStreamingAsync`;
- snapshots tool calls and provider-native MCP output safely;
- records context assembly manifests, finalizer invocations, tool invocation traces, and usage observations;
- enforces structured output and finalizer policy through `AgentRuntimeExecutionOptions`.

MAF must not directly reference product modules just to attach product tools. Current product-owned runtime tool providers are:

- `ProjectStructureAgentRuntimeToolProvider` in Workbench.
- `ImageGenerationAgentRuntimeToolProvider` in AgentFramework module.

There is no current concrete process runtime tool provider in `CanDoItAll.Modules.Processes`.

## Provider Runtime Map

Provider runtime services are registered by `AddMafProviderRuntimeServices`:

- `IProviderRuntimeDescriptorStore`
- `IProviderDriverCredentialResolver`
- `MafProviderDriverHttpClientPool`
- `IAgentProviderFactory`
- `IProviderDispatchLaneGate`
- `IMafProviderStreamingDispatchGate`
- `IProviderRuntimeHandleFactory`
- `IProviderRuntimePool`
- `IProviderBatchJobBalancer`
- `IAgentImageGenerationService`
- `IMafProviderRuntimeGateway`

Concrete provider drivers currently registered:

- OpenAI
- Azure OpenAI
- Ollama
- ComfyUI

`MafProviderRuntimeGateway` is the provider operation gateway for health checks, test chat, image chat, and Ollama model maintenance. It upserts provider descriptors before resolving a pooled runtime handle. Each operation validates provider kind before dispatching to a concrete driver.

`ProviderRuntimePool` caches runtime handles by provider id and descriptor key. Descriptor changes replace the handle and dispose the old one.

`ProviderDispatchLaneGate` limits parallel dispatch by provider/capability/operation/model lane using driver-provided dispatch limits. Streaming chat also enters the provider lane gate.

`AgentProviderFailureDisplayFormatter` normalizes quota/billing, rate-limit, and general provider failures into user-actionable messages with redacted provider detail.

This MAF handle pool is distinct from
`CanonicalProviderRuntimeProfileSnapshotService`. The latter is the immutable provider
configuration projection used during agent preparation. It is database-profile and
generation fenced, stores persistent provider `ConcurrencyToken` values as typed
revisions, probes those revisions at use time, refreshes changed providers, and fails
closed when canonical revision verification fails.

## Provider Profile And Bootstrap Behavior

`AppDatabaseBootstrapper` seeds or repairs the managed OpenAI provider profile for the active database profile:

- `OPENAI_API_KEY` can be promoted from configuration to process environment.
- The default OpenAI provider is created or normalized with Responses transport metadata.
- The default API key secret is stored through the runtime secret mechanism, not raw appsettings persistence.
- Workspace settings are pointed at the managed provider when no valid default exists.

`ProviderProfileService.ResolveFeatureMatrix` is the central feature gate for structured output, tool support, hosted/local MCP, service-managed history, vision, compaction, image generation, and approval support.

Provider save/delete commit observers update the canonical runtime profile snapshot
only after the database commit. A projection failure is explicit and faults the
snapshot; it does not roll back or conceal the successful canonical commit. Resolved
credential values are not stored in that singleton. They live in a fingerprint-checked,
one-dispatch credential scope and are cleared when it is disposed.

## Known Gaps

| Gap | Why it matters | Current handling |
| --- | --- | --- |
| No concrete `ProcessAgentRuntimeToolProvider` | Policy constants and some tests still discuss `processes_*` direct runtime tools, but current source does not register that provider. | Use `/api/processes`, governed process execution adapters, and project-structure runtime tools. Decide whether to reintroduce or retire direct process tools. |
| Process API route scope is narrower than old docs | Old docs mention definitions/templates/artifacts/assignments/escalations/approvals/analytics routes that are absent. | Current API docs must match `ProcessesApi.cs`; broader endpoints require implementation. |
| In-memory singleton dispatch queue | Works for local process lifetime, but not durable cross-process dispatch coordination. | EF stores hold run state/events; queue durability and multi-node dispatch need hardening. |
| Projection freshness depends on catch-up calls and worker behavior | API/UI correctness depends on projector catch-up after mutations and dispatch. | Existing services call catch-up, but release gates should assert freshness under failure/recovery. |
| Provider snapshots and handle pools have different invalidation boundaries | Provider-profile commits update the immutable canonical snapshot, while MAF runtime handles are still keyed/replaced by runtime descriptors at operation entry. A remote writer can bypass this host's commit observer. | Use-time database revision probes fail closed or refresh the profile snapshot; multi-host edit-to-handle propagation still needs deployment-level proof. |
| MAF direct product-tool boundary is intentionally narrow | Project-structure and image generation use runtime providers; process control is API/adapter/bridge based. | Treat missing `processes_*` tools as a product decision point, not a MAF attachment failure. |
| Historical proof docs remain broad and stale | Old class names can mislead agents into coding against removed surfaces. | Active docs should link here, `docs/api-control-plane.md`, and `docs/agent-runtime-tool-surface.md`, and mark historical proof as historical. |

## Hardening-Refactor Roadmap

### Phase 0: Documentation And Contract Guardrails

Goal: stop docs and skills from drifting away from source.

- Add source-backed doc assertions for current route lists in `ProcessesApi.cs`, `AgentsApi.cs`, `WorkflowsApi.cs`, and project-structure API.
- Add a docs static test that fails when active docs name non-existent current source files.
- Decide which remaining proof docs are historical and add an explicit banner instead of silently mixing old and current architecture.
- Keep `docs/api-control-plane.md`, `docs/agent-runtime-tool-surface.md`, and this map as the active public path.

Validation:

```powershell
rg "registers .*ProcessAgentRuntimeToolProvider|Current direct runtime tools: 23|/api/processes/definitions|/api/processes/templates|/api/processes/runs/\{runId\}/detail|ProcessManagerTools" docs src/MAF/Common/CanDoItAll.AgentFramework.Core src/MAF/Common/CanDoItAll.AgentFramework.Maf -g "*.md" -g "!processes-maf-providers-implementation-map.md"
git diff --check
```

### Phase 1: Process API Contract Hardening

Goal: make the web API contract explicit and stable.

- Generate or snapshot the `/api/processes/contract` route list from `ProcessesApi.cs`.
- Add endpoint tests for launch preflight, launch, dispatch, cancel, rework, live, detail, and history.
- Decide whether definition/template/artifact/assignment/operator endpoints return to the HTTP API. If yes, implement them as typed route groups with explicit authorization and readback tests.
- Update `candoitall-api-processes` skill after route changes, not before.

Validation:

- API route unit/integration tests.
- OpenAPI diff when route shape changes.
- Source scans proving docs and skill route lists match implementation.

### Phase 2: Process Direct Runtime Tool Decision

Goal: remove the current ambiguity around `processes_*` direct tools.

Choose one path:

- **Reintroduce** a concrete `ProcessAgentRuntimeToolProvider` in `CanDoItAll.Modules.Processes` with typed request/response models, policy classifications, approval behavior, process access metadata, and tests.
- **Retire** process direct tools by removing or renaming stale policy/test constants and updating agent role capability docs to use the HTTP API skill plus project-structure bridge tools.

If reintroduced, minimum tools should start small:

- read live/detail/history;
- launch process;
- dispatch run;
- request cancellation;
- request step rework.

Do not reintroduce broad definition/template/operator tools until the HTTP API surface and approval model are stable.

### Phase 3: Dispatch And Persistence Hardening

Goal: make process dispatch robust under restart, concurrency, and multi-worker pressure.

- Replace or augment the in-memory dispatch queue with a durable queue/outbox-backed dispatch lease.
- Prove root-run sequence locks, idempotency keys, and claim recovery under concurrent dispatch.
- Add failure tests for stale pre-running claims, terminal child runs, parent rework, and projection catch-up failure.
- Expose dispatch queue health through an operator/readiness endpoint.

Validation:

- Focused runtime dispatch tests.
- Integration tests with PostgreSQL profile.
- Recovery tests that simulate restart between claim, strategy execution, and result submission.

### Phase 4: Provider Runtime Hardening

Goal: make provider profile edits, quota failures, lane limits, and credentials operationally predictable.

- Implemented foundation: canonical immutable provider snapshots, database/profile-generation fences, provider concurrency-revision probes, post-commit upsert/remove, explicit projection faults, and per-dispatch credential scopes.
- Keep tests proving provider profile edits refresh/fault the canonical snapshot and invalidate or replace affected runtime handles.
- Make dispatch lane limits visible in provider diagnostics.
- Record provider failure category and redacted provider detail on execution runs.
- Add credential-resolution diagnostics that never log secret values.
- Add provider health/readiness proof for OpenAI, Azure OpenAI, Ollama, and ComfyUI drivers with fake/local gates.

Validation:

- `ProviderRuntimeLifecycleTests`
- `ProviderRuntimeProfileSnapshotServiceTests`
- `ProviderDispatchLaneGateTests`
- `AgentProviderFailureDisplayFormatterTests`
- provider fake-driver integration tests

### Phase 5: MAF Execution Contract Hardening

Goal: make MAF/runtime behavior deterministic for governed process automation.

- Keep structured output and finalizer policies as explicit execution contracts.
- Prove required finalizers are last significant tool invocations for governed process steps.
- Strengthen context/tool filtering for process step operation contracts.
- Keep provider-native MCP receipt enrichment bounded and auditable.
- Add regression coverage for input attachments, image model selection, approval continuation, and service-managed history compatibility.

Validation:

- `AgentFinalizerPolicyTests`
- `MafAgentRuntimeToolProviderCompositionTests`
- `MafAgentRuntimeAttachmentTests`
- `MafAgentRuntimeProviderHealthTests`

Typed activity, preparation, provider-snapshot, and module-snapshot details are in
[Agent execution activity and runtime snapshots](architecture/agent-execution-activity-and-runtime-snapshots.md).

### Phase 6: Project-Structure Process Bridge Hardening

Goal: make project-structure launched processes and subprocesses safe and explainable.

- Add focused tests for linked definition resolution, target node selection, launch variable normalization, run-node linkback, and subprocess reuse.
- Enforce subprocess scope and allowed operations before side effects.
- Add user-facing diagnostics for missing process definition link, missing target node, missing parent assignment, and denied operation contract.
- Keep project-structure tool descriptions aligned with actual typed inputs.

Validation:

- `ProjectStructureAgentIntegrationTests`
- project-structure runtime tool provider tests
- process launch integration tests

### Phase 7: Observability And Operator Readiness

Goal: give operators current, actionable state without relying on old runtime-host docs.

- Add health/readiness endpoint or projection fields for dispatch worker state, projection lag, recoverable run count, active dispatch count, provider failure category counts, and stale claim count.
- Keep `/api/processes/live`, `/api/processes/runs/{runId}`, and `/api/processes/runs/{runId}/history` as the baseline readback set.
- Add explicit operator docs for cancellation, rework, dispatch, and provider quota remediation.

Validation:

- API readback tests.
- Projection freshness tests.
- Operator runbook source-reference scan.

## Validation Commands

For documentation-only changes:

```powershell
git diff --check
rg "registers .*ProcessAgentRuntimeToolProvider|Current direct runtime tools: 23|/api/processes/definitions|/api/processes/templates|/api/processes/runs/\{runId\}/detail|ProcessManagerTools" docs src/MAF/Common/CanDoItAll.AgentFramework.Core src/MAF/Common/CanDoItAll.AgentFramework.Maf -g "*.md" -g "!processes-maf-providers-implementation-map.md"
```

For source changes in the next hardening-refactor, start with focused tests instead of the full suite:

```powershell
dotnet test tests\Unit\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~ProcessRuntimeDispatchApplicationServiceTests|FullyQualifiedName~ProviderDispatchLaneGateTests|FullyQualifiedName~ProviderRuntimeLifecycleTests|FullyQualifiedName~AgentProviderFailureDisplayFormatterTests|FullyQualifiedName~MafAgentRuntimeToolProviderCompositionTests"
dotnet test tests\Integration\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --configuration Release --filter "FullyQualifiedName~ProjectStructureAgentIntegrationTests|FullyQualifiedName~AgentFrameworkExecutionRunTrackingIntegrationTests"
```
