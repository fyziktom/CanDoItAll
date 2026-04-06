# Phase11 refactor plan

## Objective
Add the execution/runtime substrate required for the next plugin wave.

## Planned workstreams
1. separate operational messages from domain nodes and fix automation signal aggregation.
2. add canonical trigger definitions and Quartz projection.
3. add durable internal message envelopes, subscriptions, retries, and dead-letter.
4. add hosted workers that drain due triggers, connector outbox commands, and queued background jobs.
5. add plugin ingress inbox, cursors, dedupe, and explicit materialization.
6. add execution policy, telemetry, and optional MQTT bridge without making MQTT canonical.

## Expected outcome
Plugins will only need to define:
- trigger definitions,
- subscriptions/handlers,
- ingress materializers,
- domain outputs.

They will no longer need to build their own polling loops, retry ledgers, or timing logic.
