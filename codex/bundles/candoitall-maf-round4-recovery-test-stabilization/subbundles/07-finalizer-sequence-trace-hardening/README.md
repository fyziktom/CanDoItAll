# 07 — Finalizer Sequence Trace Hardening


## Problem

Finalizer sequence validation depends on tool invocation traces. For governed required-finalizer runs, missing trace data should produce a deterministic policy decision.

## Tasks

1. Review `AgentFinalizerSequenceValidator` behavior when trace data is unavailable.
2. Decide policy for governed required-finalizer runs:
   - fail if trace is expected but absent; or
   - explicitly allow with a high-severity diagnostic only when trace recorder is unavailable by configuration.
3. Ensure process mutation tools are considered significant side effects.
4. Add behavior tests with traces available, traces missing, mutation before finalizer, mutation after finalizer, and read-only tool after finalizer.

## Acceptance criteria

- Required finalizer sequence enforcement is deterministic.
- Missing trace does not silently hide a policy violation in governed runs.
- Process mutation after finalizer is rejected when finalizer should be terminal.

