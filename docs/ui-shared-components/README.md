# UI Shared Components

CanDoItAll shared UI is no longer a single component project. The current component architecture is split by responsibility:

| Project | Role |
| --- | --- |
| `src/CanDoItAll.Components.Common` | Shared primitives with no Blazor rendering dependency. |
| `src/CanDoItAll.Components.BaseLib` | Main reusable Razor component library, theme tokens, layout primitives, forms, buttons, cards, lists, feedback, and generated CSS. |
| `src/CanDoItAll.Components.CanvasLib` | Canvas and graph/workbench components built on BaseLib and Common. |
| `src/CanDoItAll.Components.OverlayLib` | Floating overlay/window components used by workbench surfaces. |
| `src/CanDoItAll.Components.WebGlLib` | WebGL workbench concept runtime and typed scene contracts. |
| `src/CanDoItAll.Components` | Compatibility/facade package for app shell, tab strip, tuning boundary, and references to core component libraries. |
| `src/CanDoItAll.Components.Sandbox` | Preview and regression host for shared components. |
| `src/CanDoItAll.Components.WebGlSandbox` | WebGL process workbench sandbox. |

The web host registers BaseLib through `AddCanDoItAllBaseLib()` and loads module/component assemblies through `CanDoItAll.Composition.ModuleAssemblies`.

## Key Conclusions

- BaseLib is the primary shared UI library for product modules.
- CanvasLib, OverlayLib, and WebGlLib are specialized libraries, not general-purpose replacements for BaseLib.
- `CanDoItAll.Components` is a facade/compatibility layer and app-shell surface. Do not put every new shared component there by default.
- Tailwind output is owned by the shared workspace and emitted to `src/CanDoItAll.Components.BaseLib/wwwroot/css/output.css`.
- Modules should prefer existing shared components before introducing raw markup-heavy local patterns.

## Documentation Map

- [Architecture: stack and architecture](architecture/stack-and-architecture.md)
- [Reference: helpers, enums, and models](reference/helpers-enums-and-models.md)
- [Components: layout and typography](components/layout-and-typography.md)
- [Components: forms and inputs](components/forms-and-inputs.md)
- [Components: navigation and workflow](components/navigation-and-workflow.md)
- [Components: data and feedback](components/data-and-feedback.md)
- [Guidelines: Codex usage guide](guidelines/codex-usage-guide.md)
- [Recommendations: missing components](recommendations/missing-components.md)
- [Transfer checklist](component-transfer-checklist.md)

## Fast Bootstrap Checklist

1. Reference the smallest component library that owns the component you need.
2. Import the relevant namespace, usually `CanDoItAll.Components.BaseLib` or `CanDoItAll.Components.CanvasLib`.
3. Register BaseLib services with `services.AddCanDoItAllBaseLib()`.
4. Load `_content/CanDoItAll.Components.BaseLib/css/output.css`.
5. Use sandbox projects for previews, demos, and regression proof.

## Fast Rules For Codex

- Use BaseLib for ordinary layout, buttons, forms, cards, lists, feedback, tabs, and page scaffolding.
- Use CanvasLib only for canvas/graph/workbench behaviors.
- Use OverlayLib for floating windows or overlay primitives instead of local ad hoc overlays.
- Do not assume a component supports richer behavior than its implementation shows. Check the component file before promising sorting, filtering, validation, virtualization, or JS interop behavior.
- Keep app-specific styling in the app or module. Shared libraries should not depend on consumer-global CSS.
