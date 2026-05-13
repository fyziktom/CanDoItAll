# Settings Schema Canonicalization And Validator

## Status

- `Completed`

## Objective

- Extract/adapt canonical settings schema/state/validator from connector infrastructure.

## Success Criteria

- There is one canonical configuration/settings schema and state model usable by connectors, workflow executors, and future plugins.
- Connector settings keep working through adapters or migrated types.
- Workflow executor settings can be validated for required fields and types.
- Invalid settings fail during validation with redacted, actionable messages.

## Covered Inputs

- `R005`
- `R013`
- `R023`
- `R032`
- `F003`
- `F005`

## Prerequisites

- `SB02`

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Workflows\WorkflowExecutorModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowDefinitionValidator.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Connectors\ConnectorManifest.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Connectors\ConnectorConfigState.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\Components\ConnectorConfigFieldEditor.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Providers\ProviderExecution.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Resources\ResourceConnectorPlugins.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\SettingsPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\SettingsPage.razor.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj`

## Deliverables

- Shared configuration schema/state/validator abstraction.
- Compatibility adapter or migration from existing ConnectorConfigurationSchema and ConnectorConfigState.
- Validation support for text/url/number/bool/json/select/secret-reference fields.
- WorkflowDefinitionValidator integration for executor settings schema.
- Unit/component tests for schema validation and connector compatibility.

## Dependency Impact

- Later plugin module, workflow bridge, settings UI, and shop work depend on this foundation. Weak proof here causes duplication, secret leakage, or unstable plugin boundaries later.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Identify the neutral subset of ConnectorConfigurationSchema, ConnectorConfigFieldDescriptor, ConnectorConfigFieldType, and ConnectorConfigState.
2. Move or wrap the neutral subset into a shared configuration namespace/project without pulling Workspace-specific workbench hook dependencies.
3. Update Workspace provider/resource connector code to use the canonical schema or adapter types.
4. Add IConfigurationSchemaValidator with required-field/type/URL/JSON/number/secret-reference validation.
5. Update WorkflowExecutorDescriptor settings schema usage so built-in executor descriptors use canonical schema JSON or a typed schema provider.
6. Integrate schema validation into WorkflowDefinitionValidator.
7. Add tests for valid settings, missing required settings, wrong types, invalid JSON, invalid URLs, and backward-compatible connector state serialization.

## Scope Exceptions

- Renderer host belongs to SB04.
- Plugin abstractions and plugin module are still out of scope.

## Do Not Do

- Do not create a new plugin-only settings schema.
- Do not delete connector settings behavior without compatibility tests.
- Do not log raw settings values for secret fields.

## Acceptance Checklist

- [x] There is one canonical configuration/settings schema and state model usable by connectors, workflow executors, and future plugins.
- [x] Connector settings keep working through adapters or migrated types.
- [x] Workflow executor settings can be validated for required fields and types.
- [x] Invalid settings fail during validation with redacted, actionable messages.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "SettingsSchema|Connector|WorkflowExecutor"`
- `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter "ConnectorConfig"`
- `dotnet build src\CanDoItAll.Modules.Workspace\CanDoItAll.Modules.Workspace.csproj`

## Browser Validation Logging

- N/A unless Settings page UI is changed; if changed, capture settings page proof.

## Progression Gate

- Passed only when settings schema validation is canonical and no duplicate plugin-only schema has been introduced.

## Suggested Agent Prompt

```text
Implement SB03 only.

Work outcome-first:
- Read this subbundle README, the root README, and reviews/01-execution-report.md.
- Verify prerequisites and exact source references before editing.
- Preserve the listed scope boundaries.
- Make the smallest correct change set.
- Capture required proof.
- Update reviews/01-execution-report.md.
- Stop if the progression gate cannot honestly pass.
```
