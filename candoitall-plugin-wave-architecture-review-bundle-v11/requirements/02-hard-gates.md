# Hard gates

## HG-11-01
Messages are not Workbench nodes by default, and signal aggregation is multi-source.

## HG-11-02
A canonical trigger registry exists and is projected into Quartz-backed runtime scheduling with deterministic keys.

## HG-11-03
Durable internal message envelopes, subscriptions, retries, and dead-letter handling exist.

## HG-11-04
Hosted workers automatically drain due triggers, connector outbox commands, and queued background work.

## HG-11-05
A plugin ingress inbox exists with dedupe, cursor tracking, and explicit materialization boundaries.

## HG-11-06
Execution policy and observability exist, and MQTT remains optional rather than canonical.
