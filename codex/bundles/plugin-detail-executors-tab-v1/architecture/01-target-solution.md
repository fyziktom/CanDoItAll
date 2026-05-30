# Target Solution

## Target State

- `PluginsPage.razor` adds an `Executors` tab alongside the existing plugin detail tabs.
- The tab reads `selectedPlugin.Descriptor.WorkflowExecutors` directly from the selected plugin catalog item.
- Executor rows show stable, scan-friendly metadata:
  - executor name
  - executor id
  - category
  - description or instruction text
  - default policy and permission posture when useful and already strongly typed
- A no-executors empty state is rendered for plugins that do not contribute workflow executors.

## Boundaries

- UI stays in the Plugins module.
- Helper methods for badge text, test ids, and policy labels may live in `PluginsPageHelpers`.
- Plugin manifests remain the source of truth. The UI must not build a second executor catalog or duplicate plugin-specific executor lists.

## Side Effects

- No database schema changes.
- No plugin package manifest changes.
- No workflow runtime registration changes.
