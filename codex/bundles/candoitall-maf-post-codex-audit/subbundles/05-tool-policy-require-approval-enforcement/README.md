# Subbundle 05 — Enforce `RequireApproval` in Function Middleware

## Goal

Make `ToolInvocationDecisionKind.RequireApproval` an actual execution barrier unless an effective approval path is active.

## Current problem

The middleware only blocks `Deny` and `SkipExecution`. `RequireApproval` continues to `next(...)`, relying on approval wrappers to intercept. That is fragile if wrappers are absent, unsupported, or incorrectly marked as available.

## Implementation tasks

1. Add effective approval flag to policy context.

Suggested fields:

```csharp
ApprovalWrapperAvailable
ApprovalWrapperEffectiveForProvider
ApplicationApprovalAvailable
```

2. Change middleware behavior.

Pseudo-code:

```csharp
if (policyDecision.Kind == RequireApproval && !context.HasEffectiveApprovalPath)
{
    throw new InvalidOperationException("Tool requires approval, but no effective approval mechanism is available.");
}

if (policyDecision.Kind is Deny or SkipExecution)
{
    throw ...;
}

return await next(...);
```

3. Application-level approval fallback.

If MAF approval requests are unsupported, convert the requested mutation to an application-level pending approval record before the tool executes. Do not execute the underlying tool until approval is granted.

4. Tests.

Required tests:

- Mutation tool with no wrapper is blocked.
- Mutation tool with wrapper but unsupported provider is blocked or pended before execution.
- Mutation tool with effective wrapper produces pending approval instead of executing immediately.
- Read-only tool continues normally.
- Unknown tool remains denied.

## Acceptance gate

No write/destructive/mutation tool may execute merely because `RequireApproval` was logged.

## Execution Result

Status: Complete. Tool middleware blocks `RequireApproval` when the selected provider/runtime has no effective approval path.
