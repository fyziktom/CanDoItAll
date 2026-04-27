# 03 - Efficient Context Selection and Session Boundary

## Problem

Current retries often start a fresh session, which is generally correct. But the system needs explicit rules for how much context to carry from failed runs.

## Required behavior

- Do not blindly replay failed chat history.
- Do not use failed MAF session state as process truth.
- Use the same session for approval continuation when required.
- Use fresh sessions for provider failure, invalid finalizer, repeated tool loop, and general step retry.
- Use fresh or controlled sessions for rework continuation, with typed durable context.

## Required implementation

Add a service such as:

```csharp
public interface IAgentRecoveryContextBuilder
{
    AgentRecoveryContext Build(AgentRecoveryDecision decision, ProcessRunContext processContext);
}
```

Context should include:

- process run/step ids;
- source execution run id;
- packet id;
- target artifacts;
- relevant tool receipts;
- validation errors;
- exact proof rerun requirements;
- compact previous output summary;
- prohibited actions.

## Acceptance criteria

- Failed run transcript is only summarized/sanitized, not replayed wholesale.
- Rework prompt contains packet id and target artifacts.
- Approval continuation keeps the existing compatible session.
- Logs include session strategy.

## Execution status

Completed. Recovery decisions carry explicit session strategies for format repair, fresh retry, rework continuation, provider fallback, approval continuation, and human escalation.
