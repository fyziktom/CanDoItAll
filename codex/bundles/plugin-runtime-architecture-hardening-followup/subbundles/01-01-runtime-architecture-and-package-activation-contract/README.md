# SB01 Runtime Architecture And Package Activation Contract

## Status

- `Ready`

## Objective

Make runtime package activation generic and source-safe so installed packages can contribute executable services/executors without contributing bundled plugin descriptors or old module-owned identity.

## Success Criteria

- Runtime package manifests remain the source of truth for installed package/plugin identity.
- Package assembly activation cannot auto-register bundled `ICanDoItAllPlugin` descriptors.
- Installed package manifest discovery is direct-root only, not recursive.
- Bundled-only UI/catalog wording and fallback identity are corrected where they belong to the generic runtime.
- A real package assembly fixture proves executor/service activation after startup/restart.

## Covered Inputs

- PRH-001 Runtime Package Activation Contract
- PRH-002 Direct Installed Manifest Discovery
- PRH-003 Generic Runtime Cleanup
- FIND-001, FIND-002, FIND-004, FIND-005, FIND-012

## Prerequisites

- Prior bundle `C:\repositories\CanDoItAll\codex\bundles\plugin-runtime-package-install` remains the baseline.
- Read `C:\repositories\CanDoItAll\codex\bundles\plugin-runtime-architecture-hardening-followup\analysis\01-current-state.md`.
- Read `C:\repositories\CanDoItAll\codex\bundles\plugin-runtime-architecture-hardening-followup\architecture\01-target-solution.md`.
- Read the `Runtime Package Activation` rows in `inventories/plugin-runtime-architecture-hardening-checklist.xlsx`.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Catalog\PluginPackageServices.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Catalog\PluginCatalogServices.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Pages\PluginsPage.razor`
- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Docker\DockerBundledPlugin.cs`
- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Gmail\GmailBundledPlugin.cs`
- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Office365\Office365BundledPlugin.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs`

## Deliverables

- One shared installed-package-root enumerator used by package listing and runtime assembly activation.
- Package activation contract that prevents installed package assemblies from registering bundled plugin descriptors.
- Package assembly integration test fixture that registers at least one workflow executor/service.
- Catalog/plugins page text corrected from bundled-only language to generic package/plugin language.
- Execution report entries for the activation contract and tests.

## Dependency Impact

- SB02 through SB06 depend on this subbundle. If runtime packages can still register bundled descriptors, log attribution, menu grouping, icon resolution, performance proof, and Docker ZIP handoff are all unreliable.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Inspect current package activation flow from manifest installation through `RuntimePluginAssemblyRegistrar`.
2. Introduce or extract a direct installed-package-root enumeration helper and replace recursive manifest scans.
3. Add tests for nested `plugin.package.json` under an installed package root.
4. Remove or constrain package assembly auto-registration of `ICanDoItAllPlugin`; keep service/executor/tool registration explicit and package-safe.
5. Add a runtime package assembly test fixture with a registrar and `IWorkflowExecutor`.
6. Prove the installed package manifest supplies plugin source/trust/package identity while the assembly supplies executable services.
7. Update bundled-only catalog/plugins page messages and unavailable fallback identity.
8. Review concrete plugin namespace/project-reference leftovers and fix only the changes needed to support the generic boundary safely.
9. Run targeted tests and update the execution report.

## Scope Exceptions

- Do not remove Docker default registration in this subbundle; SB06 owns that.
- Do not redesign all plugin descriptors if a smaller source-safe package activation contract closes the issue.
- Do not implement plugin logs here; SB02 owns logging.

## Do Not Do

- Do not add silent fallbacks that register bundled descriptors when package activation fails.
- Do not hard-code Docker/Gmail/Office365 into generic package activation.
- Do not weaken manifest validation to accept bundled/application trust for runtime packages.

## Acceptance Checklist

- [ ] Recursive installed manifest scans are gone or unreachable.
- [ ] Installed package assembly activation no longer auto-registers bundled plugin descriptors.
- [ ] Real package assembly fixture executor appears in the executor catalog after activation.
- [ ] Nested manifest test passes.
- [ ] Plugins page/catalog wording no longer implies all plugins are bundled.
- [ ] Execution report records tests and residual risks.

## Proof Required

- `dotnet test` command covering package activation integration tests.
- Source inspection note for `PluginPackageServices.cs` activation and manifest enumeration.
- Browser proof for `/plugins` only if this subbundle changes visible page wording or package state display.
- Execution report update with command output summary and changed files.

## Browser Validation Logging

- Target route: `/plugins` if UI text/catalog state was changed.
- Required viewport passes: maximized desktop and one narrower width if text/layout changed.
- Required actions: open plugins page, inspect catalog summary/empty/unavailable text, confirm no stale bundled-only language for generic package states.
- Screenshot evidence: `artifacts/sb01-plugins-generic-wording-desktop.png` and optional `artifacts/sb01-plugins-generic-wording-narrow.png`.
- Review questions: Does the page describe installed/bundled/runtime packages accurately? Does any visible text imply installed packages must be bundled?

## Progression Gate

- SB02-SB06 may continue only after a real package assembly activation test passes and installed packages cannot contribute bundled catalog descriptors.

## Suggested Agent Prompt

```text
Implement SB01 only from C:\repositories\CanDoItAll\codex\bundles\plugin-runtime-architecture-hardening-followup.
Focus on the runtime package activation contract, direct manifest discovery, and generic runtime cleanup. Add a real package assembly fixture test. Do not remove Docker default registration yet. Capture required proof and update reviews/01-execution-report.md.
```
