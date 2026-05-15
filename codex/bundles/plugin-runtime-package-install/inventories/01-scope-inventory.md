# Scope Inventory

## Must Move To `src/plugins`

| Current path | Target project |
| --- | --- |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Docker\*` | `src/plugins/CanDoItAll.Plugin.Docker` |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Email\*` | `src/plugins/CanDoItAll.Plugin.Email` |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Gmail\*` | `src/plugins/CanDoItAll.Plugin.Gmail` |
| `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Office365\*` | `src/plugins/CanDoItAll.Plugin.Office365` |

## Must Remain In Plugin Module

- Catalog models and services.
- Grant, connection, and OAuth runtime services.
- Persistence records and schema initialization.
- `/plugins` Razor page and package install UI.
- Package zip validation, storage, runtime restart state, and catalog source.

## Must Update

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Services\PluginsModuleServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Composition\RuntimeHostServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Composition\CanDoItAll.Composition.csproj`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\PluginsApi.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Pages\PluginsPage.razor`
- `C:\repositories\CanDoItAll\CanDoItAll.slnx`

## Primary Tests To Extend

- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\PluginsPageTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\PluginManifestTests.cs`
