# Icon Asset Plan

## Requirements

- Docker, Gmail, and Office365 need plugin icons in the plugins page, workflow context menu, and workflow executor nodes.
- Icons must be local assets at runtime.
- Icon lookup should use a typed descriptor, not raw string routing.
- The implementation must respect brand/trademark guidance.

## Recommended Contract

Use or introduce a model equivalent to:

- `PluginIconKind`: `MaterialSymbol`, `BundledAsset`, `PackageAsset`
- `PluginIconDescriptor`: kind, material icon name, asset path, alt text, brand name, version/cache key
- `WorkflowExecutorDescriptor` or source metadata: plugin icon descriptor for node/menu rendering
- `PluginDescriptor` or plugin catalog item: icon descriptor for plugin page and grouping

Existing `IconName` can remain as compatibility data during migration, but new rendering should prefer the typed icon descriptor.

## Candidate Assets

| Plugin | Preferred Asset | Fallback Material Icon | Source Review |
|---|---|---|---|
| Docker | Local Docker SVG from approved media/trademark source or reviewed Simple Icons asset | `terminal` or `deployed_code` | Docker media resources and trademark guidelines |
| Gmail | Local Gmail SVG from Google Brand Resource Center or reviewed Simple Icons asset | `mail` | Google Brand Resource Center |
| Office365 | Local Microsoft 365/Office asset if approved, or neutral app/cloud icon | `apps` or `cloud` | Microsoft icon guidance and product brand review |

## Source Links For Implementation Review

- `https://www.docker.com/company/newsroom/media-resources/`
- `https://www.docker.com/legal/trademark-guidelines/`
- `https://about.google/brand-resource-center/logos-list/`
- `https://about.google/brand-resource-center/brand-elements/`
- `https://learn.microsoft.com/en-us/office/dev/add-ins/design/microsoft-365-extension-management-icons`
- `https://github.com/simple-icons/simple-icons`

## Acceptance Notes

- Do not hotlink external icon URLs.
- Do not embed large unreviewed bitmap files.
- Package icon paths must be validated against traversal.
- Missing icon behavior must be explicit and visually stable.
- Browser proof must inspect plugin page, workflow menu, and executor node rendering.
