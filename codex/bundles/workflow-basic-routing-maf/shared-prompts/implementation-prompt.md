# Implementation Prompt

```text
You are implementing the workflow-basic-routing-maf bundle in CanDoItAll.
Work only on the current subbundle unless the progression gate explicitly requires a prerequisite repair.
Use Microsoft Agent Framework built-in workflow routing primitives for this phase: AddEdge<T> for predicates, AddSwitch for switch/default routing, and AddFanOutEdge<T> for multi-selection routing.
Do not implement ARTL yet. Add only the route-language seam and reject unsupported artl-v1 routes until a later ARTL compiler exists.
Do not evaluate arbitrary code or user-supplied scripts. Built-in routes must use deterministic JSON payload checks over WorkflowNodeInput.PayloadJson.
Update reviews/01-execution-report.md with exact commands, results, browser proof, and blockers before stopping.
```
