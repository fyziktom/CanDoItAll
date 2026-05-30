# Current State

## Relevant Repo Observations

- `repo://src/CanDoItAll.Modules.Plugins/Pages/PluginsPage.razor` renders the plugin detail surface with `Tabs` and existing `TabsItem` entries for main info, settings, connections, logs, and grants. It does not render plugin workflow executor descriptors.
- `repo://src/CanDoItAll.Modules.Plugins/Catalog/PluginCatalogModels.cs` keeps the full `PluginDescriptor` on `PluginCatalogItem.Descriptor`, so the page can access `Descriptor.WorkflowExecutors` without another service call.
- `repo://src/CanDoItAll.Plugins.Abstractions/PluginManifestContracts.cs` defines `PluginWorkflowExecutorDescriptor` with `ExecutorId`, `Name`, `Description`, `Category`, shapes, settings schema, default policy, permission policy, and deterministic test mode metadata.
- `repo://src/CanDoItAll.Modules.Plugins/Catalog/PluginWorkflowExecutorDescriptorSource.cs` already converts `PluginDescriptor.WorkflowExecutors` into workflow catalog descriptors, which confirms the plugin manifest is the canonical source for plugin executor metadata.
- Bundled plugins already populate descriptor-owned executors:
  - `repo://src/plugins/CanDoItAll.Plugin.Office365/Office365BundledPlugin.cs` declares three Office365 workflow executors with descriptions.
  - `repo://src/plugins/CanDoItAll.Plugin.Docker/DockerBundledPlugin.cs` declares four Docker workflow executors with descriptions.
- `repo://tests/CanDoItAll.Tests.Components/PluginsPageTests.cs` has bUnit coverage for the plugin page and already exercises tab selection for settings, connections, and logs.

## Current Gap

- Users can select a plugin and see identity, settings, OAuth connections, logs, and grants, but cannot see which workflow executors the plugin contributes.
- The UI already has the necessary selected plugin descriptor data; the gap is presentation and targeted proof, not new persistence or runtime plumbing.
