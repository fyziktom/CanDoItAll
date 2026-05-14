# Structured Input

## Objectives

- Separate plugin implementations from plugin runtime governance by creating `src/plugins` projects for Docker, Gmail, Office365, and shared email support.
- Keep the separated plugin projects in `CanDoItAll.slnx` and wire bundled plugins through composition.
- Add runtime package services that can install a plugin zip from a configured catalogue directory or uploaded browser file.
- Support package zips with a manifest, libraries, and optional icon while validating paths and manifest contents.
- Make uploaded or downloaded package manifests visible in the plugin catalog without requiring application compilation.
- Register package assemblies at startup when a package contains workflow executor implementations.
- Persist and expose restart-required state for packages whose assemblies need startup registration.
- Add a user-facing restart action so users do not need Task Manager.
- Prove existing Docker/Gmail/Office365 behavior still works.

## Assumptions

- "Plugin catalogue" means a configured local catalogue source for this pass. The package model should allow a future remote feed, but this implementation does not need a public marketplace service.
- "Without additional compilation" means users can install a zip containing compiled assemblies and metadata; the application may require restart to load new assemblies into DI.
- Runtime package services may safely defer executable service registration until startup because ASP.NET Core DI service collections are immutable after the provider is built.
- Icon support means package metadata accepts an icon file and keeps it with the installed package. Rich icon rendering can be improved later if visual requirements expand.

## Validation Expectations

- The old bundled catalog still exposes Docker, Gmail, and Office365 after implementation split.
- Package zip install rejects missing manifest, invalid manifest, and path traversal entries.
- Package zip install stores package contents and returns restart-required state for packages with assemblies.
- Catalogue install and upload install both use the same package validation path.
- `/plugins` renders catalogue package install controls, upload control, and restart-required call to action.
- Restart request calls `IHostApplicationLifetime.StopApplication` through an explicit service or API.
