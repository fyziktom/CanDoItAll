# Subbundle 05 — Finalizer sequence invariant

## Goal

Make required finalizer calls observable and optionally enforce that no state-changing/validation tool runs after the finalizer.

## Problem

Required finalizer validation ensures exactly one matching finalizer invocation. It does not currently prove that the finalizer was the last significant action before completion.

A risky sequence could be:

1. Mutation tool.
2. Finalizer tool submits “Completed”.
3. Another mutation or validation tool runs.
4. Assistant returns final JSON.

The finalizer result may no longer represent the actual post-tool state.

## Required implementation

This subbundle is recommended but can be implemented after the critical items.

1. Capture all function/tool invocations in runtime response metadata.

Suggested model:

```csharp
public sealed record AgentToolInvocationTrace(
    string ToolName,
    ToolInvocationClassification Classification,
    int Sequence,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    bool Succeeded);
```

2. Add `ToolInvocationTraces` to `AgentRuntimeResponse` or a separate metadata object.

3. In function-calling middleware, record every invocation sequence and classification.

4. Extend required finalizer validation:

- Locate the matching finalizer sequence.
- Identify mutation, validation, destructive, hosted, or local MCP calls after that sequence.
- For governed required runs, fail if any state-changing/validation tool ran after the finalizer.
- For non-governed runs, at least log telemetry.

5. Add telemetry tags:

- `agentframework.finalizer_sequence`
- `agentframework.post_finalizer_tool_count`
- `agentframework.post_finalizer_mutation_count`
- `agentframework.finalizer_last_significant_tool`

## Tests to add

- Required finalizer passes when finalizer is the last significant tool.
- Required finalizer fails when mutation occurs after finalizer.
- Read-only tool after finalizer can be allowed or warned, depending on policy; document the choice.
- Telemetry/log event includes post-finalizer tool count.

## Acceptance criteria

- Required finalizer represents the final state of the run.
- Any policy exception is explicit and logged.
