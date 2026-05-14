# Structured Input

## Objectives

- Add `ISecretVault`, strongly typed vault options, provider identifiers, a DPAPI Windows implementation, explicit unsupported provider stubs for future platforms/clouds, and an in-memory test vault.
- Move `SecretService`, storage resolvers, and agent provider credential resolution from direct `ISecretProtector` calls to vault-backed value access.
- Add strongly typed secret-reference settings for agents and workflow executors, starting with HTTP fetch.
- Add UI support for selecting stored secrets and creating new ones from project-structure flows without embedding raw secret values.
- Replace ad hoc password edit/copy behavior with a BaseLib secret field that supports copy and a timed `show for 30s` reveal.
- Update secure-configuration documentation with vault providers, runtime resolution rules, and validation commands.

## Constraints

- Windows local users must use DPAPI by default.
- Non-Windows named providers must fail explicitly until their platform implementations exist.
- DataProtection file fallback is allowed only when explicitly selected or when the platform factory reaches the fallback path; it must not masquerade as DPAPI.
- Secrets are referenced by id/key/name metadata in agent, workflow, process, and project-structure settings; raw values are resolved only at execution time.
- Logging and error messages must include record id/name/provider metadata but never the raw value.
- No XML documentation comments.
- BaseLib components must be preferred over raw page-local `div` and password/copy markup.

## Validation Expectations

- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "SecretVault|WorkflowExecutor|AgentProvider"`
- `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter "Secret|ProjectStructure|WorkflowCanvas"` when component tests exist for touched surfaces.
- `dotnet build CanDoItAll.slnx`
- Browser proof for `/settings?tab=secrets`, `/agents?tab=workflows`, and the project-structure secret dialog if the app can be hosted in this environment.

## UI Validation Strategy

- First browser pass uses a large `1600x900` viewport.
- Open the secret editor, workflow HTTP settings, and project-structure secret picker dialogs.
- Check timed reveal, copy buttons, readable labels, no clipping, no harmful overflow, and no secret text visible after the hide timeout.

## Working Assumptions

- DPAPI is available on the target Windows user profile.
- Existing DB rows may need an explicit compatibility path if they were protected with ASP.NET Core Data Protection.
- Future platform/cloud providers are intentionally not implemented in this bundle.
