# MAF Source Notes Used For Bundle Preparation

Prepared on 2026-05-28.

## Microsoft Agent Framework Workflows

Key points used:

- Workflows are explicit predefined process flows that can include agents, humans, and external systems.
- Workflow key features include type safety, graph architecture with executors/edges, external integration, checkpointing, and multi-agent orchestration.
- `WorkflowBuilder` is the graph API for fixed topologies with type-validated message routing and superstep execution.
- MAF validates type compatibility, graph connectivity, executor binding, and edge correctness.
- Supersteps run triggered executors concurrently and then synchronize before the next superstep, which affects fan-out/fan-in design.

URLs:

- https://learn.microsoft.com/en-us/agent-framework/
- https://learn.microsoft.com/en-us/agent-framework/workflows/
- https://learn.microsoft.com/en-us/agent-framework/workflows/workflows
- https://learn.microsoft.com/en-us/agent-framework/workflows/executors
- https://learn.microsoft.com/en-us/agent-framework/workflows/edges

## C# executor guidance

Key points used:

- C# workflow executor handlers should preferably be `[MessageHandler]` methods in `partial` classes deriving from `Executor`.
- This source-generated pattern improves handler registration, compile-time validation, performance, and Native AOT compatibility.
- Shared stateful executors should implement reset semantics to avoid stale state between runs.

URL:

- https://learn.microsoft.com/en-us/agent-framework/workflows/executors

## Tool approval and skills

Key points used:

- Approval-required function tools can be wrapped with `ApprovalRequiredAIFunction`.
- Callers must detect `FunctionApprovalRequestContent` and continue approval/rejection loops until all required approvals are handled.
- Agent Skills package reusable instructions/resources/scripts through progressive disclosure.
- `SubprocessScriptRunner` is demonstration-oriented and production use requires sandboxing, resource limits, input validation, allow-listing, logging, and audit trails.

URLs:

- https://learn.microsoft.com/en-us/agent-framework/agents/tools/
- https://learn.microsoft.com/en-us/agent-framework/agents/tools/tool-approval
- https://learn.microsoft.com/en-us/agent-framework/agents/skills

## Package version note

Observed package page:

- `Microsoft.Agents.AI.Workflows` latest displayed version: `1.7.0`, last updated 2026-05-26.
- The repository MAF integration project referenced `Microsoft.Agents.AI.Workflows` `1.6.2` during bundle preparation.

URL:

- https://www.nuget.org/packages/Microsoft.Agents.AI.Workflows
