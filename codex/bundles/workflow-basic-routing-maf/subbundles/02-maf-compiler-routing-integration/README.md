# MAF Compiler Routing Integration

## Status

- `Completed`

## Objective

- Compile the new workflow routing contract into Microsoft Agent Framework workflow primitives so basic routing executes inside the MAF workflow graph.
- Replace the current behavior where `ConditionExpression` is passed to the non-generic `AddEdge` overload as a label-like string and therefore does not act as an executable predicate.
- Provide a deterministic built-in JSON route evaluator that can later be replaced or extended by ARTL through a narrow compiler interface.

## Success Criteria

- Direct edges still compile with the current non-conditional MAF edge path.
- Predicate edges compile through `builder.AddEdge<WorkflowNodeInput>(..., Func<WorkflowNodeInput?, bool>, ...)`.
- Switch case/default groups compile through `builder.AddSwitch(...)` with predictable case order and one default.
- Fan-out selector groups compile through `builder.AddFanOutEdge<WorkflowNodeInput>(...)` with stable target ordering.
- Runtime tests prove that false branches are skipped, switch default is honored, and fan-out invokes only selected target executors.

## Covered Inputs

- User requirement: use MAF's built-in prepared workflow routing for simple IF/ELSE, SWITCH, and related routing.
- Current-state finding: `MafWorkflowCompiler` currently calls `AddEdge(source, target, string conditionExpression, idempotent: true)` for every edge.
- MAF reference baseline: conditional edge predicates, switch-case routing, and multi-selection/fan-out routing are available in the current Microsoft Agent Framework workflow samples/API.
- Architecture requirement: route evaluation must occur against `WorkflowNodeInput.PayloadJson` and remain deterministic.

## Prerequisites

- Subbundle 01 completed with route contract and validation tests passing.
- MAF package reference and API signatures confirmed in `CanDoItAll.AgentFramework.Maf.csproj`.
- Runtime test harness can invoke in-process preview workflows with fake executors or instrumentation.

## Exact Source References

- `C:\repositories\CanDoItAll/src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowCompiler.cs`
- `C:\repositories\CanDoItAll/src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafInProcessWorkflowExecutionBackend.cs`
- `C:\repositories\CanDoItAll/src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`
- `C:\repositories\CanDoItAll/src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowExecutorContracts.cs`
- `C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Unit/WorkflowExecutorTests.cs`
- `C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Unit/WorkflowFoundationTests.cs`

## Deliverables

- `IWorkflowRoutingCompiler` abstraction in Core or a clearly MAF-neutral workflow runtime namespace.
- `BuiltInJsonWorkflowRoutingCompiler` and a small evaluator for the approved JSON path/operator subset.
- `MafWorkflowCompiler` routing-group logic that chooses `AddEdge<T>`, `AddSwitch`, and `AddFanOutEdge<T>` based on `WorkflowEdge.Routing`.
- Compile-time failures for unsupported route languages, malformed route settings, duplicate switch defaults, and invalid fan-out target indices.
- Unit/runtime tests demonstrating real branch execution behavior.

## Dependency Impact

- Subbundle 03 may expose route authoring only after this subbundle proves authored routes will execute correctly.
- Subbundle 04 API/persistence tests must include definitions that this compiler can execute.
- Subbundle 05 closure depends on the runtime proof to distinguish actual MAF routing from cosmetic UI labels.

## Validation Depth

- `Critical runtime foundation`: route predicates, switch grouping, and fan-out target selection must be proven by execution tests, not just by graph construction tests.

## Implementation Steps

1. Add `IWorkflowRoutingCompiler`, `WorkflowCompiledRoute`, and `WorkflowCompiledFanOutRoute` in a MAF-neutral layer.
2. Implement `BuiltInJsonWorkflowRoutingCompiler` using `System.Text.Json` and the limited JSON path/operator matrix from `architecture/01-target-solution.md`.
3. Register/inject the routing compiler into `MafWorkflowCompiler`; default to the built-in compiler only when dependency injection has not supplied another compiler.
4. Replace the flat edge loop in `MafWorkflowCompiler` with the grouping algorithm in `architecture/02-compiler-grouping-algorithm.md`.
5. Ensure `ConditionExpression` is only used as a label or legacy metadata unless an explicit legacy parser is implemented and tested.
6. Add deterministic ordering for switch cases and fan-out targets based on edge order plus `FanOutTargetIndex` where provided.
7. Add tests with instrumented executors that count invocations and capture payloads for true/false predicate branches.
8. Add switch tests for first matching case and default behavior.
9. Add fan-out tests for zero, one, and multiple selected targets if MAF semantics allow zero-target selection; otherwise document and validate the non-zero constraint.
10. Run targeted unit/runtime tests and record proof.

## Scope Exceptions

- Do not implement production DurableTask/DTS host proof here unless the current project already exposes it.
- Do not implement an advanced JSONPath library or ARTL parser.
- Do not use the UI to verify compiler behavior; UI proof is separate.

## Do Not Do

- Do not continue using the string-only `AddEdge` overload for conditional routes.
- Do not compile unsupported route languages as direct edges.
- Do not rely on branch labels as route predicates.
- Do not let predicate exceptions crash the workflow without a clear compile/validation error when the issue is statically detectable.

## Acceptance Checklist

- Direct, predicate, switch, and fan-out routes compile through the intended MAF APIs.
- Runtime tests prove branch inclusion/exclusion behavior using observable executor invocations.
- Invalid route definitions fail before workflow build or at build with a clear `WorkflowCompilationResult` error.
- `MafWorkflowStatusMapper` and existing execution events remain compatible.
- No regression to simple direct workflow preview execution.

## Proof Required

- `dotnet test C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~WorkflowExecutorTests|FullyQualifiedName~WorkflowFoundationTests" --verbosity minimal -m:1`
- Include test output or execution-report rows showing predicate false branch skipped, switch default selected, and fan-out target selection honored.
- Add a code-review note in `reviews/01-execution-report.md` confirming the compiler no longer treats `ConditionExpression` as an executable predicate.

## Browser Validation Logging

- `N/A`: this subbundle is runtime/compiler only.
- Browser proof remains blocked until subbundle 03 creates route-authoring UI.

## Progression Gate

- Subbundle 03 may begin only after runtime tests demonstrate that a route authored in the workflow model changes actual MAF execution behavior.

## Suggested Agent Prompt

```text
Implement subbundle 02 only.
Compile WorkflowEdge.Routing into MAF AddEdge<T>, AddSwitch, and AddFanOutEdge<T>. Prove actual runtime branching with instrumented tests. Do not use the string AddEdge overload for executable conditional logic and do not implement ARTL.
```
