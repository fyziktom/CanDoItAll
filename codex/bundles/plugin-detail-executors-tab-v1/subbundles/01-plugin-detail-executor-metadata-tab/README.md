# Plugin detail executor metadata tab

## Status

- `Completed`

## Objective

- Add a plugin-detail `Executors` tab that lists the selected plugin's descriptor-owned workflow executors with names, ids, categories, and short descriptions or instructions.

## Success Criteria

- The plugin detail tab set includes an `Executors` tab with a badge count equal to `selectedPlugin.Descriptor.WorkflowExecutors.Count`.
- The tab renders executor metadata from `selectedPlugin.Descriptor.WorkflowExecutors`.
- Executor rows include at least name, executor id, category, and descriptor description.
- A plugin with no executor descriptors shows an intentional empty state.
- Targeted tests, build, browser proof or documented browser blocker, proof manifest, and semantic invariant contract are recorded.

## Covered Inputs

- `N001`: Add plugin-detail executor list as another tab.
- `N002`: Load executor info dynamically from each plugin.
- `N003`: Show short description or instructions.
- `N004`: Each plugin must carry this info inside itself.

## Prerequisites

- Prepared-stage bundle validator passes.
- `PluginCatalogItem.Descriptor.WorkflowExecutors` remains available to `PluginsPage.razor`.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Plugins/Pages/PluginsPage.razor`
- `repo://src/CanDoItAll.Modules.Plugins/Pages/PluginsPageHelpers.cs`
- `repo://src/CanDoItAll.Modules.Plugins/Catalog/PluginCatalogModels.cs`
- `repo://src/CanDoItAll.Plugins.Abstractions/PluginManifestContracts.cs`
- `repo://src/plugins/CanDoItAll.Plugin.Office365/Office365BundledPlugin.cs`
- `repo://tests/CanDoItAll.Tests.Components/PluginsPageTests.cs`

## Deliverables

- New plugin-detail `Executors` tab in `PluginsPage.razor`.
- Any small helper methods needed for executor badge text, test ids, or labels.
- Focused `PluginsPageTests` coverage for plugins with and without workflow executors.
- `proof/SB01/manifest.md`, `proof/SB01/semantic-invariants.md`, transcripts, source assertions, and browser artifacts or explicit browser blocker.

## Dependency Impact

- Final closure depends on this phase. Weak proof would invalidate the raw request because a hard-coded executor list or missing description text would falsely appear complete for only known bundled plugins.

## Validation Depth

- `Critical UI/data foundation`: component-test, build, browser-visible proof, semantic adequacy evidence, anti-stub audit, changed-file hashes, and raw-note literal closure.

## Implementation Steps

1. Add helper methods only if needed to format executor badge count, policy labels, or stable data-test ids.
2. Add an `Executors` `TabsItem` in `PluginsPage.razor`.
3. Render `selectedPlugin.Descriptor.WorkflowExecutors` with existing component-library layout patterns.
4. Add an empty state for `WorkflowExecutors.Count == 0`.
5. Add component tests for the Office365 executor list and no-executors empty state.
6. Run targeted tests and build.
7. Run browser validation for `/plugins` at desktop and narrow widths when local app startup is available.
8. Update proof artifacts, execution report, subbundle status, and root validation summary.

## Scope Exceptions

- No new plugin descriptor field is added. The existing `PluginWorkflowExecutorDescriptor.Description` is treated as the short description or instruction source.
- Package plugin loading is not separately changed; package manifests already hydrate `PluginCatalogItem.Descriptor`.

## Do Not Do

- Do not add a second executor catalog for the plugin page.
- Do not hard-code Office365, Gmail, Docker, or any other plugin executor rows in the UI.
- Do not change workflow runtime invocation, executor registration, grants, OAuth, or package installation.
- Do not introduce XML documentation comments.

## Acceptance Checklist

- `PluginsPage.razor` contains an `Executors` tab under selected plugin detail.
- The tab iterates `selectedPlugin.Descriptor.WorkflowExecutors`.
- Office365 executor names and descriptions render in component tests.
- No-executor plugin descriptor renders a no-executors empty state.
- Browser proof or a recorded blocker covers tab visibility, row readability, wrapping, and narrow-width layout.

## Proof Required

- Failing-first or source assertion artifact proving the old page had no executor tab and the new implementation reads descriptor executors.
- Passing transcript for `dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter PluginsPageTests`.
- Passing transcript for `dotnet build src/CanDoItAll.Modules.Plugins/CanDoItAll.Modules.Plugins.csproj`.
- Anti-stub audit transcript checking for hard-coded plugin executor rows and production `TODO` or `NotImplemented` paths in changed production files.
- `proof/SB01/manifest.md` with changed-file SHA-256 hashes and portable proof references.
- `proof/SB01/semantic-invariants.md` covering shallow-pass trap, adversarial no-executors case, semantic positive case, anti-stub audit, and raw-note literal closure.
- Browser screenshots for desktop and narrow widths, or a clear blocker with the strongest available component-rendered substitute.

## Browser Validation Logging

- Route: `/plugins`.
- Viewports: desktop `1600x900` first, then narrow `390x844`.
- Actions: navigate to `/plugins`, select a plugin with workflow executors, open the `Executors` tab, assert executor rows and description text, capture screenshot.
- Screenshots: `proof/SB01/browser/plugins-executors-desktop.png` and `proof/SB01/browser/plugins-executors-narrow.png`.
- Review questions: tab is visible, rows are readable, descriptions wrap cleanly, no content overlaps, empty state is intentional for a no-executor plugin if reachable.

## Progression Gate

- Final closure may proceed only after tests, build, semantic proof, anti-stub audit, raw-note closure, and browser proof or documented browser blocker are recorded in `reviews/01-execution-report.md` and `proof/SB01/manifest.md`.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
