# Current State

## Architecture Baseline

The previous bundle moved concrete plugins under `src/plugins` and added runtime package install/upload/restart surfaces. The current code is closer to a plugin runtime, but the activation contract still mixes bundled plugin registration with installed package activation.

The app composition layer still directly registers all three concrete plugins:

- `C:\repositories\CanDoItAll\src\CanDoItAll.Composition\RuntimeHostServiceCollectionExtensions.cs:54` registers Docker.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Composition\RuntimeHostServiceCollectionExtensions.cs:55` registers Gmail.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Composition\RuntimeHostServiceCollectionExtensions.cs:56` registers Office365.

That is acceptable only while those plugins are intentionally default/bundled. It becomes wrong for Docker after the requested ZIP handoff, because the app must end running without Docker registered by default.

## Runtime Package Activation Concern

Runtime package manifests reject bundled/application trust at `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Catalog\PluginPackageServices.cs:514`, but runtime package assembly registration still auto-registers `ICanDoItAllPlugin` implementations at `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Catalog\PluginPackageServices.cs:801`.

The concrete plugin assemblies expose bundled descriptors:

- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Docker\DockerBundledPlugin.cs:20`
- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Gmail\GmailBundledPlugin.cs:24`
- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Office365\Office365BundledPlugin.cs:28`

If Docker is packaged as a runtime ZIP without changing this contract, package activation can load the assembly, register bundled descriptors, and conflict with the installed package manifest identity. The implementation must make the runtime manifest the source of truth for installed packages and prevent package assemblies from contributing bundled catalog identities.

## Manifest Discovery Concern

Installed package manifest discovery is recursive in two places:

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Catalog\PluginPackageServices.cs:299`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Catalog\PluginPackageServices.cs:787`

Because package extraction copies arbitrary files under the package root after validation, recursive discovery can treat a nested `plugin.package.json` as an installed package. The correct shape is direct package-root discovery, for example `InstalledRootPath\<package-root>\plugin.package.json`, with package root validation.

## Observability Gap

Workflow execution has an audit model:

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowExecutorObservability.cs:15`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowExecutorObservability.cs:34`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowExecutorContracts.cs:254`

But default registrations bind the observer to null:

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Services\AgentFrameworkModuleServiceCollectionExtensions.cs:88`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Hosting\AgentFrameworkServiceCollectionExtensions.cs:61`

Plugin abstractions also define plugin execution events:

- `C:\repositories\CanDoItAll\src\CanDoItAll.Plugins.Abstractions\PluginExecutionContracts.cs:41`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Plugins.Abstractions\PluginExecutionContracts.cs:275`

No durable implementation was found in the inspected paths. Installation/package/OAuth work is currently diagnosable mainly through `ILogger` and current UI state, which is not enough for the requested plugins page log subtab.

## Generic Runtime Leftovers

Several UI and catalog strings still describe the old bundled-only model:

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Pages\PluginsPage.razor:24`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Pages\PluginsPage.razor:131`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Pages\PluginsPage.razor:137`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Catalog\PluginCatalogServices.cs:186`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Catalog\PluginCatalogServices.cs:276`

Concrete plugin projects also still use the `CanDoItAll.Modules.Plugins` namespace even though they live under `src/plugins`, for example:

- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Docker\DockerBundledPlugin.cs:5`
- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Gmail\GmailBundledPlugin.cs:5`
- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Office365\Office365BundledPlugin.cs:5`

The implementation should clean naming and dependency direction where it can be done safely. The important boundary is not namespace cosmetics alone: the generic runtime must not depend on concrete Docker/Gmail/Office365 implementations after Docker is removed from default registration.

## Workflow Canvas Menu State

`WorkflowExecutorCanvasCatalog.BuildQuickCreateActions` builds the right-click executor menu at `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowExecutorCanvasCatalog.cs:14`. It currently places implemented executors directly under `Executors` at `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowExecutorCanvasCatalog.cs:39`.

CanvasLib already supports nested action children:

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Canvas\Workbench\CanvasWorkbenchChrome.cs:104`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\04-context-menu-and-composer.js:617`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\04-context-menu-and-composer.js:644`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\04-context-menu-and-composer.js:711`

The likely implementation path is to rebuild the action tree, not to rewrite CanvasLib.

## Icon State

Executors currently carry `string IconName` at `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Workflows\WorkflowExecutorModels.cs:358`. Package manifests carry `IconPath` at `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Catalog\PluginPackageModels.cs:101`, but plugin descriptors do not expose a reusable icon contract for plugin page, context menu, and executor node rendering.

The bundle should lead implementation toward a typed icon model instead of spreading raw icon strings through UI code.

## Test Coverage State

Existing runtime package tests are mostly manifest-only:

- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:121`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:160`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\PluginCatalogIntegrationTests.cs:703`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\PluginsPageTests.cs:121`

The follow-up needs tests that prove an installed package assembly can register executors/services after restart/startup without registering bundled catalog identities.
