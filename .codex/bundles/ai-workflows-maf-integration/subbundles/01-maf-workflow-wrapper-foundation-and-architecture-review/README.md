# MAF Workflow Wrapper Foundation And Architecture Review

## Status

- `Completed`

## Objective

- Establish the first-class CanDoItAll workflow domain and MAF wrapper boundary before any workflow UI, persistence-heavy feature work, or process integration starts.
- Decide with source evidence whether workflow runtime orchestration belongs inside existing AgentFramework projects or a new workflow runtime library.
- Produce and record the detailed phase-1 architecture review required by the human architect.

## Success Criteria

- Workflow models, identifiers, component kinds, executor kinds, event kinds, and run states are strongly typed.
- MAF workflow primitives are wrapped behind CanDoItAll contracts instead of leaked directly into persistence/API boundaries.
- Prepared LLM Call Component model shape is defined with provider/model, modality, settings, instructions, input shape, output shape, and validation.
- MAF source-backed runtime decision record is documented, including in-process versus DurableTask/DTS versus Azure Functions hosting.
- Detailed architecture review passes before dependent subbundles proceed.

## Covered Inputs

- RQ-001, RQ-002, RQ-003, RQ-005, RQ-006, RQ-013, RQ-014, RQ-015, RQ-016, RQ-017, RQ-020, RQ-021, RQ-022, RQ-023, RQ-026.
- RN-001, RN-002, RN-003, RN-005, RN-008, RN-011, RN-012, RN-013, RN-014, RN-016, RN-018.

## Prerequisites

- Current bundle README, requirements, architecture, inventories, and traceability are read.
- Local MAF source clone exists at `C:\repositories\agent-framework`.
- No implementation from later workflow UI/process subbundles has started.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\CanDoItAll.AgentFramework.Models.csproj`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Agents\AgentModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Providers\ProviderModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\CanDoItAll.AgentFramework.Core.csproj`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Contracts\Contracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Execution\ExecutionCheckpointServices.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Execution\ExecutionEventServices.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\CanDoItAll.AgentFramework.Maf.csproj`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\MafAgentRuntime.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\MafAgentRuntime.AgentFactory.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\MafAgentRuntime.Session.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\MafHandoffWorkflowFactory.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Workflows\Workflow.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Workflows\WorkflowBuilder.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Workflows\InProcessExecution.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Workflows\StreamingRun.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Workflows\WorkflowEvent.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Workflows\RunStatus.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Workflows\WorkflowHostingExtensions.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Workflows\WorkflowHostAgent.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Workflows.Declarative\DeclarativeWorkflowBuilder.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.DurableTask\ServiceCollectionExtensions.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.DurableTask\Workflows\IWorkflowClient.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.DurableTask\Workflows\DurableWorkflowClient.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.DurableTask\Workflows\DurableWorkflowOptions.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Hosting.AzureFunctions\Workflows\DurableWorkflowOptionsExtensions.cs`

## Deliverables

- Add or update workflow domain models in the AgentFramework model layer for definitions, graph nodes, edges, ports, component references, settings references, validation issues, run state projections, event kinds, artifacts, and external requests.
- Add typed identifiers/value objects or strongly typed wrappers for workflow id, workflow version id, workflow node id, workflow edge id, workflow component id, workflow run id, workflow request id, workflow artifact id, and workflow executor kind.
- Add Core contracts for workflow compilation, workflow runtime management, workflow event mapping, workflow checkpoint handling, workflow artifact handling, workflow external request handling, and workflow-as-agent/process executor adaptation.
- Add MAF-specific adapter contracts/classes that map CanDoItAll workflow models to MAF `Workflow` graphs and MAF events/statuses back to CanDoItAll models.
- Add runtime backend abstractions that can represent in-process, DurableTask/DTS, and Azure Functions-hosted workflow execution without changing CanDoItAll workflow definitions.
- Define the initial prepared LLM Call Component contract, including provider/model, modality, model settings, instruction template, input shape, result shape, validation, and allowed tool/agent policy.
- Add a policy decision that in-process execution is for local development, tests, previews, and approved short non-durable runs, while durable production/long-running workflows should use MAF DurableTask/DTS when it meets requirements.
- Add a performance review checklist for workflow runtime/API hot paths based on `analysis/03-article-and-performance-review.md`.
- Produce a phase-1 architecture review document or execution-report section that approves or rejects the chosen project/library boundary.

## Dependency Impact

