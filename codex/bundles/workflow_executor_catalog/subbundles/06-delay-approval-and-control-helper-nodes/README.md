# 06-delay-approval-and-control-helper-nodes

## Objective

Implement essential control helpers without pretending to be durable scheduling.

## Required work

1. Implement `utility.delay` for short in-process waits with strict max duration.
2. Implement explicit `human.approval` executor using existing external request mechanism.
3. Add simple control operations:
   - NoOp
   - Fail
   - Assert
   - GateByBoolean
   - EmitEvent
4. Add validation preventing long delays in non-durable runtime.
5. Add tests for cancellation and timeout behavior.
6. Ensure approval response semantics are consistent with `HumanInput`.

## Acceptance checklist

- Delay is safe and bounded.
- Approval executor pauses only when reached.
- Control helper failures produce typed event payloads.
