# Source Artifacts

## Prior Bundle

- `C:\repositories\CanDoItAll\codex\bundles\plugin-runtime-package-install\README.md`
- `C:\repositories\CanDoItAll\codex\bundles\plugin-runtime-package-install\analysis\01-current-state.md`
- `C:\repositories\CanDoItAll\codex\bundles\plugin-runtime-package-install\architecture\01-target-solution.md`
- `C:\repositories\CanDoItAll\codex\bundles\plugin-runtime-package-install\reviews\01-execution-report.md`

The prior bundle is complete and moved plugin implementations under `src/plugins`, added runtime package catalog/upload/restart UI, and captured validation/browser proof. This follow-up assumes that work is the baseline and does not reopen it except where hardening is required.

## Repo Evidence

- `C:\repositories\CanDoItAll\src\CanDoItAll.Composition\RuntimeHostServiceCollectionExtensions.cs:53` registers the plugin module.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Composition\RuntimeHostServiceCollectionExtensions.cs:54` registers Docker directly as a default plugin.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Composition\RuntimeHostServiceCollectionExtensions.cs:55` registers Gmail directly as a default plugin.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Composition\RuntimeHostServiceCollectionExtensions.cs:56` registers Office365 directly as a default plugin.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Catalog\PluginPackageServices.cs:299` recursively scans installed manifests.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Catalog\PluginPackageServices.cs:514` rejects runtime package manifests that claim bundled source.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Catalog\PluginPackageServices.cs:787` recursively scans manifests again for runtime assembly registration.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Catalog\PluginPackageServices.cs:800` invokes assembly registrars.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Catalog\PluginPackageServices.cs:801` auto-registers `ICanDoItAllPlugin` from package assemblies.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Catalog\PluginCatalogServices.cs:186` still reports a missing bundled catalog.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Catalog\PluginCatalogServices.cs:270` falls back to bundled source kind for unavailable installed plugins.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Pages\PluginsPage.razor:24` still describes bundled plugin manifests.
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowExecutorContracts.cs:254` creates workflow executor audit records.
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowExecutorObservability.cs:34` defines `IWorkflowExecutorExecutionObserver`.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Services\AgentFrameworkModuleServiceCollectionExtensions.cs:88` binds the observer to the null observer.
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Hosting\AgentFrameworkServiceCollectionExtensions.cs:61` also binds the observer to the null observer.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Plugins.Abstractions\PluginExecutionContracts.cs:41` defines `PluginExecutionEvent`.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Plugins.Abstractions\PluginExecutionContracts.cs:275` defines `IPluginExecutionEvents`.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowExecutorCanvasCatalog.cs:14` builds right-click quick-create actions.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowExecutorCanvasCatalog.cs:39` currently places implemented executors directly under `Executors`.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Canvas\Workbench\CanvasWorkbenchChrome.cs` supports `CanvasWorkbenchAction.Children`.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\04-context-menu-and-composer.js` supports recursive context submenus.
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Workflows\WorkflowExecutorModels.cs:358` stores executor icons as `string IconName`.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Catalog\PluginPackageModels.cs:101` stores package `IconPath`.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Catalog\PluginPackageModels.cs:120` exposes package UI `IconPath`.
- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Docker\DockerBundledPlugin.cs:5` still uses the `CanDoItAll.Modules.Plugins` namespace.
- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Gmail\GmailBundledPlugin.cs:5` still uses the `CanDoItAll.Modules.Plugins` namespace.
- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Office365\Office365BundledPlugin.cs:5` still uses the `CanDoItAll.Modules.Plugins` namespace.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Catalog\PluginPermissionServices.cs:146` contains `FindFirstByKeyAsync`.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\OAuth\PluginOAuthService.cs:329` contains `ResolveWorkflowConnectionIdAsync`.
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:121` tests manifest-only package install/catalog behavior.
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\PluginsPageTests.cs:121` tests manifest-only package upload from the plugins page.

## Icon And Brand Sources For Implementation Review

- Docker media resources: `https://www.docker.com/company/newsroom/media-resources/`
- Docker trademark guidelines: `https://www.docker.com/legal/trademark-guidelines/`
- Google Brand Resource Center product icons: `https://about.google/brand-resource-center/logos-list/`
- Google brand elements: `https://about.google/brand-resource-center/brand-elements/`
- Microsoft 365 add-in icon guidance: `https://learn.microsoft.com/en-us/office/dev/add-ins/design/microsoft-365-extension-management-icons`
- Simple Icons repository and license: `https://github.com/simple-icons/simple-icons`

Use these sources to choose or verify local assets. Do not hotlink external assets at runtime.
