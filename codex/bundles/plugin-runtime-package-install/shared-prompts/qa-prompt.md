# QA Prompt

Validate the plugin runtime package work against the raw request, not only the implementation diff.

Check:

- Docker, Gmail, and Office365 still appear in the plugin catalog after moving implementation projects.
- Package zips are validated through strongly typed manifests.
- Missing manifest, invalid manifest, and unsafe zip paths fail predictably.
- Catalogue install and upload install share the same backend validation path.
- Installed package descriptors appear in the catalog without recompiling.
- Packages with assemblies mark restart required.
- Restart action calls host lifetime and gives the user a clear state.
- `/plugins` package controls are visible, readable, and use the existing component system.
- Browser proof includes large desktop viewport plus narrower width if layout changes.
