# Target Solution

## Project Boundary

- `src/CanDoItAll.Modules.Plugins` remains the runtime module. It owns catalog composition, install records, settings, grants, OAuth, package storage/loading services, the `/plugins` page, and API endpoints.
- `src/plugins/CanDoItAll.Plugin.Docker` owns Docker manifest, constants, host-tool service, workflow settings, and workflow executors.
- `src/plugins/CanDoItAll.Plugin.Email` owns shared email workflow payload and message models used by email plugins.
- `src/plugins/CanDoItAll.Plugin.Gmail` owns Gmail manifest, constants, API client, and workflow executors.
- `src/plugins/CanDoItAll.Plugin.Office365` owns Office365 manifest, constants, Graph client, and workflow executors.
- `src/CanDoItAll.Composition` references and registers the bundled plugin implementation projects after `AddPluginsModule`.

## Runtime Package Model

- Package zips contain `plugin.package.json` or `plugin.manifest.json`.
- The package manifest includes a `PluginDescriptor`, optional entry assembly name, optional list of assemblies, optional icon path, and restart policy.
- The install path extracts package contents into a configured installed package root after validating manifest and zip entry paths.
- Installed package descriptors are exposed through an `IPluginCatalogSource`.
- Package assemblies are loaded during service registration at startup. Concrete `IWorkflowExecutor` implementations in those assemblies are registered through DI.
- Packages installed while the app is running are visible as catalog manifests immediately, but executable types require restart.

## Restart Flow

- Package install records restart-required state when a package contains assemblies or declares restart required.
- `/plugins` shows restart-required state and a restart action.
- Restart action calls a service backed by `IHostApplicationLifetime.StopApplication` after returning a clear status to the UI/API.
- The app does not kill arbitrary processes. Restart supervision remains the host/process manager responsibility.

## Catalogue Flow

- A configured catalogue root contains plugin package zips.
- The package service lists catalogue packages by reading their manifests without installing them.
- Selecting a package from the catalogue uses the same validation/extraction code path as uploaded zips.
- Future remote catalogue support can build on `PluginPackageDescriptor.CatalogUri` without changing the UI contract.

## Security And Maintainability

- Zip extraction must reject absolute paths and traversal outside the install root.
- Manifest validation must use `PluginManifestValidator`; invalid packages return explicit `Result` errors.
- Package install does not grant capabilities. Existing install/enable/grant separation remains intact.
- Logs must identify plugin/package ids and actor, without logging secrets or package payload contents.
