# Stack And Architecture

## Source And Packaging

| Concern | Current implementation |
| --- | --- |
| Common primitives | `src/CanDoItAll.Components.Common` |
| Primary shared UI | `src/CanDoItAll.Components.BaseLib` |
| Canvas and graph UI | `src/CanDoItAll.Components.CanvasLib` |
| Overlay windows | `src/CanDoItAll.Components.OverlayLib` |
| WebGL workbench runtime | `src/CanDoItAll.Components.WebGlLib` |
| Facade and app shell | `src/CanDoItAll.Components` |
| Component sandbox | `src/CanDoItAll.Components.Sandbox` |
| WebGL sandbox | `src/CanDoItAll.Components.WebGlSandbox` |
| Target framework | `net10.0` |
| Primary package dependency | `Microsoft.AspNetCore.Components.Web` |
| Generated stylesheet | `src/CanDoItAll.Components.BaseLib/wwwroot/css/output.css` |

## Actual Library Roles

### Common

`CanDoItAll.Components.Common` contains shared non-rendering primitives. Keep it dependency-light. It should not grow UI rendering or app-specific behavior.

### BaseLib

`CanDoItAll.Components.BaseLib` is the main product UI component library. It owns:

- design tokens and `CadThemes`
- `StyledComponentBase`
- BaseLib service registration through `AddCanDoItAllBaseLib()`
- `NotificationService`
- badges, buttons, cards, forms, feedback, identity, layout, lists, navigation, tabs, typography, and simple data display components
- the generated Tailwind CSS output

Most product modules should start here before adding local component markup.

### CanvasLib

`CanDoItAll.Components.CanvasLib` owns canvas, graph, layout, and interaction components used by process/workbench-style surfaces. It depends on BaseLib and Common.

### OverlayLib

`CanDoItAll.Components.OverlayLib` owns floating overlay/window components and runtime helpers. It depends on BaseLib. Use it for workbench overlays instead of local one-off floating surfaces.

### WebGlLib

`CanDoItAll.Components.WebGlLib` owns the WebGL workbench concept runtime with typed scene contracts and browser proof hooks. It depends on OverlayLib and is specialized.

### Facade And Sandboxes

`CanDoItAll.Components` is a facade/app-shell layer. Its project file intentionally removes broad historical `Components/**` content and includes only app shell, tab strip, and tuning boundary assets plus references to component libraries.

`CanDoItAll.Components.Sandbox` and `CanDoItAll.Components.WebGlSandbox` are preview and validation hosts. Do not move catalog/demo-only assets into runtime libraries.

## Consumption Pattern

Current web integration:

- `Program.cs` calls `builder.Services.AddCanDoItAllBaseLib()`.
- Runtime component assemblies are supplied by `CanDoItAll.Composition.ModuleAssemblies.All`.
- Shared CSS is loaded from `_content/CanDoItAll.Components.BaseLib/css/output.css`.

Module integration:

- Reference BaseLib for routine product UI.
- Reference CanvasLib only when the module needs canvas or graph behavior.
- Reference OverlayLib/WebGlLib only for their specialized surfaces.
- Keep module-specific business logic in the module, not in shared UI libraries.

## Styling Model

BaseLib uses generated utility CSS and component-level class composition. The Tailwind workspace at the repo root emits CSS to BaseLib:

```powershell
npm run tailwind:build
```

Shared components should expose typed parameters or focused child content rather than asking consumers to reassemble behavior with raw `div`/`span` structures.

## Parent-Child Component Patterns

Several components use parent-child composition and registration. When editing or moving these components, keep children inside the expected parent:

- grid and column patterns
- tab and tab-item patterns
- steps and steps-item patterns
- canvas node/link/control patterns

If a child component renders no visible HTML by itself, moving it outside the parent can break behavior without a compile error.

## Architecture Constraints

- BaseLib is not a full enterprise component suite.
- Do not claim sorting, filtering, grouping, virtualization, validation, dialog, chart, or JS behavior exists without checking the component implementation.
- Add shared abstractions only when more than one module needs them or when the library boundary is the right owner.
- Keep preview/demo/fake data in sandbox projects.
- Keep app-specific or consumer-specific styling out of shared libraries.

## Practical Guidance

Use shared libraries when you need stable, repeated UI patterns. Use a module-local component when behavior is specific to one module. Promote module-local UI into a shared library only when the second real consumer arrives or when the component belongs to a cross-module surface such as canvas, overlays, or app shell.
