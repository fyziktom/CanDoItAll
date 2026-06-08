# Explicit Gateway Lane Design

## Allowed pattern
- Explicit method per approved verification-only lane.
- Strongly typed request per lane.
- Strongly typed response shared through `ProcessDriverVerificationResponse`.
- Read-only implementations only.
- Gateway may construct default verifier instances, but must not register, discover, or dynamically select implementations.

## Denied pattern
- `Verify(ProcessDriverVerificationGatewayLane lane, object payload)`.
- Runtime plugin discovery.
- Service provider or DI usage.
- Registry or selector.
- Manager command.
- Scheduler/workflow hook.
- Any execution-capable operation.
