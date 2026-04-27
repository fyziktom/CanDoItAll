# Subbundle 09 — Domain-Specific Recovery Guidance Provider

## Goal

Keep generic process automation generic while preserving useful calculator/app-building recovery behavior.

## Current problem

Calculator-specific recovery guidance was removed from the generic MAF runtime, which is good. It still appears in the process automation dispatch layer. That is better, but still not ideal as a long-term generic architecture.

## Implementation tasks

1. Introduce a recovery guidance provider abstraction.

Suggested interface:

```csharp
public interface IProcessAutomationRecoveryGuidanceProvider
{
    ProcessAutomationRecoveryGuidance GetGuidance(ProcessAutomationRecoveryContext context);
}
```

2. Move calculator guidance behind a named strategy.

Examples:

- `CalculatorApplicationRecoveryGuidanceProvider`
- `DefaultProcessAutomationRecoveryGuidanceProvider`

3. Select guidance by process template/skill metadata.

Do not infer only from free-form text when structured metadata is available.

4. Tests.

- Calculator template gets calculator-specific recovery guidance.
- Generic process gets generic recovery guidance.
- Unknown template does not receive calculator instructions.

## Acceptance gate

Generic process automation must not hardcode calculator-specific behavior.
