# Forbidden patterns

- do not leave `AutomationRuntimeOptions` as `AddOptions<AutomationRuntimeOptions>()` only,
- do not rely on tests being the only place that configures runtime options,
- do not hard-code MQTT host/port/client id in production code.
