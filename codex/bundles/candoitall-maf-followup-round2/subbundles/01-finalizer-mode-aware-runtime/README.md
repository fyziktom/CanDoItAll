# Subbundle 01 — Finalizer mode-aware runtime composition

## Goal

Make finalizer tool attachment and finalizer instructions depend on the effective `AgentFinalizerMode`, not just the presence of a structured-output contract.

## Problem

`MafAgentRuntime.AgentFactory.cs` currently calls `CreateFinalizerCapture(structuredOutput)` during runtime build. The runtime receives no execution finalizer mode. The execution service later resolves the mode from `ExecutionRunRecord.MetadataJson`.

This means the runtime can attach a finalizer tool and tell the model to call it exactly once while the execution layer treats the same run as `Disabled` or `Shadow`.

## Required implementation

1. Introduce a runtime-level policy/options object.

Suggested record:

```csharp
public sealed record AgentRuntimeExecutionPolicy(
    AgentFinalizerMode FinalizerMode = AgentFinalizerMode.Disabled,
    bool RequireStructuredOutputValidation = true,
    int MaxStructuredOutputRepairAttempts = 0);
```

2. Extend `IAgentRuntime.RunAsync(...)` and `IAgentRuntime.RespondToPendingApprovalsAsync(...)` to accept the policy/options object.

Keep backwards-compatible defaults if necessary.

3. In `AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`, compute the effective policy from the created run before invoking runtime.

Suggested helper:

```csharp
private static AgentRuntimeExecutionPolicy ResolveRuntimeExecutionPolicy(
    ExecutionRunRecord run,
    AgentStructuredOutputContract? structuredOutput)
{
    return new AgentRuntimeExecutionPolicy(
        FinalizerMode: AgentFinalizerPolicies.ResolveMode(run, structuredOutput),
        RequireStructuredOutputValidation: ExecutionInvocationMetadata.ResolveRequireStructuredOutputValidation(run),
        MaxStructuredOutputRepairAttempts: ExecutionInvocationMetadata.ResolveMaxStructuredOutputRepairAttempts(run, structuredOutput));
}
```

4. Pass this policy into:

- initial runtime run,
- approval continuation,
- auto-approval continuation,
- temperature-retry path,
- scenario harness runtime,
- process mock runtime,
- tests/fakes.

5. In `MafAgentRuntime.AgentFactory.cs`, update `CreateRuntimeBuildAsync(...)` to take the runtime execution policy.

Behavior:

- `Required`: attach finalizer tool; append required-mode instructions.
- `Shadow`: attach finalizer tool only if the team wants shadow telemetry; append shadow-mode instructions, not exact-once required instructions.
- `Disabled`: do not call `CreateFinalizerCapture(...)`; do not append finalizer instructions.

6. Preserve the `ResponseFormat` structured-output path independently from finalizer mode.

`structuredOutput` should still produce JSON-schema `ResponseFormat` when supported, even when finalizer mode is disabled.

## Tests to add

- `Required_mode_attaches_finalizer_tool_and_required_instructions`.
- `Disabled_mode_does_not_attach_finalizer_tool_or_instructions`.
- `Shadow_mode_does_not_append_required_exact_once_instruction`.
- `Execution_service_passes_required_mode_for_process_step_runs`.
- `Approval_continuation_preserves_effective_finalizer_mode`.
- `Temperature_retry_preserves_effective_finalizer_mode`.

A lightweight fake runtime can capture the policy passed by the execution service.

## Acceptance criteria

- No runtime finalizer tool is available in disabled mode.
- No exact-once finalizer instruction appears in disabled or shadow mode.
- Required process-step automation still requires exact-one finalizer at completion.
- Build and tests pass.

## Status

Completed.

## Requirements Owned

R01, F01.

## Prerequisites

None.

## Dependency Impact

Critical foundation for subbundles 02, 05, and 07.

## Validation Depth

Code inspection plus behavioral tests for required, shadow, and disabled finalizer composition across initial, continuation, and retry paths.

## Progression Gate

Downstream finalizer instruction and sequence work may continue only after runtime composition receives and honors the effective finalizer mode.

## Closure Proof

Implemented/verified through `AgentRuntimeExecutionOptions`, `CreateFinalizerCapture`, disabled-mode finalizer omission, continuation propagation, and temperature retry preservation. Focused proof: `MafAgentRuntimeTests` passed; full unit suite passed; mandatory Release build passed.
