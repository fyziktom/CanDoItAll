# SB04 semantic invariants

## Admission and dispatch

- Admission atomically persists the operation, pending user turn, active-turn state, and evidence.
- Admission invokes no provider and succeeds only while an actual dispatcher executor is registered.
- Local signaling reduces latency; durable polling is the cross-instance source of liveness.

## Ownership

- At most one unexpired owner/epoch can execute an operation.
- Every heartbeat and owned state write fences operation ID, owner ID, epoch, lease expiry, and runtime
  profile identity.
- A terminal or RecoveryRequired transition releases owner and lease timestamps while retaining the
  monotonically increasing execution epoch.

## Cancellation and lifetime

- Client request cancellation after durable admission does not cancel provider execution.
- Explicit cancellation is committed before the local CTS optimization is signaled.
- The current owner observes remote cancellation within the configured heartbeat interval and again
  before treating provider cancellation as semantically authoritative.
- Absence from the local CTS registry never proves an operation is abandoned or recoverable.

## Fail-closed recovery

- An expired pre-dispatch lease can be reclaimed with a new epoch.
- Provider-dispatch-started evidence makes automatic redispatch unsafe; an expired owner is reduced to
  RecoveryRequired instead.
- Runtime-profile or owner loss cannot fabricate Succeeded, Failed, or Cancelled.
