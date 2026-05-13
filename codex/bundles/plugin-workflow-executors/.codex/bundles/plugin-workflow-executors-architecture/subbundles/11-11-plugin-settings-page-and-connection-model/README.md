# Plugin Settings Page And Connection Model

## Status

- `Ready`

## Objective

- Add plugin catalog/settings UI, connection settings, health check surface.

## Success Criteria

- Users can view plugin settings and create/edit plugin connections.
- Connection settings are validated by canonical schema.
- Secret bindings are persisted as references only.
- Health check action exists and is redacted.
- Browser proof exists for catalog/settings/connection flows.

## Covered Inputs

- `R005`
- `R006`
- `R007`
- `R011`
- `R014`
- `R024`
- `R025`
- `R031`
- `R034`
- `F004`
- `F005`
- `F006`

## Prerequisites

- `SB10,SB04,SB05`

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Security\SecurityModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Security\SecretRuntimeResolver.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Forms\SecretField.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Connectors\ConnectorManifest.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Connectors\ConnectorConfigState.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\Components\ConnectorConfigFieldEditor.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\SettingsPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\SettingsPage.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Composition\ShellNavigation.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj`

## Deliverables

- Plugin catalog/settings page or settings tab.
- Plugin connection entities/services/API endpoints.
- Renderer host integration for plugin settings and connection settings.
- Secret picker/binding integration through plugin secret broker.
- Health-check API/service surface.
- Component/browser tests.

## Dependency Impact

- Shop, OAuth2, final proof, and future SaaS plugin bundles depend on this MVP being coherent and bounded.

## Validation Depth

- `Plugin MVP implementation`

## Implementation Steps

1. Define plugin connection DTOs and persistence separate from installations.
2. Add plugin connection service with create/read/update/delete and redacted summaries.
3. Render plugin settings/connection forms through renderer host and canonical schema fallback.
4. Add secret-reference fields using existing secret picker/SecretField patterns without raw secret values.
5. Add health-check endpoint that invokes plugin health check through capability context and redacts results.
6. Add catalog/settings page navigation and empty/error states.
7. Add component tests for forms, validation, redaction, and health check status.
8. Capture browser proof for catalog, create connection, edit connection, and health check.

## Scope Exceptions

- OAuth2 authorize button can be placeholder/disabled until SB16.
- Workflow executor usage belongs to SB12.

## Do Not Do

- Do not put plugin settings inside workflow node JSON.
- Do not reveal raw secret values in plugin settings UI.
- Do not create plugin-specific page-local settings renderers.

## Acceptance Checklist

- [ ] Users can view plugin settings and create/edit plugin connections.
- [ ] Connection settings are validated by canonical schema.
- [ ] Secret bindings are persisted as references only.
- [ ] Health check action exists and is redacted.
- [ ] Browser proof exists for catalog/settings/connection flows.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter "PluginSettings|PluginConnection"`
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "PluginConnection|PluginHealth"`
- Browser screenshots for `/settings/plugins` or equivalent route.

## Browser Validation Logging

- Required. Open plugin catalog/settings route, create/edit a connection, verify validation and redaction, capture maximized and narrower screenshots.

## Progression Gate

- Passed only when plugin settings and plugin connections are distinct, schema-validated, and redacted.

## Suggested Agent Prompt

```text
Implement SB11 only.

Work outcome-first:
- Read this subbundle README, the root README, and reviews/01-execution-report.md.
- Verify prerequisites and exact source references before editing.
- Preserve the listed scope boundaries.
- Make the smallest correct change set.
- Capture required proof.
- Update reviews/01-execution-report.md.
- Stop if the progression gate cannot honestly pass.
```
