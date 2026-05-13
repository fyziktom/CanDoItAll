# Secret Runtime Authorization And Plugin Secret Broker

## Status

- `Ready`

## Objective

- Make secrets consumer-bound and introduce plugin-facing secret broker contract.

## Success Criteria

- Secret runtime resolution enforces consumer type and consumer id for plugin/executor/connection scenarios.
- A plugin-facing secret broker contract exists or is ready to be used by the plugin module.
- Secret references in settings are persisted as ids/bindings only.
- Tests prove forbidden secrets are rejected and logs/messages are redacted.

## Covered Inputs

- `R004`
- `R011`
- `R012`
- `R019`
- `R034`
- `F006`
- `F007`

## Prerequisites

- `SB01,SB03`

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Security\SecurityModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Security\SecretRuntimeResolver.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Security\SecretVaults.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Security\SecurityModuleServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Security\StorageSecretResolver.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Forms\SecretField.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Connectors\ConnectorManifest.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\Components\ConnectorConfigFieldEditor.razor`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj`

## Deliverables

- Consumer-bound secret authorization rules.
- Plugin secret purposes and request models.
- IPluginSecretBroker contract or foundation adapter.
- Redacted secret binding summaries.
- Tests for allowed/denied plugin secret resolution.

## Dependency Impact

- Later plugin module, workflow bridge, settings UI, and shop work depend on this foundation. Weak proof here causes duplication, secret leakage, or unstable plugin boundaries later.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Audit SecretRuntimeRequest and current SecretReference/AllowedSecret behavior.
2. Define consumer type values for plugin, plugin connection, workflow executor, and workflow node where appropriate.
3. Implement authorization rules that bind secret resolution to allowed secret ids and consumer ownership.
4. Add plugin-ready secret broker abstraction that delegates to ISecretRuntimeResolver.
5. Ensure error messages never include secret values.
6. Review non-Windows vault provider behavior and document explicit supported-provider expectations for plugin deployments.
7. Add unit/integration tests for allowed, missing, wrong-purpose, wrong-consumer, and deleted secret references.

## Scope Exceptions

- Plugin module does not exist yet; broker can be contract/adaptor only.
- OAuth2 token storage is deferred to SB16.

## Do Not Do

- Do not expose ISecretVault directly to plugins.
- Do not store raw secrets in settings JSON.
- Do not silently bypass purpose or consumer checks for built-ins.

## Acceptance Checklist

- [ ] Secret runtime resolution enforces consumer type and consumer id for plugin/executor/connection scenarios.
- [ ] A plugin-facing secret broker contract exists or is ready to be used by the plugin module.
- [ ] Secret references in settings are persisted as ids/bindings only.
- [ ] Tests prove forbidden secrets are rejected and logs/messages are redacted.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "SecretRuntime|SecretBroker|Vault"`
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "Secret"`
- `dotnet build src\CanDoItAll.Modules.Security\CanDoItAll.Modules.Security.csproj`

## Browser Validation Logging

- N/A unless secret picker UI changes; if changed, capture settings secret picker proof.

## Progression Gate

- Passed only when a plugin/executor/connection cannot resolve an unbound secret even if it knows the secret id.

## Suggested Agent Prompt

```text
Implement SB05 only.

Work outcome-first:
- Read this subbundle README, the root README, and reviews/01-execution-report.md.
- Verify prerequisites and exact source references before editing.
- Preserve the listed scope boundaries.
- Make the smallest correct change set.
- Capture required proof.
- Update reviews/01-execution-report.md.
- Stop if the progression gate cannot honestly pass.
```
