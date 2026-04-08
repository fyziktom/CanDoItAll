# Required implementation evidence

- source code binds `AutomationRuntimeOptions` from configuration in non-test code,
- at least one sample config or doc shows the expected section shape,
- no runtime option relies solely on test-time `services.Configure(...)` overrides.
