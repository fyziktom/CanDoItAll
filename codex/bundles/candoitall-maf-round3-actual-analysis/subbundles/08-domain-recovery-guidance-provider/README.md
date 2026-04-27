# 08 - Domain Recovery Guidance Provider

## Problem

Generic process dispatch recovery code still contains domain-specific calculator/Blazor/project-layout instructions.

## Required implementation

Introduce a guidance provider abstraction:

```csharp
public interface IProcessAutomationRecoveryGuidanceProvider
{
    bool CanHandle(ProcessRecoveryGuidanceContext context);
    ProcessRecoveryGuidance BuildGuidance(ProcessRecoveryGuidanceContext context);
}
```

Select providers by process template, project type, tags, artifact expectations, or workspace evidence.

## Acceptance criteria

- Generic recovery directive builder contains only generic recovery structure.
- Calculator/Blazor guidance is moved to a dedicated provider.
- Guidance is short and typed.
- Prompt rendering happens at the edge.
- Static regression prevents new domain-specific strings in generic dispatch files.

## Execution status

Completed. Domain-specific calculator, Blazor, and project recovery guidance is behind `IProcessAutomationRecoveryGuidanceProvider`; generic directive static regression and behavioral directive tests pass.
