# Exit criteria

Phase11 is closed only when all hard gates are satisfied:

- **HG-11-01** operational messages are separated from Workbench nodes, and automation signals use a multi-source aggregation seam.
- **HG-11-02** a canonical trigger registry exists and is projected into Quartz-backed runtime scheduling with deterministic keys.
- **HG-11-03** durable internal message envelopes, subscriptions, retries, and dead-letter handling exist.
- **HG-11-04** hosted workers automatically drain due triggers, connector outbox commands, and queued background work.
- **HG-11-05** a plugin ingress inbox exists with dedupe, cursors, and explicit materialization.
- **HG-11-06** execution policy and observability exist, and MQTT remains optional.

Behavioral proof must include all required tests named in this bundle.
The phase11 gate script must fail the current repo and pass only after the execution/runtime plane exists.
