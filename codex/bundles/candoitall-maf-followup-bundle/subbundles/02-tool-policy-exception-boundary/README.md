# Subbundle 02 — Tool policy exception boundary

## Problem

The MAF function-call middleware currently catches broad exception types as policy exceptions:

```csharp
private static bool IsPolicyException(Exception exception)
    => exception is InvalidOperationException or NotSupportedException;
```

Because this catch happens around `return await next(context, cancellationToken)`, a real downstream tool failure can be reclassified as `Tool '<name>' was blocked by policy`.

## Required change

Create a dedicated exception type, for example:

```csharp
public sealed class AgentToolPolicyBlockedException : Exception
{
    public AgentToolPolicyBlockedException(string toolName, string reason)
        : base($"Tool '{toolName}' was blocked by policy. {reason}")
    {
        ToolName = toolName;
        Reason = reason;
    }

    public string ToolName { get; }
    public string Reason { get; }
}
```

Use repository style and namespace conventions.

Only throw this exception from explicit policy-block branches:

- `Deny`
- `SkipExecution`
- `RequireApproval` without effective approval path

Catch only this type for policy-block telemetry/logging. Let all exceptions thrown by `next(...)` keep their original type/message unless there is a separate, explicit error-handling policy.

## Tests

Add `AgentToolInvocationPolicyTests` and/or middleware-focused tests:

- Unknown tool returns `Deny`.
- Mutation tool without auto approval and without effective approval path returns `RequireApproval` and middleware blocks with `AgentToolPolicyBlockedException`.
- Mutation tool with effective approval path is allowed to proceed to MAF approval wrapper.
- A fake tool that throws `InvalidOperationException("business failure")` after policy allow is not reported as blocked by policy.
- Repeated mutation/validation signatures over the limit are denied.
