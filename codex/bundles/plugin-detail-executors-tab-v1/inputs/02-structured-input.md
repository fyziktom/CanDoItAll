# Structured Input

## Core Objective

- Add a plugin-detail `Executors` tab that lists the selected plugin's workflow executors with plugin-owned short descriptions or instructions.

## Success Criteria

- Selected plugin detail includes an `Executors` tab.
- Executor rows are rendered from `selectedPlugin.Descriptor.WorkflowExecutors`.
- Each row includes executor name, id, category, and descriptor description.
- A plugin with no workflow executors shows an intentional empty state.
- Component tests, build, and browser-visible proof or a documented browser blocker support closure.

## Hard Constraints

- Do not introduce a second executor registry or hard-coded per-plugin executor text.
- Do not change workflow runtime execution, plugin grants, OAuth behavior, package installation, or persistence.
- Use the existing plugin page component patterns and strongly typed descriptor contracts.

## Allowed Side Effects

- `repo://src/CanDoItAll.Modules.Plugins/Pages/PluginsPage.razor` may change.
- `repo://src/CanDoItAll.Modules.Plugins/Pages/PluginsPageHelpers.cs` may change for small helper methods.
- `repo://tests/CanDoItAll.Tests.Components/PluginsPageTests.cs` may change for focused component tests.
- Bundle proof artifacts may be added under `bundle://proof/SB01/`.

## Source Artifacts

- `inputs/00-original-request.md`
- `inputs/01-source-artifacts.md`

## Input Coverage Signals

- `N001`: Add executor list to plugin detail as another tab.
- `N002`: Load executor info dynamically from each plugin.
- `N003`: Show short description or instructions.
- `N004`: Each plugin must carry this info inside itself.

## Dependency And Sequencing Signals

- One implementation subbundle is enough because existing descriptors already carry executor metadata.
- Final closure depends on proving descriptor-driven rendering; hard-coded Office365 or Docker rows would not satisfy the raw request.

## Validation Expectations

- Component test proves a plugin with multiple executors shows descriptor-owned executor names and descriptions.
- Component test proves a plugin with no executors shows a no-executors empty state.
- Browser or rendered UI proof verifies the `/plugins` detail tab is visible and readable at desktop and narrow widths.

## Evidence Contract

- Prepared-stage bundle validator transcript.
- Targeted component test transcript.
- Plugin module build transcript.
- Anti-stub audit transcript.
- Changed-file SHA-256 hashes.
- Browser screenshots or explicit browser blocker under `proof/SB01/`.
- Completed-stage bundle validator transcript.

## UI Validation Strategy

- Use `/plugins` as the route.
- Validate desktop first at `1600x900`, then narrow width at `390x844`.
- Review tab visibility, row readability, description wrapping, and absence of overlap.

## Browser Validation Analytics

- Record route, viewport, actions, screenshots, and result in `reviews/01-execution-report.md`.

## Working Assumptions

- "Instructions" maps to `PluginWorkflowExecutorDescriptor.Description`; no new descriptor field is required unless implementation proves descriptions are insufficient.
- `PluginCatalogItem.Descriptor` remains the active plugin manifest snapshot source for both bundled and package plugins.

## Primary Risks

- The implementation could accidentally render known bundled executors instead of selected descriptor data.
- The new tab could be technically present but visually hard to scan on narrow layouts.
