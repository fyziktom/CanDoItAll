# Validation Matrix

## Backend Unit Tests

Required areas:

- executor descriptor backward compatibility;
- descriptor provenance/availability behavior;
- settings schema validation;
- settings renderer registry duplicate keys;
- secret authorization by consumer type/id;
- plugin secret broker binding enforcement;
- plugin service façade policy checks;
- plugin manifest duplicate ids/executor ids;
- plugin workflow executor bridge invocation;
- sample plugin happy path and failure path.

## Integration Tests

Required areas:

- plugin installation state persistence;
- plugin connection persistence and redacted summaries;
- plugin catalog API;
- plugin connection health-check API;
- workflow executor catalog includes enabled plugin executors;
- disabled/incompatible plugin executors are not runnable.

## Component Tests

Required areas:

- plugin catalog component renders states and actions;
- schema fallback settings form renders text/url/number/bool/json/select/secret fields;
- plugin connection form redacts secrets;
- workflow canvas editor renders plugin executor settings through renderer host.

## Browser Proof

Required routes:

- plugin catalog/settings route;
- plugin connection create/edit/health check;
- workflow editor executor catalog/selection;
- workflow run/test with sample plugin executor.

Required viewports:

- maximized desktop;
- medium-width desktop/tablet if layout changes.

## Command Proof

Suggested commands:

```text
dotnet build CanDoItAll.slnx
dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "Plugin|WorkflowExecutor|Secret|SettingsSchema"
dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "Plugin"
dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter "Plugin"
dotnet test tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --filter "Plugin"
```
