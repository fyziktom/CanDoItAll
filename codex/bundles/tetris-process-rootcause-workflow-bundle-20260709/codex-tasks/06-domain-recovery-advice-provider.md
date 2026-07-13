# Task 06: Move domain recovery advice out of generic application

## Goal

Remove .NET/software-delivery hardcodes from `ProcessStepRecoveryInstructionBuilder`.

## Proposed design

Add:

```csharp
public interface IProcessRecoveryAdviceProvider
{
    bool CanHandle(ProcessStepRecoveryAdviceContext context);
    IReadOnlyList<string> BuildAdvice(ProcessStepRecoveryAdviceContext context);
}
```

Providers:

- `GenericProcessRecoveryAdviceProvider`
- `DotNetSoftwareDeliveryRecoveryAdviceProvider`

The generic builder collects diagnostics and delegates to providers.

## Move out of generic builder

Move logic referencing:

- `qa-validation`,
- `qa-recheck`,
- `quality-accepted`,
- `repair-required`,
- `repair-escalation`,
- `workspace_dotnet_*`,
- `workspace_pwsh_run_script`,
- `workspace_dotnet_new`.

## Acceptance

- Generic builder has no domain tokens.
- DotNet provider produces equivalent or better guidance when software-delivery process metadata is present.
- Tests verify provider selection and generated guidance.
