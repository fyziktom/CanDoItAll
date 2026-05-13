# OAuth2 Extension Point And Connection Broker

## Status

- `Ready`

## Objective

- Add OAuth2 broker contracts/storage placeholders/provider extension points.

## Success Criteria

- OAuth2 broker contracts exist and fit plugin connections.
- Plugin connection auth state can represent OAuth2 without exposing tokens to plugins.
- Fake/test OAuth2 provider proves authorization/token lease flow.
- No real SaaS provider implementation is required.

## Covered Inputs

- `R004`
- `R010`
- `R034`
- `F006`
- `F007`

## Prerequisites

- `SB14`

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Security\SecurityModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Security\SecretRuntimeResolver.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Security\SecretVaults.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Security\SecurityModuleServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\SettingsPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\SettingsPage.razor.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj`

## Deliverables

- IPluginOAuth2Broker and related DTOs.
- OAuth2 provider descriptor/registry.
- Plugin connection auth state extensions.
- Protected token storage placeholder or adapter design through vault/protected storage.
- Fake/test provider and tests.

## Dependency Impact

- Future remote shop and OAuth2 provider bundles depend on this seam staying safe and non-breaking.

## Validation Depth

- `Future-facing contract foundation`

## Implementation Steps

1. Add OAuth2 provider descriptor and request/result models.
2. Add IPluginOAuth2Broker with start authorization, complete callback, and acquire token lease operations.
3. Add plugin connection auth state fields for OAuth2 provider/scopes/status without storing raw tokens in plugin settings.
4. Define protected token storage owner and secret/vault relationship.
5. Implement a fake/test OAuth2 broker/provider for tests and UI contract proof.
6. Add disabled/placeholder UI action for OAuth2 authorization if no real provider exists.
7. Add tests proving plugins receive token leases only and cannot persist refresh tokens.

## Scope Exceptions

- No Gmail/Office365/Figma production OAuth provider.
- No public OAuth callback deployment hardening beyond contract shape.

## Do Not Do

- Do not store OAuth tokens in PluginConnection.SettingsJson.
- Do not expose refresh tokens to plugin executors.
- Do not force all plugins to use OAuth2.

## Acceptance Checklist

- [ ] OAuth2 broker contracts exist and fit plugin connections.
- [ ] Plugin connection auth state can represent OAuth2 without exposing tokens to plugins.
- [ ] Fake/test OAuth2 provider proves authorization/token lease flow.
- [ ] No real SaaS provider implementation is required.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "PluginOAuth|OAuth2"`
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "PluginOAuth"`

## Browser Validation Logging

- If UI changed: capture OAuth2 placeholder/fake authorization state on plugin connection page.

## Progression Gate

- Passed only when OAuth2 can be added later without breaking plugin connection/settings contracts.

## Suggested Agent Prompt

```text
Implement SB16 only.

Work outcome-first:
- Read this subbundle README, the root README, and reviews/01-execution-report.md.
- Verify prerequisites and exact source references before editing.
- Preserve the listed scope boundaries.
- Make the smallest correct change set.
- Capture required proof.
- Update reviews/01-execution-report.md.
- Stop if the progression gate cannot honestly pass.
```
