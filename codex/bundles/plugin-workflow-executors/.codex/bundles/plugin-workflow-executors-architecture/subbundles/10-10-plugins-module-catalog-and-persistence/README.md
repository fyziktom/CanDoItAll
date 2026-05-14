# Plugins Module Catalog And Persistence

## Status

- `Completed`

## Objective

- Create Plugins module, catalog, installed state, API wiring, composition, migrations.

## Success Criteria

- A dedicated Plugins module exists and is wired into composition deterministically.
- Bundled plugin catalog source exists.
- Installation/enabled state persists separately from plugin connection settings.
- Plugin catalog API returns bundled/installed/enabled/unavailable states.

## Covered Inputs

- `R001`
- `R006`
- `R007`
- `R008`
- `R022`
- `R024`
- `R031`
- `R035`
- `F011`
- `F012`
- `F015`

## Prerequisites

- `SB09`

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Composition\ModuleAssemblies.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Composition\RuntimeHostServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Composition\ShellNavigation.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Program.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\ApiEndpointRouteBuilderExtensions.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj`

## Deliverables

- New `src/CanDoItAll.Modules.Plugins` project.
- Plugin catalog service and bundled plugin source.
- Plugin installation entities and migrations.
- Plugin API endpoint map.
- Composition/module assembly/nav wiring.
- Integration tests for catalog/install state.

## Dependency Impact

- Shop, OAuth2, final proof, and future SaaS plugin bundles depend on this MVP being coherent and bounded.

## Validation Depth

- `Plugin MVP implementation`

## Implementation Steps

1. Create Plugins module project with references to abstractions, SharedKernel, Infrastructure, Security, and UI components as needed.
2. Define plugin catalog, installation store, and bundled source services.
3. Add EF entities/migrations for plugin installations and manifest snapshots.
4. Add API endpoints for catalog and installations using DTOs, not EF entities.
5. Wire module into ModuleAssemblies and RuntimeHostServiceCollectionExtensions.
6. Add shell navigation entry or Settings route link for plugins.
7. Add install/enable/disable audit event hooks if audit foundation exists.
8. Add integration tests for bundled catalog and persisted install state.

## Scope Exceptions

- Settings page and connections belong to SB11.
- Workflow executor bridge belongs to SB12.
- Remote shop belongs to SB15.

## Do Not Do

- Do not load plugin assemblies dynamically.
- Do not merge plugin catalog into Workspace settings services.
- Do not persist connection secrets in installation settings.

## Acceptance Checklist

- [x] A dedicated Plugins module exists and is wired into composition deterministically.
- [x] Bundled plugin catalog source exists.
- [x] Installation/enabled state persists separately from plugin connection settings.
- [x] Plugin catalog API returns bundled/installed/enabled/unavailable states.

## Proof Captured

- `dotnet build src\CanDoItAll.Modules.Plugins\CanDoItAll.Modules.Plugins.csproj` - passed, 0 warnings, 0 errors.
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "PluginCatalog|PluginInstallation"` - passed, 3 tests.
- `dotnet build CanDoItAll.slnx` - passed, 0 warnings, 0 errors.
- Browser route proof: `/plugins` rendered the plugin catalog shell after database confirmation; screenshot captured at `artifacts\sb10-plugins-catalog\sb10-plugins-catalog-route.png`.
- Startup/browser diagnostics: browser console had 0 warnings/errors and server log scan found no `fail:`, `crit:`, `Exception`, or `error:` entries after the AgentFramework warmup query repair.

## Proof Required

- `dotnet build src\CanDoItAll.Modules.Plugins\CanDoItAll.Modules.Plugins.csproj`
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "PluginCatalog|PluginInstallation"`
- `dotnet build CanDoItAll.slnx`

## Browser Validation Logging

- If navigation/catalog shell is visible, capture plugin catalog route screenshot after module wiring.

## Progression Gate

- Passed. Plugin catalog/install state is separate, deterministic, and persisted outside plugin connection settings.

## Suggested Agent Prompt

```text
Implement SB10 only.

Work outcome-first:
- Read this subbundle README, the root README, and reviews/01-execution-report.md.
- Verify prerequisites and exact source references before editing.
- Preserve the listed scope boundaries.
- Make the smallest correct change set.
- Capture required proof.
- Update reviews/01-execution-report.md.
- Stop if the progression gate cannot honestly pass.
```
