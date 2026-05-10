# MAF Workflow Capability Inventory

## Source Files Reviewed

- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Workflows\Workflow.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Workflows\WorkflowBuilder.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Workflows\InProcessExecution.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Workflows\Run.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Workflows\StreamingRun.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Workflows\WorkflowSession.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Workflows\IWorkflowExecutionEnvironment.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Workflows\RunStatus.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Workflows\WorkflowEvent.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Workflows\RequestPort.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Workflows\ExternalRequest.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Workflows\ExternalResponse.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Workflows\AIAgentExtensions.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Workflows\WorkflowHostingExtensions.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Workflows\WorkflowHostAgent.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Workflows\Checkpointing\FileSystemJsonCheckpointStore.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Workflows\Visualization\WorkflowVisualizer.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Workflows.Declarative\DeclarativeWorkflowBuilder.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Workflows.Declarative\DeclarativeWorkflowOptions.cs`
- `C:\repositories\agent-framework\dotnet\src\Shared\Workflows\Execution\WorkflowRunner.cs`
- `C:\repositories\agent-framework\dotnet\src\Shared\Workflows\Execution\WorkflowFactory.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.DurableTask\ServiceCollectionExtensions.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.DurableTask\Workflows\IWorkflowClient.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.DurableTask\Workflows\IWorkflowRun.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.DurableTask\Workflows\IStreamingWorkflowRun.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.DurableTask\Workflows\DurableWorkflowClient.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.DurableTask\Workflows\DurableWorkflowOptions.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.DurableTask\Workflows\DurableWorkflowRunner.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Hosting.AzureFunctions\Workflows\DurableWorkflowOptionsExtensions.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Hosting.AzureFunctions\Workflows\DurableWorkflowsFunctionMetadataTransformer.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Hosting.AzureFunctions\BuiltInFunctions.cs`

## Capabilities To Wrap

- Graph construction with executor nodes, edges, conditional routing, fan-out, fan-in, start executor, output executor, and validation.
- Execution through in-process environments with lockstep, concurrent, and off-thread modes.
- Streaming and non-streaming runs, run status inspection, cancellation, external response sending, and message sending.
- Workflow events for started, output, warning, error, superstep, executor invocation/completion/failure, request info, and orchestration-specific telemetry.
- Checkpoint manager/store usage for pause/resume and crash/restart recovery.
- Human-in-loop through request ports, external requests, and external responses.
- Agents as workflow executors and workflows exposed as agents.
- Visualization through Mermaid/DOT output for debugging and canvas validation.
- Declarative YAML build support as a possible import/export path, not necessarily as the initial canonical model.
- Durable execution through `Microsoft.Agents.AI.DurableTask`, `IWorkflowClient`, durable run handles, Durable Task worker/client registration, and DTS-backed checkpoint/orchestration history.
- Unified durable registration through `ConfigureDurableOptions` for agents plus workflows, with workflow-only `ConfigureDurableWorkflows` as the narrower option.
- Azure Functions hosting that can generate workflow run endpoints, RequestPort respond/status endpoints, orchestration/activity/entity functions, and optional MCP tool triggers.

## Gaps CanDoItAll Must Own

- Durable workflow definition and version model.
- Durable workflow run records and event history.
- Durable external request records and UI/API response flow.
- Artifact capture and relation to workflow steps and process runs.
- Provider/model/settings policy tied to CanDoItAll provider registry.
- UI projections and browser-verifiable workflow authoring/testing.
- Process role assignment integration.
- Architecture review and migration policy around persistence/API boundaries.
- Product API and authorization decisions around generated Functions endpoints and optional MCP exposure.
- Product projections around DTS dashboard/run status so users do not need to leave CanDoItAll for normal workflow monitoring.
