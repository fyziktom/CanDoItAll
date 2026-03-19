# UI Shared Components

This folder documents the shared Blazor component layer currently living in:

- `C:\repositories\zyphonote\src\App.Components\Radzen`
- `C:\repositories\zyphonote\src\App.Components\Radzen\Blazor`

The current library is best understood as a Radzen-shaped compatibility layer, not the real Radzen package. It provides a small, intentionally narrow subset of components with familiar names and enums, backed by custom Razor components, custom helper enums, and a generated CSS file.

## Key conclusions

- The API shape is Radzen-like, but the implementation is fully custom.
- Most components are thin wrappers over native HTML plus utility-class styling.
- The library is useful for consistent layout and simple CRUD screens.
- Several names imply richer behavior than what is actually implemented.
- Codex should always check this documentation before assuming a full Radzen feature exists.

## Documentation map

- [Architecture: stack and architecture](architecture/stack-and-architecture.md)
- [Reference: helpers, enums, and models](reference/helpers-enums-and-models.md)
- [Components: layout and typography](components/layout-and-typography.md)
- [Components: forms and inputs](components/forms-and-inputs.md)
- [Components: navigation and workflow](components/navigation-and-workflow.md)
- [Components: data and feedback](components/data-and-feedback.md)
- [Guidelines: Codex usage guide](guidelines/codex-usage-guide.md)
- [Recommendations: missing components](recommendations/missing-components.md)

## Fast bootstrap checklist

1. Reference `Zyphonote.App.Components`.
2. Import `@using Radzen` and `@using Radzen.Blazor`.
3. Register services with `services.AddRadzenComponents()`.
4. Load `_content/Zyphonote.App.Components/css/output.css`.
5. Do not assume advanced Radzen features unless they are documented here.

## Fast rules for Codex

- Prefer these shared components for common layout, inputs, tabs, steps, grids, and simple notifications.
- Do not use `Dialog`, `Tooltip`, or `ContextMenu` as functional overlays. They are placeholders only.
- Do not assume `DataGrid` supports sorting, filtering, virtualization, grouping, or empty templates.
- Do not assume `Chart` supports full axis configuration. It currently renders only simple line-series values.
- Treat `Variant`, `Shade`, and some enum members as partially implemented unless called out otherwise.
