# Observability and optional MQTT adapter

## Decision
MQTT may be added, but only as an optional adapter.

## What MQTT is good for here
- live dashboards,
- agent activity feeds,
- transient status broadcasting,
- future external observers,
- future process decomposition.

## What MQTT must not become
- the canonical trigger registry,
- the canonical internal message store,
- the authoritative retry/dead-letter ledger,
- the only source of truth for automation state.

## Recommended exact types for phase11
- `AutomationExecutionLogRecord`
- `AutomationDeliveryAttemptRecord`
- `IAutomationTelemetryPublisher`
- `MqttAutomationTelemetryBridge`

## Required rule
Core scheduling, dispatch, retries, and ingress must continue to work when MQTT is disabled.
