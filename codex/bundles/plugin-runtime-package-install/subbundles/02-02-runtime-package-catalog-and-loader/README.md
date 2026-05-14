# 02-runtime-package-catalog-and-loader

## Status

- `Completed`

## Objective

Add runtime package services that install plugin zips from a configured catalogue or uploaded stream, validate manifests and zip paths, expose installed package descriptors in the plugin catalog, register package workflow executor assemblies at startup, and persist restart-required state.

## Covered Inputs

- `N003`, `N004`, `N005`, `N010`
- Requirements: `R005`, `R006`, `R007`, `R008`, `R009`, `R010`, `R011`

## Prerequisites

- SB01 completed and proved.
- Plugin module no longer directly owns concrete bundled plugin registrations.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Catalog\PluginCatalogServices.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Catalog\PluginCatalogModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Services\PluginsModuleServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Plugins.Abstractions\PluginManifestContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Plugins.Abstractions\PluginManifestValidation.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\PluginsApi.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Support\TestHostApplicationLifetime.cs`

## Deliverables

- Strongly typed package manifest, catalogue item, install result, and restart status models.
- Package store/service with catalogue listing, catalogue install, uploaded zip install, installed package listing, and restart-required state.
- Runtime package catalog source that contributes installed package manifests to `PluginCatalogService`.
- Startup assembly scanner/registration for installed package executors.
- API endpoints for catalogue packages, catalogue install, upload install if implemented through API, restart status, and restart request.
- Integration/unit tests for manifest validation, zip path rejection, catalogue install, upload install, catalog visibility, startup loading, and restart status.

## Dependency Impact

- SB03 depends on these services.
- If install results, restart state, or catalogue list are local-only or fake, the UI cannot close.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Add package option/path helpers with safe defaults rooted under the application content root or configured paths.
2. Add package manifest models that wrap existing `PluginDescriptor`.
3. Add zip read/extract validation with traversal rejection and bounded package size.
4. Add package install service methods for catalogue package and uploaded stream.
5. Add installed package catalog source.
6. Add startup assembly loader for installed package executor types.
7. Add restart-required marker/status and graceful restart service.
8. Add API endpoints and targeted tests.

## Scope Exceptions

- Remote marketplace browsing is not implemented in this phase; local configured catalogue packages are the concrete source.
- Package assemblies installed while the process is running are not registered until restart.

## Do Not Do

- Do not auto-grant plugin capabilities during package install.
- Do not swallow loader exceptions without a visible package error/status.
- Do not extract zip entries outside the installed package root.
- Do not add raw string identifiers when a strongly typed plugin id/package id exists.

## Acceptance Checklist

- Valid package zip installs and appears in catalog.
- Catalogue install and upload install share validation.
- Invalid manifest returns explicit validation errors.
- Zip traversal returns explicit failure.
- Package with assemblies returns restart-required status.
- Startup loader registers package executor types when present.

## Proof Required

- Targeted integration/unit tests for package service and catalog source.
- API test for catalogue and restart endpoints.
- Execution report SB02 gate row updated.

## Browser Validation Logging

- `N/A` for this subbundle. UI proof belongs to SB03.

## Progression Gate

- SB03 may start only after package install services and restart status pass targeted tests.

## Suggested Agent Prompt

```text
Implement SB02 only. Add runtime package install/catalog/startup-loader/restart-status services and tests. Preserve grant separation and fail explicitly on invalid package inputs.
```
