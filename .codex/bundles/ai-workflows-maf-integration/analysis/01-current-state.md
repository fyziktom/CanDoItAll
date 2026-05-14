# Current State

## CanDoItAll Architecture Findings

- The solution root is `C:\repositories\CanDoItAll\CanDoItAll.slnx`.
- The current AgentFramework layer is split into models, core contracts, MAF implementation, persistence, components, and hosting projects.
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\CanDoItAll.AgentFramework.Maf.csproj` already references `Microsoft.Agents.AI.Workflows` version `1.3.0`.
- `MafAgentRuntime` currently focuses on agent execution, provider-specific agent creation, sessions, streaming, tool policy, approvals, and persistence snapshots.
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\MafHandoffWorkflowFactory.cs` already builds a MAF handoff workflow and exposes it as an `AIAgent`, proving MAF workflows are already present internally but not modeled as first-class CanDoItAll workflow definitions or workflow runs.
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Contracts\Contracts.cs` defines agent runtime, workspace, event sink, checkpoint bridge, diagnostics, and provider registry contracts. These are likely patterns for workflow equivalents, not necessarily contracts to overload.
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Execution\ExecutionCheckpointServices.cs` already bridges agent execution checkpoints to MAF checkpoint storage. Workflow runtime should reuse the concept but not hide workflow-specific checkpoint and external request semantics.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\AgentsHomePage.razor` is the existing Agents module page. Workflow UI should remain in this module but get its own route/page and page state.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes` contains process definition, process runtime, process canvas, artifacts, assignments, and process launch surfaces. Workflow UI may borrow interaction patterns but cannot share process definitions as its canonical model.
- `ProcessRoleEditorModel.PreferredExecutorKind` and `ProcessRunAssignmentViewModel.ExecutorKind` are currently string-shaped. Workflow integration must replace or wrap this with a strongly typed executor kind because process launch must choose between human/agent/workflow without stringly behavior.
- Web API routes are mapped in `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\ApiEndpointRouteBuilderExtensions.cs` with existing `MapAgentsApi` and `MapProcessesApi`. Workflows need a separate API surface rather than piggybacking on agent endpoints.

## MAF Workflow Findings

- `Microsoft.Agents.AI.Workflows` provides a graph workflow model with `Workflow`, `WorkflowBuilder`, executors, edges, ports, events, runs, streaming runs, checkpoints, and in-process execution environments.
- `WorkflowBuilder` supports direct edges, conditional edges, fan-out, fan-in barriers, output executors, and agent executors.
- `InProcessExecution` exposes `OffThread`, `Concurrent`, `Lockstep`, `RunAsync`, `RunStreamingAsync`, `OpenStreamingAsync`, `ResumeAsync`, and checkpoint-aware overloads. This is a usable runtime core, but it is not a complete application-level run manager.
- `StreamingRun` exposes session id, status, event stream, external response sending, message sending, and cancellation.
- `RunStatus` distinguishes not started, idle, pending requests, ended, and running states. CanDoItAll needs its own persisted workflow run status model that maps deliberately to these states.
- `WorkflowEvent` includes workflow start, output, warning, error, request info, superstep, and executor-level events. These can drive observations, logs, artifacts, and UI timelines.
- Human-in-loop is modeled through request ports, external requests, external responses, and request-info events. CanDoItAll needs durable request records and explicit response handling.
- MAF checkpointing exists through checkpoint managers and stores. CanDoItAll still needs persisted run metadata, checkpoint references, rehydrate/resume policy, and execution history.
- MAF supports using agents as workflow executors and exposing workflows as agents with `AsAIAgent`. This directly supports the product goal of treating workflows and agents as peer execution choices.
- MAF declarative workflow support can build workflows from Foundry-style YAML. This is a possible import/export or advanced authoring path, but the initial CanDoItAll domain model should not become a raw YAML wrapper without architecture review.

## Durable Workflow Article Findings

- The May 6, 2026 .NET Blog article distinguishes the lightweight in-process runner from durable execution. In-process execution is appropriate for quick starts and local development, but real-world agent workflows often need restart survival, long duration, and external observability.
- `Microsoft.Agents.AI.DurableTask` adds durability to MAF workflows without requiring a different workflow definition. The host/runtime changes, not the workflow graph.
- Durable execution uses Durable Task Scheduler (DTS) as the backend for state, checkpoints, orchestration history, coordination, and dashboard observability. The local DTS emulator exposes scheduler and dashboard ports.
- For .NET Generic Host scenarios, `ConfigureDurableWorkflows` registers workflows with the Durable Task runtime. `ConfigureDurableOptions` is the broader API when agents and workflows are registered together, and it auto-registers agents used by workflows.
- The DurableTask workflow client shape is `IWorkflowClient`, with durable run handles and streaming handles. CanDoItAll should adapt this rather than recreate a scheduler when durable execution is required.
- Azure Functions hosting can generate workflow HTTP triggers, orchestration/activity/entity functions, RequestPort response/status endpoints, and optional MCP tool triggers. This is an important hosting option, but CanDoItAll still needs product-level API, authorization, UI, audit, and process integration decisions.
- RequestPort is the durable human-in-loop primitive. Durable hosting can expose generated response and status endpoints, but CanDoItAll should own durable product records, permissions, and UI around pending requests.
- MAF durable workflows support fan-out/fan-in, AI agents as executors, conditional routing, shared state, and sub-workflows; sub-workflows become sub-orchestrations on the durable runtime.

## Planning Conclusion

- Use MAF as the workflow execution engine where possible.
- Prefer MAF DurableTask for durable production/long-running workflow execution when it satisfies the requirements. Use in-process execution only for local development, tests, previews, or explicitly short non-durable runs.
- Build a CanDoItAll workflow domain and runtime management layer around MAF/DurableTask because the application still needs persistence projections, settings, tests, observations, human input UI, artifacts, authorization, audit, and process integration.
- Make the wrapper foundation a hard gate. UI and process integration should not proceed until the first architecture review proves the model boundaries and runtime ownership.
