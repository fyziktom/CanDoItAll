# Current State

## Plugin Module Ownership

- `CanDoItAll.Modules.Plugins` currently contains both runtime governance services and concrete plugin implementations.
- `PluginsModuleServiceCollectionExtensions.AddPluginsModule` directly registers `DockerBundledPlugin`, `GmailBundledPlugin`, `Office365BundledPlugin`, plugin-specific API clients, Docker host tool service, and plugin-specific workflow executors.
- This violates the desired module boundary: the module is not just a plugin runtime, it also owns every bundled plugin implementation.

## Runtime Catalog

- `PluginCatalogService` composes descriptors from `IPluginCatalogSource` implementations and persisted installation records.
- `BundledPluginCatalogSource` reads descriptors from registered `ICanDoItAllPlugin` instances.
- Installation persistence stores manifest snapshots in `PluginInstallationRecord`, but install currently means "install a descriptor from an already registered source," not "add a package to the runtime."
- `PluginSourceKind` already has `Bundled`, `LocalPackage`, `RemotePackage`, and `ShopCatalog`, but there is no package install store or zip ingest path.

## Runtime Execution

- Existing workflow executors are regular DI registrations of `IWorkflowExecutor`.
- Since the app provider is built at startup, new executor types in a user-installed assembly cannot be registered into the current provider without restart.
- This means package installation can be immediate for manifest/catalog visibility, but executable registrations from package assemblies need a restart path.

## UI

- `/plugins` already uses shared components such as `PageScaffold`, `PageHeader`, `SummaryTiles`, `ListDetailShell`, `Tabs`, `FormSection`, `Grid`, `Stack`, `Cluster`, `Button`, `Alert`, `EmptyState`, and status badges.
- The page currently supports install/enable/disable, grants, connection settings, and OAuth, but not package download/upload or restart.

## Tests

- `PluginCatalogIntegrationTests` covers bundled catalog listing, persisted install state, API routes, OAuth flows, grant enforcement, and Docker workflow executor availability.
- `PluginsPageTests` covers settings persistence and OAuth launch behavior.
- New runtime package tests can fit beside these without broad test scaffolding.
