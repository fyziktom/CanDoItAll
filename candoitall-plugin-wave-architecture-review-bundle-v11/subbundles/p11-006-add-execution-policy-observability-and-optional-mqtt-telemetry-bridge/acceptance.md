# Acceptance

- Add execution attempt logs or equivalent telemetry records.
- Preserve correlation and causation metadata through internal dispatch.
- Expose dead-letter and retry state for operators.
- Add an optional MQTT telemetry publisher/bridge for live dashboards and future external observers.
- The core runtime must keep working when MQTT is disabled.
- Recommended exact types:
  - `AutomationExecutionLogRecord`
  - `AutomationDeliveryAttemptRecord`
  - `IAutomationTelemetryPublisher`
  - `MqttAutomationTelemetryBridge`
