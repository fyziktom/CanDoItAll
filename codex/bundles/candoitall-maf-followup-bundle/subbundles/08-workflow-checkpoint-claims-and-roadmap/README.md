# Subbundle 08 — Workflow checkpoint claims and roadmap

## Problem

The repository references `Microsoft.Agents.AI.Workflows` and uses `FileSystemJsonCheckpointStore` to bridge pending approval checkpoints. That is useful, but it is not the same as full MAF workflow orchestration of the process graph.

## Required change

Update documentation and add a small roadmap that distinguishes:

1. Current state: custom process dispatcher + MAF agent runtime + MAF checkpoint store for pending approval state.
2. Near-term target: selected process subflows wrapped/adapted as MAF workflows where this improves typed routing, checkpointing, and HITL.
3. Non-goal: rewriting the whole process engine prematurely.

## Optional implementation seam

Add a small interface/adapter for future MAF workflow execution, for example:

```csharp
public interface IAgentProcessWorkflowAdapter
{
    bool CanHandle(ProcessDefinition definition);
    Task<ProcessWorkflowExecutionResult> ExecuteAsync(...);
}
```

Do not implement a broad rewrite unless explicitly approved.

## Tests/docs

- Docs must stop implying full workflow orchestration if only checkpoint bridging is implemented.
- Checkpoint bridge tests should prove structured output contract metadata survives capture/resume.
- If an adapter seam is added, test that the current process dispatcher remains the default path.
