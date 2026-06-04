# Assumptions And Risks

## Assumptions

- "Instructions" in the raw request maps to the short user-facing guidance already carried by `PluginWorkflowExecutorDescriptor.Description`.
- `PluginCatalogItem.Descriptor.WorkflowExecutors` is the correct dynamic data source because each plugin owns that manifest data.
- Existing page component patterns are sufficient; no new shared component is needed for this narrow detail tab.

## Critical Path Risks

- `SB01` is the only critical foundation. If it hard-codes executor rows or loses descriptor data for package plugins, the feature would appear correct only for bundled fixtures and would violate the dynamic loading requirement.
- If executor descriptions are treated as optional and hidden too aggressively, the tab may fail the "short description/instructions" part of the request.

## Validation Risks

- bUnit proves descriptor-driven rendering, but browser proof is still needed to catch tab wrapping, list readability, and narrow-width spacing.
- A plugin with no executors must be tested so the empty state is intentional instead of a blank tab.
- Package plugin runtime loading is outside this feature; descriptor-loaded package executors should be covered by the same model because `PluginCatalogItem.Descriptor` stores the active descriptor.

## Reopen Triggers

- Reopen `SB01` if tests or browser proof show hard-coded plugin names, hard-coded executor ids, or a failure to render executors from `selectedPlugin.Descriptor.WorkflowExecutors`.
- Reopen `SB01` if the new tab hides executor descriptions or makes them unreadable at narrow width.
- Reopen `SB01` if the change requires service-layer contract changes not represented in this bundle.
