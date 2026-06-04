# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| `N001`: Add plugin-detail executor list as another tab. | `requirements/01-normalized-requirements.md#REQ-001` | `subbundles/01-plugin-detail-executor-metadata-tab` | `dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter PluginsPageTests` and browser proof for `/plugins` | UI surface only. |
| `N002`: Load executor info dynamically from each plugin. | `requirements/01-normalized-requirements.md#REQ-002` | `subbundles/01-plugin-detail-executor-metadata-tab` | Component test uses descriptor-owned Office365 executors and no-executor descriptor case. | No hard-coded per-plugin executor list allowed. |
| `N003`: Show short description or instructions. | `requirements/01-normalized-requirements.md#REQ-003` | `subbundles/01-plugin-detail-executor-metadata-tab` | Component test asserts executor names and descriptions from descriptors. | Existing `PluginWorkflowExecutorDescriptor.Description` is the instruction source unless proven insufficient. |
| `N004`: Each plugin must carry this info inside itself. | `architecture/01-target-solution.md#target-state` | `subbundles/01-plugin-detail-executor-metadata-tab` | Source assertion that UI reads `selectedPlugin.Descriptor.WorkflowExecutors`; no separate registry. | Existing plugin manifests already own executor descriptors. |
