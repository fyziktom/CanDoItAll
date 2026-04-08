# Acceptance

- Add execution attempt and delivery attempt records.
- Preserve correlation and causation metadata through internal dispatch.
- Expose retry and dead-letter state for operators.
- Add an optional MQTT telemetry publisher/bridge for live dashboards and future observers.
- The core runtime must keep working when MQTT is disabled.
- Recommended exact types:
  - `AutomationExecutionLogRecord`
  - `AutomationDeliveryAttemptRecord`
  - `IAutomationTelemetryPublisher`
  - `MqttAutomationTelemetryBridge`
