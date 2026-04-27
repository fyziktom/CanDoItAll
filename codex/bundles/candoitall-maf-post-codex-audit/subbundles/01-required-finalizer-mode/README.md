# Subbundle 01 — Required Finalizer Mode for Critical Process Runs

## Goal

Turn the current process-step finalizer from advisory shadow telemetry into an enforceable exact-one finalizer for critical process automation runs.

## Current problem

Process automation currently invokes agents with:

```csharp
MetadataJson: "{}"
```

`AgentFinalizerPolicies.ResolveMode(...)` defaults process-step runs to `Shadow`, not `Required`. Therefore a missing finalizer tool call is logged but does not fail the process step.

## Implementation tasks

1. Add a typed way to request finalizer mode.

Preferred option:

```csharp
public sealed record ExecutionInvocationPolicy(
    AgentFinalizerMode FinalizerMode,
    int MaxStructuredOutputRepairAttempts,
    bool RequireStructuredOutputValidation);
```

Then include it in `ExecutionInvocationContext` or a sibling field in `ExecutionRunRequest`.

Acceptable minimal option:

- Add a small metadata builder that writes `{ "agentFinalizerMode": "required" }` safely instead of hardcoding JSON strings.

2. In `ProcessRunAutomationDispatchService.Execution.cs`, set finalizer mode required for governed process-step runs.

Suggested behavior:

- Required by default for real process automation.
- Shadow only through explicit config flag such as `AgentFramework:Finalizers:ProcessStepMode = Shadow`.
- Disabled only for non-critical development/test runs.

3. Update `AppendFinalizerInstructions(...)` to be mode-aware.

Required mode instructions should say:

```text
You must call `<tool>` exactly once.
The tool arguments are the only machine-readable final result for this run.
Do not provide the machine result as ordinary assistant text.
Assistant text is display-only.
```

Shadow mode instructions may say:

```text
Return the structured output and also call `<tool>` exactly once with an identical object for telemetry comparison.
```

4. Add tests.

Required tests:

- Process-step request metadata sets required finalizer mode.
- Missing finalizer in required mode fails.
- Duplicate finalizer in required mode fails.
- Invalid finalizer JSON in required mode fails.
- Valid finalizer in required mode succeeds.
- Shadow mode still allows missing finalizer but logs/notifies.

5. Update docs.

Document finalizer modes and when each is allowed.

## Acceptance gate

A process-step run with required finalizer mode must not complete unless exactly one valid `submit_process_step_outcome` call is captured.
