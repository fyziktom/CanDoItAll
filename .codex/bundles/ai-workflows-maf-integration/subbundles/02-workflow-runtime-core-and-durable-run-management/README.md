# Workflow Runtime Core And Durable Run Management

## Status

- `Completed`

## Objective

- Build the CanDoItAll workflow runtime manager around MAF execution primitives so workflow runs are durable, observable, cancellable, resumable, and safe for process/UI/API consumers.
- Prefer MAF DurableTask/DTS for production, long-running, distributed, or restart-resilient runs when it satisfies requirements; keep in-process execution for tests, previews, local development, and approved short non-durable runs.
- Prove what CanDoItAll owns above DurableTask: product run records/projections, artifacts, authorization, audit, UI/API state, and process references.

## Success Criteria

- Workflow runs have product lifecycle records, event timeline/projections, DurableTask run references where applicable, external request records, artifact references, cancellation state, and resume/respond behavior.
- MAF streaming/non-streaming execution maps cleanly to CanDoItAll run states and observations.
- DurableTask `IWorkflowClient`/run handles are evaluated and used for durable backend unless a review documents a concrete blocker.
- DTS emulator or equivalent durable backend proof exists for local durable validation.
- Parallel workflow runs are safe and do not reuse MAF workflow/executor instances in a way that violates ownership/concurrency constraints.
- Architecture review confirms runtime ownership before UI/process subbundles consume the runtime.

## Covered Inputs

- RQ-001, RQ-002, RQ-003, RQ-011, RQ-012, RQ-013, RQ-017, RQ-018, RQ-020, RQ-021, RQ-022, RQ-023, RQ-024, RQ-025, RQ-026.
- RN-001, RN-002, RN-003, RN-008, RN-011, RN-014, RN-015, RN-016, RN-017, RN-018.

## Prerequisites

- Subbundle 01 completed.
- Phase-1 architecture review passed or explicitly approved runtime follow-up edits.
- Workflow model and MAF adapter contracts exist.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Contracts\Contracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Execution\ExecutionCheckpointServices.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Execution\ExecutionEventServices.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\MafAgentRuntime.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\MafAgentRuntime.Session.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\CanDoItAll.AgentFramework.Persistence.csproj`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Workflows\InProcessExecution.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Workflows\Run.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Workflows\StreamingRun.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Workflows\WorkflowSession.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Workflows\IWorkflowExecutionEnvironment.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Workflows\RequestPort.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Workflows\ExternalRequest.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Workflows\ExternalResponse.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Workflows\Checkpointing\FileSystemJsonCheckpointStore.cs`
- `C:\repositories\agent-framework\dotnet\src\Shared\Workflows\Execution\WorkflowRunner.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.DurableTask\ServiceCollectionExtensions.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.DurableTask\Workflows\IWorkflowClient.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.DurableTask\Workflows\IWorkflowRun.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.DurableTask\Workflows\IStreamingWorkflowRun.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.DurableTask\Workflows\DurableWorkflowClient.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.DurableTask\Workflows\DurableWorkflowRun.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.DurableTask\Workflows\DurableStreamingWorkflowRun.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.DurableTask\Workflows\DurableWorkflowOptions.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.DurableTask\Workflows\DurableWorkflowRunner.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.DurableTask\Workflows\DurableWorkflowLiveStatus.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.DurableTask\Workflows\DurableWorkflowWaitingForInputEvent.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.DurableTask\Workflows\PendingRequestPortStatus.cs`

## Deliverables

- Workflow runtime manager service that starts, observes, cancels, resumes/responds, and completes workflow runs through the Core runtime contract.
- DurableTask backend implementation using `Microsoft.Agents.AI.DurableTask`, `IWorkflowClient`, Durable Task worker/client registration, and DTS for production-style durable runs unless architecture review documents why another backend is required.
- In-process backend implementation or adapter for local/test/preview runs only.
- Persistence entities/configurations or equivalent product storage for workflow runs, run events/projections, DurableTask run references, external requests, test runs, and artifacts, following the boundary approved in subbundle 01.
- MAF runtime implementation that maps MAF/DurableTask sessions/events/status/checkpoints to CanDoItAll records.
- External request handler for human-in-loop and approval-like workflow pauses.
- Artifact capture path for workflow step outputs and generated files/structured outputs.
- Runtime tests proving normal completion, failure, cancellation, pending RequestPort/external request, external response resume, durable run reference capture, and concurrent run isolation.
- Local durable validation plan using DTS emulator or documented equivalent, including dashboard/status observability.
- Performance review of runtime hot paths: event streaming, status polling, serialization, graph validation, and external request response.

