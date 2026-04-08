# Hard-gate prompt

Use this bundle as the source of truth.

Do not mark phase11 complete unless all of the following are true:
- `gate_check_phase11.py` passes,
- the required tests exist,
- the new runtime workers are actually wired in startup,
- connector outbox pending processing is automatic,
- background jobs are actually drained,
- trigger wakeups are durable,
- ingress envelopes do not auto-become nodes,
- MQTT is optional.
