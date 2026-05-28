# 03-maf-workflow-compiler-and-executor-foundation

## Status

- `Prepared`

## Objective

Introduce or verify the native MAF workflow compiler/adapter and typed executor foundation so CanDoItAll workflow definitions use MAF workflow capabilities during execution.

## Success Criteria

- A single service compiles/adapts repository workflow definitions to native MAF workflows.
- The compiler uses `WorkflowBuilder` and MAF executors/edges rather than custom traversal for execution.
- Runtime payloads use a typed message envelope.
- C# executor adapters use source-generated MAF executor patterns where appropriate: `partial` classes deriving from `Executor` and `[MessageHandler]` methods.
- Stateful shared executors implement reset semantics.
- Tests prove a simple linear workflow, conditional route, switch/fan-out scenario, and LLM/component adapter compilation.

## Covered Inputs

- R02, R05, R06, R07, R09, R10, R11, R12, R15

## Prerequisites

- SB01 and SB02 passed.
- MAF package baseline decision complete.

## Exact Source References

- `src/CanDoItAll.AgentFramework.Maf/`
- Workflow runtime services found by SB01.
- Workflow model and validator from SB02.
- MAF documentation for `WorkflowBuilder`, executors, edges, events, and supersteps.

## Deliverables

- `IWorkflowMafCompiler` or equivalent existing service hardened.
- MAF executor adapters for start/end/strict logic/triage/LLM/human input placeholders as appropriate.
- Typed message envelope and serializer boundary.
- Edge mapping for direct/conditional/switch/fan-out/fan-in semantics currently supported by the repository model.
- Golden tests asserting compiler output behavior via MAF execution, not just DTO snapshots.

## Implementation Steps

1. Identify any existing MAF compiler/runtime code from SB01.
2. Refactor to one compiler/adapter boundary if multiple paths exist.
3. Define typed message envelope and conversion helpers at repository/MAF boundary.
4. Implement MAF executor adapters with `[MessageHandler]` where feasible.
5. Map repository edges/routes to native MAF builder edges and route predicates.
6. Add tests for build-time validation and superstep behavior.
7. Ensure cancellation tokens are propagated.
8. Update proof and execution report.

## Scope Exceptions

- Plugin-specific executors are hardened in SB04; this subbundle may add registry hooks but should not fully migrate all plugins.
- UI migration is SB06.

## Do Not Do

- Do not leave a second custom workflow execution path as the default if native MAF execution succeeds.
- Do not use raw `object` payloads across executor boundaries without an explicit adapter.
- Do not hide route evaluation in untested string expressions.

## Acceptance Checklist

- At least one repository workflow executes through native MAF in a deterministic test.
- Route and edge behavior is covered with tests.
- Executor adapters align with MAF C# executor guidance.
- MAF package/API usage is centralized enough for future upgrades.

## Proof Required

- Compiler/adapter unit tests.
- MAF in-process workflow execution transcript.
- Source assertions for `[MessageHandler]`/executor patterns or documented rationale where not applicable.

## Progression Gate

SB04 may start only after the compiler/adapter and executor activation model are stable enough for plugin executors.

## Suggested Agent Prompt

```text
Implement SB03 only. Build the native MAF compiler/adapter and typed executor foundation. Prove execution through MAF, not through a custom graph traversal shortcut.
```
