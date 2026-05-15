# Assumptions And Risks

## Assumptions

- Runtime package zips use a manifest named `plugin.package.json` or `plugin.manifest.json`.
- A package manifest can describe the plugin using the existing strongly typed `PluginDescriptor`.
- Package assemblies are compiled against the public plugin abstractions and supported application contracts.
- Packages with assemblies are loaded at startup through `AssemblyLoadContext`; current app provider mutation is out of scope.
- The restart action gracefully stops the app. A hosting supervisor such as dotnet watch, the manager, IIS, or another process host is responsible for starting it again.

## Critical Path Risks

- If the plugin split leaves module references to concrete plugin types, the architecture goal is only cosmetic.
- If package install only changes UI state, the runtime package requirement is not solved.
- If zip extraction is not path-safe, upload becomes a security risk.
- If restart-required state is not persisted, users can lose the instruction after navigating away.
- If the restart action does not call host lifetime, users still need Task Manager.

## Validation Risks

- Existing tests import plugin constants from `CanDoItAll.Modules.Plugins`; moving files while preserving namespace reduces churn, but project references must still make those types available.
- Browser proof can pass visually while package services are not wired. UI validation must be paired with service/API tests.
- A full solution build may be blocked by a running web process. If so, validate with isolated output paths and targeted tests, then document the lock.

## Reopen Triggers

- Reopen SB01 if `CanDoItAll.Modules.Plugins` still directly registers concrete Docker/Gmail/Office365 types after the split.
- Reopen SB02 if uploaded package manifests do not appear in `PluginCatalogService.ListCatalogAsync`.
- Reopen SB02 if tests show path traversal or invalid manifests are accepted.
- Reopen SB03 if `/plugins` hides restart-required state after a package install.
- Reopen SB03 if restart only updates UI state and does not call `IHostApplicationLifetime.StopApplication`.
- Reopen SB04 if any existing plugin catalog/OAuth/workflow test regresses.