- Subbundle 02 depends on the runtime contracts and MAF adapter boundary.
- Subbundle 03 depends on workflow/component/settings model shape.
- Subbundle 04 and 05 depend on stable UI view models and typed graph primitives.
- Subbundle 06 depends on the typed workflow executor kind and workflow-as-agent/process adapter boundary.
- If this subbundle is weak, later work will spread incompatible model assumptions across persistence, API, UI, and process runtime.

## Validation Depth

- Critical foundation with detailed architecture review.
- Build and test proof is required for all touched AgentFramework projects.
- Review proof must include a source-backed MAF runtime capability decision, DurableTask/DTS decision, and performance hot-path review.

## Implementation Steps

1. Re-read the listed CanDoItAll AgentFramework files and MAF workflow files.
2. Write a short decision record in the execution report describing MAF runtime capabilities, DurableTask/DTS capabilities, what CanDoItAll wraps, and what CanDoItAll must own.
3. Add strongly typed workflow identifiers, enums/discriminated kinds, and DTO/domain models in the model layer.
4. Add Core workflow interfaces and records without binding Core to concrete MAF runtime types.
5. Add MAF adapter skeletons or helpers that compile CanDoItAll workflow definitions to MAF workflows and map MAF statuses/events to CanDoItAll statuses/events.
6. Add LLM Call Component model and validation contract.
7. Add a performance guard section to the runtime design for async, event streaming, serialization, status polling, and graph validation hot paths.
8. Add focused unit tests for model validation, status/event mapping, component validation, and MAF graph compile error handling.
9. Run build/tests for touched projects.
10. Perform the detailed phase-1 architecture review using `shared-prompts/architecture-review-prompt.md`.
11. Apply any required review fixes before marking this subbundle complete.
12. Update `reviews/01-execution-report.md` with proof, review results, and phase-1 gate status.

## Scope Exceptions

- Do not implement full workflow persistence, runtime hosting, UI, canvas editing, API routes, or process launch integration here.
- Do not decide all final UX details here; only define stable domain and runtime boundaries.

## Do Not Do

- Do not expose raw MAF `Workflow`, `WorkflowEvent`, `RunStatus`, or checkpoint objects as persistence or API contracts.
- Do not reuse process definition models as canonical workflow models.
- Do not leave process executor kind as a new magic string.
- Do not add a fallback provider/model/execution mode when workflow compilation or execution fails.
- Do not reimplement Durable Task scheduling/checkpoint/orchestration history if MAF DurableTask satisfies the requirement.
- Do not start Agents module workflow UI work before the architecture gate passes.

## Acceptance Checklist

- Workflow domain model has typed ids and typed state/kind fields.
- Core contracts describe workflow compile/run/event/checkpoint/external-request/artifact responsibilities.
- MAF adapter boundary is isolated to MAF implementation project or an explicitly reviewed workflow runtime library.
- LLM Call Component model covers provider/model, modality, settings, instructions, input shape, output shape, and validation.
- Runtime design states when to use in-process, DurableTask/DTS, or Azure Functions hosting.
- Runtime design includes performance guardrails for event streaming, polling/status, serialization, and graph validation.
- Architecture review records project ownership, persistence/API boundary, MAF runtime decision, and follow-up edits.
- Execution report marks the phase-1 architecture gate as passed or blocked with concrete reasons.

## Proof Required

- `dotnet build C:\repositories\CanDoItAll\CanDoItAll.slnx`
- Focused test command for touched AgentFramework test projects, or a documented reason if no test project exists yet plus added test project/task in the review.
- Execution report excerpt summarizing MAF source findings and boundary decision.
- Execution report excerpt summarizing DurableTask/DTS and Azure Functions hosting decision.
- Performance scan/review excerpt based on `analysis/03-article-and-performance-review.md`.
- Architecture review findings with blocking/non-blocking classification and final gate decision.

## Browser Validation Logging

- N/A - this subbundle has no browser-visible surface.
- Execution report must still record `N/A` for browser route, viewport, Playwright evidence, screenshots, and result.

## Progression Gate

- Downstream subbundles may not start until the detailed phase-1 architecture review has no unresolved blocking findings, the workflow wrapper/model boundary is accepted, and the in-process/DurableTask/Azure Functions runtime policy is accepted.

## Suggested Agent Prompt

```text
Implement subbundle 01 only.
Focus on workflow domain models, Core contracts, MAF adapter boundaries, LLM Call Component shape, and the phase-1 architecture review.
Do not implement UI, persistence-heavy runtime, web API routes, or process integration.
Use the local MAF source references listed in this subbundle and update reviews/01-execution-report.md before closing.
```
