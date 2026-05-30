# Implementation Prompt

Implement only `SB01` for `plugin-detail-executors-tab-v1`.

- First verify the selected plugin detail page still exposes `PluginCatalogItem.Descriptor.WorkflowExecutors`.
- Add the smallest UI change to `PluginsPage.razor` that creates an `Executors` tab and renders descriptor-owned executor metadata.
- Add helper methods only where they remove duplication or produce stable test ids and badge text.
- Add focused component tests in `PluginsPageTests`.
- Do not change workflow runtime execution, plugin package install behavior, OAuth behavior, grants, or persistence.
- Capture required proof under `proof/SB01/`, update `reviews/01-execution-report.md`, and stop if dynamic descriptor-driven rendering cannot be proven.
