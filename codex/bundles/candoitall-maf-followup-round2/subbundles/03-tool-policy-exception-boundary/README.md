# Subbundle 03 — Tool-policy exception boundary

## Goal

Ensure tool-policy blocks cannot be confused with ordinary tool execution failures.

## Problem

`MafAgentRuntime.AgentFactory.cs` currently uses broad exception detection:

```csharp
private static bool IsPolicyException(Exception exception)
    => exception is InvalidOperationException or NotSupportedException;
```

Because the middleware catches policy exceptions around `await next(...)`, ordinary tool exceptions of these types can be reclassified as “blocked by policy”.

## Required implementation

1. Add a dedicated exception type.

Suggested location:

```text
src/CanDoItAll.AgentFramework.Core/ToolInvocation/AgentToolPolicyBlockedException.cs
```

Suggested implementation:

```csharp
public sealed class AgentToolPolicyBlockedException : InvalidOperationException
{
    public AgentToolPolicyBlockedException(
        string toolName,
        ToolInvocationDecisionKind decisionKind,
        string reason)
        : base($"Tool '{toolName}' was blocked by policy. {reason}")
    {
        ToolName = toolName;
        DecisionKind = decisionKind;
        Reason = reason;
    }

    public string ToolName { get; }
    public ToolInvocationDecisionKind DecisionKind { get; }
    public string Reason { get; }
}
```

2. In function-calling middleware, throw this exception only from policy-deny, skip-execution, and missing-effective-approval-path branches.

3. Replace the broad catch filter with:

```csharp
catch (AgentToolPolicyBlockedException exception)
{
    activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
    throw;
}
```

or, if wrapping is still desired, wrap only this dedicated exception.

4. Remove `IsPolicyException(...)` entirely.

5. Ensure actual `next(context, cancellationToken)` exceptions keep their original meaning.

## Tests to add

- Static regression: source must not contain `IsPolicyException`.
- Static regression: source must not contain `exception is InvalidOperationException or NotSupportedException` in policy catch logic.
- Unit/behavior test: a fake allowed tool throwing `InvalidOperationException` is not reported as `AgentToolPolicyBlockedException` and does not include “blocked by policy”.
- Unit/behavior test: missing approval path does throw `AgentToolPolicyBlockedException`.

## Acceptance criteria

- Policy-block telemetry is precise.
- Tool bugs remain diagnosable as tool bugs.
- Build and tests pass.
