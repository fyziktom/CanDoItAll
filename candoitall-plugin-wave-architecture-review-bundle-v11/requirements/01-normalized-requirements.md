# Normalized requirements

## R11-001
Operational execution messages must be persisted outside the Workbench node graph.

## R11-002
The automation signal surface must aggregate multiple contributors and must not depend on last-registration-wins DI behavior.

## R11-003
The platform must expose a canonical trigger registry with cron, timezone, enablement, and misfire semantics preserved in its own model.

## R11-004
Quartz must be used as a scheduler runtime projection, not as the domain source of truth.

## R11-005
A durable internal message plane must support commands, events, wakeups, retries, delayed delivery, fan-out, dedupe, and dead-letter.

## R11-006
Hosted workers must automatically process due triggers, connector outbox commands, and queued background work.

## R11-007
External polling/webhook/email-style plugin inputs must land in a durable ingress inbox before any domain materialization happens.

## R11-008
Execution telemetry, attempt logs, correlation, and dead-letter visibility must exist.

## R11-009
MQTT may exist only as an optional adapter; the core runtime must function without it.
