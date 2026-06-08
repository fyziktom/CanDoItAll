# Runtime Host Decision

## Decision
Runtime host remains not approved.

## Why
The system now has useful read-only domain driver packages and an explicit gateway, but production runtime integration requires additional audit persistence, lifecycle ownership, authorization, operational controls, and failure semantics.

## Approved next work
- Explicit batch gateway.
- Process read-only orchestration over supplied payloads.
- More source-backed tests.
- Documentation and release gates.

## Still denied
- Generic runtime host.
- Driver registry/selector/DI.
- Manager command.
- Scheduler/workflow integration.
- Execution-capable drivers.
- Any mutation side effect.