## Dependency Impact

- Subbundle 03 depends on runtime test runner and workflow run projections.
- Subbundle 04 depends on run history and external request state for the workflow page.
- Subbundle 05 depends on validation/preview execution and event projection.
- Subbundle 06 depends on process-triggered workflow run APIs and durable status.
- Subbundle 07 depends on stable runtime service/API behavior.

## Validation Depth

- Critical runtime foundation.
- Requires build, unit/integration tests, DurableTask/DTS proof or documented blocker, performance review, and an architecture review focused on run management, concurrency, checkpointing, and human-in-loop durability.
- Execution result: runtime manager, in-process MAF backend, product run/event/external-request/artifact stores, pause/respond behavior, explicit backend rejection, and concurrent run isolation were implemented and validated. DurableTask/DTS smoke is explicitly blocked for this subbundle because no product DTS host/client registration exists yet; subbundle 07 remains responsible for durable host/API integration and durable smoke proof before production closure.

## Implementation Steps

1. Review subbundle 01 architecture decision and update this subbundle if approved boundaries changed.
2. Model product workflow run, event/projection, durable backend reference, external request, artifact, and test-run storage.
3. Implement workflow runtime manager service in the approved Core/Maf/Persistence split.
4. Add DurableTask backend evaluation and implementation path using `ConfigureDurableOptions` where agents and workflows are hosted together.
5. Keep in-process backend scoped to tests/previews/local short runs.
6. Map MAF and DurableTask statuses/events to CanDoItAll run/event states.
7. Implement start/run/stream/cancel/resume/respond flows with explicit failure handling.
8. Ensure each run gets a fresh or concurrency-safe MAF workflow/session/executor setup according to MAF ownership rules.
9. Add artifact capture and external request persistence.
10. Add tests for success, failure, cancellation, pending request, response resume, durable backend reference capture, and concurrent run isolation.
11. Add performance review notes and targeted scans for runtime/event/status code.
12. Run build/tests and DTS emulator or equivalent durable smoke when available.
13. Run an architecture review for runtime ownership and durability.
14. Update the execution report.

## Scope Exceptions

- Do not build the full workflow page or canvas editor here.
- Do not integrate process roles here beyond defining the process-facing runtime contract if needed.
- Do not implement Azure Functions hosting here beyond evaluating/adapting runtime contracts unless architecture review makes it the selected host for this phase.

## Do Not Do

- Do not treat MAF checkpoint files as the only workflow run record.
- Do not build a custom durable scheduler when MAF DurableTask/DTS meets the durable execution requirements.
- Do not store opaque event JSON without typed event kind and queryable state.
- Do not silently continue a workflow after a failed external request response.
- Do not assume one `Workflow` instance can be reused concurrently unless MAF source and tests prove the executor bindings allow it.
- Do not put blocking calls, sync-over-async, or replay-unsafe logic in durable orchestration paths.

## Acceptance Checklist

- Workflow run manager exposes explicit start, get status, get events, cancel, resume, and respond-to-request operations.
- Runtime has a documented DurableTask/DTS backend decision and an in-process backend policy.
- Run events/projections, durable backend references, external requests, and artifacts are durable or have a documented storage implementation.
- Runtime maps MAF statuses/events explicitly.
- Human-in-loop request can pause and resume a workflow.
- Concurrency tests prove isolated parallel runs.
- Performance review covers runtime/event/status hot paths.
- Architecture review accepts the runtime model.

## Proof Required

- `dotnet build C:\repositories\CanDoItAll\CanDoItAll.slnx`
- Runtime-focused test command covering workflow run manager behavior.
- Test output proving external request pause/resume and concurrent run isolation.
- DTS emulator or equivalent durable smoke proof, or a documented blocker with follow-up task.
- Performance scan/review notes for new runtime/API hot paths.
- Execution report runtime architecture review notes.

## Browser Validation Logging

- N/A - this subbundle has no browser-visible surface.
- Execution report must record `N/A` for browser route, viewport, Playwright evidence, screenshots, and result.

## Progression Gate

- UI, API, and process workflow integration may not proceed until the workflow runtime manager can start, observe, cancel, resume/respond, persist product workflow state, and prove or explicitly block DurableTask/DTS durable execution with passing tests.

## Suggested Agent Prompt

```text
Implement subbundle 02 only.
Build durable workflow run management around the subbundle 01 wrapper boundary.
Prove events, checkpoints, artifacts, human-in-loop requests, cancellation, resume, and concurrency.
Do not implement workflow UI, canvas, or process assignment integration.
```
