# Process Workflow Architecture Review

## Status

- `Passed`

## Scope

- Reviewed subbundle 06 process role workflow integration after implementation.
- Focused on typed executor selection, process ownership, workflow runtime boundary, artifact/request projection, persistence compatibility, and runtime/API performance.

## Reviewed Evidence

- `src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionEnums.cs`
- `src/CanDoItAll.Modules.Processes/Persistence/Entities/ProcessRuntimeModels.cs`
- `src/CanDoItAll.Modules.Processes/Persistence/Configurations/ProcessRuntimeEntityConfigurations.cs`
- `src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.RunStart.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessWorkflowRunCoordinator.cs`
- `src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowProcessExecutorBridge.cs`
- `src/CanDoItAll.Web/Api/ProcessesApi.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessWorkflowExecutorIntegrationTests.cs`

## Decisions

| Topic | Decision | Rationale |
| --- | --- | --- |
| Executor model | Accepted | Process executor selection is represented with `ProcessExecutorKind` and normalized through `ProcessExecutorKindNames`; workflow support is not scattered as ad hoc string comparisons. |
| Process ownership | Accepted | Processes remain the outer orchestrator. A process step starts/observes a workflow through `IWorkflowProcessExecutorBridge` and records a link; it does not own MAF node execution or duplicate workflow event storage. |
| Runtime boundary | Accepted | `CanDoItAll.Modules.Processes` depends on product workflow contracts and runtime manager abstractions, not raw MAF runtime types. The selected backend is opaque to process code. |
| Artifact/request visibility | Accepted | Workflow run references and workflow artifacts are projected into process artifacts. Workflow waiting-for-input state maps to `WaitingApproval` and remains visible in the process execution ledger. |
| API integration | Accepted after fix | Process run detail API now includes `WorkflowRuns`, and the scoped assignment-resolution API preserves workflow definition/version ids. |
| Migration compatibility | Accepted | New workflow executor fields are nullable, existing executor kind values remain normalized, and workflow run links are stored in a dedicated table with explicit foreign keys. |

## Performance Review

- Scan scope: workflow runtime bridge, process workflow coordinator, process runtime read projections, run-start assignment logic, launch candidate discovery, `WorkflowsApi`, and `ProcessesApi`.
- Checklist: sync-over-async, blocking waits, `Task.Run`, `Thread.Sleep`, culture-sensitive case conversion, per-call regex, compiled regex misuse, JSON serialization hot spots, string comparison, LINQ allocation/query paths.
- Result: no required code changes. Two blocking-wait hits were false positives on property names (`ResultSummary`, `ResultShapeKind`). String `Contains` uses either EF/set membership or explicit `StringComparison.OrdinalIgnoreCase`. The only flagged set lookup uses a `HashSet<string>` with `StringComparer.OrdinalIgnoreCase`.
- LINQ usage is acceptable in EF queries, read-model projection, and UI/API filtering paths. In-memory ordering is deliberate where SQLite `DateTimeOffset` ordering is not safely translatable.

## Gate Result

- Subbundle 06 passes. Workflow-backed process role assignment is proven by service/runtime tests, API projection tests, migrations, and browser proof.
- Production durable execution remains outside this subbundle and is tracked in the final architecture review.
