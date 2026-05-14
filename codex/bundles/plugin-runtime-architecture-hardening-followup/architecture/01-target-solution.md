# Target Solution

## Boundary Model

The target architecture keeps four boundaries explicit:

- Plugin abstractions: typed contracts for descriptors, execution events, icons, package identity, and executor source metadata.
- Plugin runtime module: generic package install/list/activation, durable logs, grants, OAuth, catalog aggregation, package asset resolution, and plugins page UI.
- Concrete plugins: Docker, Gmail, Office365 service registration, clients, executors, descriptors, settings, and plugin-specific assets.
- Workflow module/canvas: generic executor catalog consumption and menu/node rendering based on executor source/icon metadata.

The plugin runtime may know about `PluginId`, `PluginPackageId`, `WorkflowExecutorId`, source kind, trust level, and icon descriptors. It must not need to know that Docker, Gmail, or Office365 exist.

## Runtime Package Activation

Installed packages should be activated in two layers:

1. Read and validate the package manifest from the direct installed package root.
2. Load package assemblies and invoke explicit registrars/service hooks for services/executors/tools.

The installed package manifest must remain the catalog identity source. Avoid auto-registering `ICanDoItAllPlugin` from package assemblies unless the contract can prove it is package-aware and cannot emit bundled/application descriptors.

Recommended minimal direction:

- Replace scattered recursive manifest enumeration with a single package-root enumerator service.
- Keep package manifest validation strict on source/trust.
- Treat `ICanDoItAllPlugin` auto-registration from runtime packages as unsafe unless explicitly redesigned.
- Allow package assemblies to register `IWorkflowExecutor` and related operational services.
- Add integration fixture that packages a real test assembly with a registrar and executor.

## Logging Model

Introduce a durable plugin log store with two logical streams:

- Installation logs: package upload, validation, installation, enable/disable, restart required, activation, activation failure.
- Runtime logs: plugin executor started/completed/failed, plugin event emitted, runtime service failure.

The UI can show these as separate tabs or segmented views under the plugins page log subtab. The storage model may be one table with `PluginLogStreamKind` or two tables if existing persistence conventions strongly favor separation. Prefer one table with a typed stream enum unless query/index needs say otherwise.

Required fields:

- `PluginLogId`
- `PluginId?`
- `PluginPackageId?`
- `WorkflowExecutorId?`
- `PluginLogStreamKind`
- `PluginLogOperationKind`
- `PluginLogSeverity`
- `Status`
- `Message`
- `DetailsJson`
- `CorrelationId` or `OperationId`
- `CreatedAtUtc`

Redaction must be centralized. Reuse the existing workflow executor audit redaction behavior where possible and add plugin-specific redaction for settings, OAuth, command args, and package content.

## Workflow Canvas Menu

Build the right-click menu from executor descriptors like this:

- Layer 1: node creation choices.
- Layer 2 under `Executors`: built-in executor categories and one generic `Plugins` action/icon when plugin executors exist.
- Layer 3 under `Plugins`: one action per plugin, using the plugin icon/name.
- Layer 4 under each plugin: exact plugin executors.

If CanvasLib strictly counts the `Executors` submenu as layer 2 and the plugin submenu as layer 3, the plugin action itself is the third layer and exact executors render as that action's children. Keep the user-visible requirement intact: plugin executors must not be direct children of `Executors`.

Grouping key:

- Plugin executor: `WorkflowExecutorDescriptor.Source.Kind != WorkflowExecutorSourceKind.BuiltIn` and `Source.PluginId` has a value.
- Plugin group label/icon: resolve from plugin catalog by `PluginId`; fall back to source display name/icon descriptor only through explicit fallback logic.

## Icon Contract

Prefer a typed icon descriptor that can represent:

- Material symbol fallback
- Bundled static asset path
- Installed package asset path
- Optional brand asset metadata

Avoid passing raw string icon names across every layer. Existing `IconName` can be preserved for compatibility during migration, but workflow menu and plugin page should converge on a typed descriptor.

Package icons must be resolved through a safe asset service that:

- Normalizes paths.
- Rejects path traversal.
- Does not expose arbitrary package files.
- Supports cache busting/versioning by package id/version or install timestamp.

## Performance And EF

The target runtime should avoid repeated sync database reads while rendering catalogs or descriptors. Catalog/list operations should batch reads per request and push latest-row selection into the database. Any remaining in-memory filtering must be bounded, justified, and covered by tests.

## Docker Handoff End State

After the final subbundle:

- The app builds and starts without default Docker registration.
- Docker is packaged as a runtime ZIP.
- The ZIP has a checksum and an install verification note.
- The user can manually install the ZIP through the plugins page.
- The implementation report proves Docker executors appear only after package install/activation.
