# 06-delay-approval-and-control-helper-nodes

## Status

- Status: `Completed`

## Closure Notes

- Implemented bounded delay helper semantics.
- Implemented explicit approval-request executor using existing external request semantics.
- Kept host command execution planned/unavailable rather than introducing unsafe process execution in this bundle.
- Proof manifest: `bundle://proof/SB06/manifest.md`
- Semantic invariants: `bundle://proof/SB06/semantic-invariants.md`

## Objective

Implement essential control helpers without pretending to provide durable scheduling or unsafe host command execution.

## Covered Inputs

- RN02: Users need obvious helper nodes and executors.
- R7: Implement delay/wait and explicit approval helper capabilities with clear runtime semantics.
- R8: Helper node behavior must not silently pass through active workflows.
- R11: Scenario harness must cover approval flow.

## Prerequisites

- SB01 closure gate passed.
- SB02 closure gate passed if helper outputs create artifacts.
- SB05 closure gate passed if helper output reports are used in examples.
- Existing external request runtime behavior is reviewed.

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowExternalRequestRuntime.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowRuntimeManager.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowCompiler.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/BuiltInWorkflowExecutorDescriptors.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/PlannedWorkflowExecutor.cs`
- `repo://tests/CanDoItAll.Tests.Unit/WorkflowFoundationTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/WorkflowExecutorTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/WorkflowApiIntegrationTests.cs`

## Scope

- Implement `utility.delay` for short in-process waits with a strict maximum duration.
- Implement `human.approval` through the existing external request mechanism.
- Add simple bounded control operations: no-op, fail, assert, gate-by-boolean, and emit-event if they fit existing runtime contracts.
- Validate long delays as unavailable in the current in-process runtime.
- Keep `command.process` unavailable unless a separate approved command-policy subbundle is created.

## Dependency Impact

- SB08 helper node policy depends on explicit delay/approval semantics.
- SB09 templates can include approval-gated workflows only after this phase is proven.
- SB10 approval scenario proof depends on this surface.

## Validation Depth

- Tests for delay max duration, cancellation, timeout, approval request creation only when reached, approval response handling, and failure event payloads.
- Negative tests for long delay and unsupported command execution.
- Runtime proof must distinguish in-process delay from durable scheduling.

## Implementation Steps

1. Add strongly typed delay and approval settings.
2. Implement safe delay executor with cancellation token use and strict max duration.
3. Implement approval executor using `WorkflowExternalRequestRuntime`.
4. Add control helper executor operations only where they can produce typed deterministic results.
5. Update descriptors and tests.

## Do Not Do

- Do not implement durable scheduling in this bundle.
- Do not use thread blocking for delay.
- Do not auto-approve external effects.
- Do not implement host command execution as a convenience.

## Acceptance Checklist

- Delay is safe, bounded, cancellable, and honest about in-process limits.
- Approval executor pauses only when reached.
- Approval response semantics match `HumanInput`.
- Control helper failures produce typed event payloads.
- Unsafe command execution remains unavailable.

## Proof Required

- Passing targeted delay/approval/control test transcript.
- Negative proof for long delay, cancellation, and unsupported command execution.
- Changed-file hashes, source assertions, and anti-stub audit.
- Execution report row for SB06 closure.

## Browser Validation Logging

- N/A unless approval request UI changes; if UI changes, record route, viewport, approval actions, screenshots, and result.

## Progression Gate

- Continue to SB07 only after delay and approval helpers are bounded, cancellation-aware, and do not imply durable scheduling.

## Suggested Agent Prompt

Use SB06 to implement bounded control helpers. Keep delay and approval semantics explicit, reject unsafe command execution, and prove cancellation and approval behavior through targeted tests.
