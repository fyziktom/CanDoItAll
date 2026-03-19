# UI Shared Components

This folder documents the shared Blazor component library currently stored in:

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components\Components`

The current library is a compatibility layer with familiar component names and enums, backed by custom Razor components, helper enums, and a generated CSS file.

## Key conclusions

- The API surface is custom and intentionally narrow.
- Most components are thin wrappers over native HTML plus utility-class styling.
- The library is useful for consistent layout and simple CRUD screens.
- Several names imply richer behavior than what is actually implemented.
- Codex should check this documentation before assuming a feature exists.

## Documentation map

- [Architecture: stack and architecture](architecture/stack-and-architecture.md)
- [Reference: helpers, enums, and models](reference/helpers-enums-and-models.md)
- [Components: layout and typography](components/layout-and-typography.md)
- [Components: forms and inputs](components/forms-and-inputs.md)
- [Components: navigation and workflow](components/navigation-and-workflow.md)
- [Components: data and feedback](components/data-and-feedback.md)
- [Guidelines: Codex usage guide](guidelines/codex-usage-guide.md)
- [Recommendations: missing components](recommendations/missing-components.md)
- [Transfer checklist](component-transfer-checklist.md)

## Fast bootstrap checklist

1. Reference `CanDoItAll.Components`.
2. Import `@using CanDoItAll.Components`.
3. Register services with `services.AddCanDoItAllComponents()`.
4. Load `_content/CanDoItAll.Components/css/output.css`.
5. Do not assume advanced features unless they are documented here.

## Fast rules for Codex

- Prefer these shared components for common layout, inputs, tabs, steps, grids, and simple notifications.
- Do not use `Dialog`, `Tooltip`, or `ContextMenu` as functional overlays. They are placeholders only.
- Do not assume `DataGrid` supports sorting, filtering, virtualization, grouping, or empty templates.
- Do not assume `Chart` supports full axis configuration. It currently renders only simple line-series values.
- Treat `Variant`, `Shade`, and some enum members as partially implemented unless called out otherwise.
