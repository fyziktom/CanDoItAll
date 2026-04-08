# QA prompt

Verify that phase11 is genuinely closed.

Check all of the following:
- messages are not default Workbench nodes,
- signal aggregation is multi-source,
- canonical triggers exist and Quartz is only a runtime projection,
- a durable message plane exists with retries and dead-letter,
- hosted workers are registered and actively drain work,
- ingress envelopes exist and materialization is explicit,
- MQTT can be disabled without breaking core execution,
- all required tests exist and are meaningful,
- `scripts/gate_check_phase11.py` passes only after the implementation.

Reject the implementation if any new runtime behavior still depends on manual calls, request-path polling, or in-memory-only transport.
