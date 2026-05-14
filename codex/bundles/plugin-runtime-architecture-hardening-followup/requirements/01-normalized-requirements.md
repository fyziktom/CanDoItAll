# Normalized Requirements

## PRH-001 Runtime Package Activation Contract

Installed runtime package manifests must be the source of truth for package identity, plugin identity, source kind, trust level, manifest snapshot, and package metadata. Package assemblies may register services, workflow executors, tool recipe catalogs, and explicit startup hooks, but must not auto-contribute bundled `ICanDoItAllPlugin` descriptors for installed packages.

Success criteria:

- Runtime package assemblies cannot register a bundled plugin descriptor through auto-discovery.
- A package assembly fixture with a workflow executor loads after startup/restart and appears in the executor catalog.
- Duplicate plugin/package identity validation still catches real conflicts.

## PRH-002 Direct Installed Manifest Discovery

Installed package discovery must enumerate only direct package roots under the configured installed root. Nested `plugin.package.json` files must not be treated as installed packages.

Success criteria:

- Package listing and runtime assembly registration use the same direct-root discovery logic.
- Tests cover a nested manifest inside an installed package and prove it is ignored or rejected predictably.

## PRH-003 Generic Runtime Cleanup

The generic plugin runtime must not contain direct concrete Docker/Gmail/Office365 assumptions except intentional default registration in composition and test fixtures. Bundled-only wording and fallback source/trust values must be made generic or derived from persisted snapshots.

Success criteria:

- Plugins page and catalog messages no longer imply every plugin is bundled.
- Unavailable installed packages preserve source/trust from the snapshot where available and do not default to bundled identity.
- Docker can be removed from default composition without breaking generic plugin runtime build/startup.

## PRH-004 Durable Installation Logs

Plugin installation/package lifecycle operations must write durable user-facing log records that can explain package upload, validation failure, install, enable/disable, restart-required, and activation failures.

Success criteria:

- Logs include plugin id/package id when known, operation type, severity, timestamp, correlation/operation id, status, redacted message, and actionable details.
- Sensitive settings, OAuth values, package secrets, and command arguments are redacted.
- Tests prove records are written for success and failure paths.

## PRH-005 Durable Runtime Logs

Plugin workflow executor/runtime usage must write durable plugin runtime log records from the existing workflow executor audit/event surfaces.

Success criteria:

- `IWorkflowExecutorExecutionObserver` or equivalent bridge persists plugin executor started/completed/failed records.
- `IPluginExecutionEvents` has a real implementation or clear bridge into the durable log service.
- Built-in executor logs do not pollute plugin runtime logs unless explicitly filtered in.

## PRH-006 Plugins Page Logs Subtab

The plugins page must expose a plugin logs subtab that separates installation logs from runtime logs and supports sorting/filtering by plugin/package/time/severity.

Success criteria:

- Installation logs and runtime logs are visibly distinct.
- Current plugin selection filters logs by default, with a clear all-plugins option if the existing page pattern supports it.
- Browser proof captures log subtab behavior with large and narrower viewport checks.

## PRH-007 Workflow Canvas Plugin Executor Menu

The workflow canvas right-click menu must group plugin executors under one generic plugin icon in the second `Executors` layer, then open a third layer grouped by plugin, and then list exact plugin executors.

Success criteria:

- Plugin executors no longer appear directly in the second menu layer.
- Built-in executors remain discoverable.
- Office365-style plugins with many executors have a dedicated plugin layer.
- Tests and browser proof cover nested menu behavior.

## PRH-008 Plugin Icon Contract

Docker, Gmail, and Office365 must have plugin icons usable in plugins page, workflow menu, and workflow executor node rendering. The model must avoid stringly typed icon routing.

Success criteria:

- A typed icon descriptor or equivalent strongly typed contract exists.
- Package icon assets are resolved safely from installed package content.
- Bundled/default plugin icons use reviewed local assets or documented Material icon fallbacks.
- Missing icon behavior is explicit, logged where useful, and visually stable.

## PRH-009 Performance And EF Hardening

Known performance and EF risks in plugin runtime paths must be fixed or explicitly deferred with evidence.

Success criteria:

- `FindFirstByKeyAsync` latest selection is pushed into EF.
- `ResolveWorkflowConnectionIdAsync` reduces and orders candidates before materialization.
- Executor descriptor availability no longer causes repeated sync database reads during catalog construction.
- Installed manifest scanning is direct-root and not recursive.

## PRH-010 Docker Default Disable And Package ZIP Handoff

Docker must be removed as a default app plugin and prepared as a runtime package ZIP that the user can manually install. The ZIP must be validated before handoff, but the app must end running without Docker registered by default.

Success criteria:

- Docker is not registered by default in app composition.
- The Docker package ZIP includes manifest, icon, assembly outputs, and required runtime dependencies.
- A test/startup pass proves the Docker ZIP can be installed and its executors become available after package activation.
- Final app run proof shows Docker absent until installed.

## PRH-011 Validation And Proof

Every subbundle must update the execution report with exact commands, tests, browser screenshots where relevant, and residual risks.

Success criteria:

- Bundle execution cannot close with TODO proof.
- Browser-visible changes include screenshot review notes.
- Package handoff includes file path, checksum, and install verification notes.
