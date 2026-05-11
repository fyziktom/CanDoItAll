# MAF compiler runtime policy and artifacts

## Status

- `Completed`

## Objective

- Wire executor nodes into MAF in-process execution and centralize timeout, retry, failure, event, and artifact behavior.

## Success Criteria

- `MafWorkflowCompiler` calls `IWorkflowExecutorInvoker` for executor nodes instead of pass-through functions.
- Invoker applies validated timeout/retry policy once for all executors.
- Failures include node id, executor id, attempt count, timeout, and sanitized settings summary.
- Executor outputs can become workflow execution results and artifact/event records where supported.

## Covered Inputs

- R02, R03, R11, R12, R16.

## Prerequisites

- Subbundle 01 contracts compile.
- Subbundle 02 spreadsheet executor is available.
- Subbundle 03 storage/HTTP and service-unavailable behavior are available.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Workflows\MafWorkflowCompiler.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Workflows\MafInProcessWorkflowExecutionBackend.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowContracts.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Workflows\FunctionExecutor.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Workflows\ExecutorBindingExtensions.cs`

## Deliverables

- MAF compiler executor binding that calls the invoker.
- Shared invoker implementation with timeout/retry/cancellation.
- Runtime tests proving an executor was invoked and failures do not become pass-through output.
- Event/artifact mapping updates if existing stores support them.

## Dependency Impact

- Subbundle 06 cannot honestly run scenario validation until this phase proves executor execution.
- UI proof in subbundle 05 is incomplete if created executor nodes cannot run.

## Validation Depth

- `Critical runtime foundation`

## Implementation Steps

1. Add invoker dependency to MAF compiler with compatibility for existing tests/DI.
2. Replace pass-through binding for executor nodes with a function executor delegate that calls `IWorkflowExecutorInvoker`.
3. Keep pass-through behavior only for legacy node kinds that still intentionally simulate execution.
4. Apply timeout/retry in the invoker using cancellation tokens and no silent fallback.
5. Wire output payload shape and artifact/event recording.
6. Add workflow execution tests for success, failure, timeout/retry, and unknown executor validation.

## Scope Exceptions

- Production DurableTask hosting setup is out of scope unless already present and trivial to reuse.
- Distributed retries/checkpoints are left to durable host integration later; this phase provides compatible executor semantics.

## Do Not Do

- Do not swallow executor exceptions into success payloads.
- Do not duplicate retry loops inside individual executors.
- Do not change unrelated workflow node behavior.

## Acceptance Checklist

- A test executor returns a distinct payload proving invocation.
- A throwing executor fails predictably.
- Invalid executor settings never reach the concrete executor.
- Retry count and timeout settings are tested.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter WorkflowExecutor`
- Execution report entry showing pass-through was replaced for executor nodes.

## Browser Validation Logging

- `N/A`

## Progression Gate

- Subbundle 06 may start only after MAF runtime tests prove real executor invocation and failure behavior.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
