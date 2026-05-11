# Target Solution

## Layering

- `CanDoItAll.AgentFramework.Models` owns serializable workflow domain models: workflow definitions, component definitions, run view models, workflow settings, workflow test requests, typed identifiers, typed executor kinds, and DTO-shape contracts that are safe for persistence/API boundaries.
- `CanDoItAll.AgentFramework.Core` owns workflow abstractions: workflow definition compiler, workflow runtime manager, workflow event sink, workflow checkpoint bridge, workflow artifact service, external request service, workflow test runner, and process-facing workflow executor contract.
- `CanDoItAll.AgentFramework.Maf` owns MAF-specific implementation: converting CanDoItAll workflow definitions into MAF `Workflow` graphs, binding LLM call components, binding agent steps, mapping MAF events/status/checkpoints to CanDoItAll models, and running through `InProcessExecution` or another MAF execution environment.
- `CanDoItAll.AgentFramework.Persistence` owns durable workflow definitions, component library entries, workflow settings, workflow runs, workflow events, checkpoints, external requests, test run results, and artifacts.
- `CanDoItAll.Modules.AgentFramework` owns the Blazor workflow page, workflow catalog, settings/testing panels, run history, component library UI, and workflow canvas editor.
- `CanDoItAll.Web\Api` exposes workflow API routes and maps them near existing agents/processes APIs.
- `CanDoItAll.Modules.Processes` remains the process orchestrator and only integrates workflows as a typed executor option for roles/assignments.

## Execution Boundary

- MAF supplies the workflow graph and execution primitives.
- MAF DurableTask and Durable Task Scheduler should supply durable orchestration, checkpointing, distributed execution, long-running coordination, and dashboard observability when workflows are production, long-running, or restart-resilient.
- CanDoItAll supplies product run management: run creation policy, product state projections, event observation, checkpoint/run references, external request records, artifacts, cancellation/resume policy, UI/API projection, authorization, audit, and process assignment integration.
- In-process MAF execution is a supported backend only for local development, tests, preview/test runs, and explicitly short non-durable runs unless architecture review accepts a different use.
- A workflow may be exposed as an agent where useful through MAF `AsAIAgent`, but this is an adapter. It must not erase the workflow definition/run identity.
- Azure Functions hosting is an optional deployment boundary to evaluate. Generated workflow endpoints, RequestPort response/status endpoints, and MCP tool triggers may reduce hosting code, but CanDoItAll must decide whether to use, wrap, or disable them based on product API, authorization, audit, and process integration requirements.

## Domain Model Shape

- `WorkflowDefinition`: identity, name, description, version, status, graph, inputs, outputs, settings reference, validation state, audit metadata.
- `WorkflowNode`: typed node id, node kind, display metadata, ports, input bindings, output contract, component reference or inline configuration.
- `WorkflowEdge`: typed edge id, source/target ports, condition, fan-out/fan-in behavior, validation state.
- `LlmCallComponent`: identity, provider/model selection, modality, model settings, instructions, input schema, output/result shape, retry/cancellation policy, safety/tool policy where applicable.
- `WorkflowRun`: identity, definition version, status, session/checkpoint references, current requests, emitted artifacts, event timeline, source process assignment when applicable.
- `WorkflowExternalRequest`: durable human-in-loop/tool-approval/input request with typed request/response shapes and explicit timeout/cancel semantics.

## Runtime Policy

- Use explicit failure states and actionable logs for MAF compile/run/map failures.
- Do not silently fallback to a different provider, model, workflow version, or execution mode.
- Treat workflow definitions as immutable for a run. Editing a definition creates a new version or preserves a snapshot for existing runs.
- Keep process artifacts and workflow artifacts related through references, not by merging their persistence tables unless a review proves this is superior.
- Prefer `ConfigureDurableOptions` when durable workflows and agents are hosted together. Use `ConfigureDurableWorkflows` only where a host is workflow-only.
- Do not reimplement Durable Task scheduling, checkpointing, or orchestration history if MAF DurableTask satisfies the requirement.
- Keep durable orchestration replay constraints in mind: orchestrator paths must avoid blocking calls, non-deterministic logic, and allocation-heavy payload transformations.

## Review Gates

- Phase 1 review must approve the wrapper model, MAF abstraction boundary, runtime ownership, persistence ownership, and typed executor model.
- Phase 2 review must approve workflow runtime reliability, including whether DurableTask/DTS is the default durable backend, before UI and process integration consume it.
- UI reviews must approve page structure, canvas model correctness, component library ergonomics, and browser evidence.
- Final review must verify process orchestration remains above workflows and that all raw architect notes are closed.
