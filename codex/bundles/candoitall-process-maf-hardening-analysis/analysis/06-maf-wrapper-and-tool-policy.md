# MAF wrapper and tool policy analysis

## Result summary persistence

AgentFramework initializes `ExecutionRunRecord.ResultSummary` as empty and later sets it from `response.ResponseText` or failure summary.

Relevant files:

- `src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.Helpers.cs:461-488`
- `src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs:1059-1073`
- `src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs:386-394`
- `src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs:913-920`

For process runs, `ResultSummary` should always contain a compact structured process outcome summary, even when final structured output parsing happens separately. Operator diagnostics currently expect JSON beginning with `{` to extract `status`, `reason`, `branchOutcomeKey`, `nextActions`, and `evidenceRefs`.

Recommended fix:

- store the final `ProcessStepOutcomeResult` JSON or compact projection in `ResultSummary`,
- if the model response is repaired/validated into structured output, persist the repaired structured output,
- if the run fails before structured output, persist a compact failure JSON with `status: "Blocked"`, `reason`, failed tool receipts, and safe evidence refs,
- keep raw model text in separate detail/artifact if needed.

## Tool preflight gap

`AgentProcessReadinessEvaluator` checks agent-level access/capability metadata, but actual runtime tool availability depends on composed providers and scoped process access.

Recommended fix:

1. Add a composed-tool preflight service near dispatch, after the exact `AgentRuntimeContext` is known.
2. It should return:
   - required tool name,
   - provider key,
   - composed/not composed,
   - authorized/not authorized,
   - reason if denied,
   - remediation.
3. If missing, do not launch the agent. Return a deterministic `NeedsManager` diagnostic:

```text
process.runtime.required_tool_not_composed
Step prepare-solution-skeleton requires project_structure_process_subprocess_launch, but it was not composed for governed process context <run>/<step>.
```

This will avoid wasting a full LLM run and producing vague “missing tool” loops.

## Tool receipt contract

For steps with hard tool requirements, the runtime should distinguish:

- tool required because the step must execute it,
- tool optional but allowed,
- tool required only if a launch variable declares the path,
- runtime-owned tool not exposed to the agent.

This distinction is currently blurred by `AllowedOperations`, `RequiredReceipts`, launch context and prose.
