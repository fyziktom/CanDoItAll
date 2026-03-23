# NodeCardComposer Specification

## Purpose

Compose graph node cards from primitives: container, text, chips, icons, image slots, metadata stacks, and inline affordances.

## User scenarios

- Project Structure object cards with type, title, status, priority, and marker metadata.
- Prompt Factory session, component, input, branch, and run-step cards.
- Future reusable note, media, placeholder, or validation cards.

## Functional scope

### Minimum viable version

Extract node-card composition templates from renderNodes and page-specific mapping rules.

### Target advanced version

Declarative card templates, per-type slots, compact/expanded states, and inline toolbar anchors.

## Visual behavior

The component should render using the shared canvas/workbench visual language, with consistent spacing, typography, contrast, and selected/hover/disabled states.

## Interaction behavior

If interactive at all, it should emit small semantic events such as click, hover, or focus without owning domain actions.

## Component states

- Idle
- Measuring
- Rendered
- Updating
- Disabled
- ErrorFallback

## Edge cases

- Null or incomplete input model arriving during a mid-refresh state.
- Rapid successive updates caused by selection changes, drag loops, or remote persistence echoes.
- Read-only or disabled mode where visuals still need to render but actions must not execute.

## Error behavior

Render a safe fallback visual state, preserve the last good layout when possible, and surface diagnostics/test hooks for investigation.

## Responsibilities

- Provide a reusable visual or structural building block.
- Keep styling and state behavior consistent across canvases.
- Avoid domain-specific assumptions in shared components.

## Responsibility boundaries

- Does not own domain-specific business actions.
- Does not own page-level layout beyond its local rendering responsibility.
- Does not bypass shared state/selection services when interactive.

## Inputs

- Typed props/model.
- Current scene/viewport context.
- Selection or focus state when relevant.
- Theme and interaction settings.

## Outputs

- Rendered visual or interaction state.
- Typed callbacks/events.
- Optional diagnostics metadata.

## Parameters / props / configuration

- Id
- IsReadOnly
- IsDisabled
- ThemeKey
- Bounds
- Tone
- Variant
- SemanticKind

## Data model

Props model + optional measured layout state + style/state projection model.

## Integration points

- src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099
- src/CanDoItAll.ComponentKit/wwwroot/canvas-workbench.css#L1-L2309
- src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor#L1-L1399
- src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866
- src/CanDoItAll.ComponentKit/Canvas/CanvasWorkbenchContracts.cs#L5-L340
- src/CanDoItAll.ComponentKit/Canvas/Graph/NodeCardComposer.cs
- src/CanDoItAll.ComponentKit/Components/NodeCardComposer.razor
- src/CanDoItAll.ComponentKit/wwwroot/js/node-card-composer.js
- tests/CanDoItAll.Tests.Components/NodeCardComposerTests.cs

## Dependencies

- ContainerPrimitive
- TextBlockPrimitive
- ChipBadgePrimitive
- ImagePrimitive
- IconGlyphPrimitive

## Performance requirements

Cache card template selection and only rerender affected cards when data changes.

## UX/UI notes

Cards must remain information-dense but legible; do not let type-specific customization fracture the shared language.

## Accessibility notes

Expose clear semantic labels, selected state, and per-card actions to keyboard and screen readers.

## Recommended implementation in Blazor + C#

Define typed props/state in C# and keep business semantics there. JS should only own the pieces that are directly tied to browser APIs or hot-path rendering.

## Recommended JS layer

Implement a dedicated JS module with a narrow public API. Own pointer-heavy math, rendering loops, hit testing, and host integration there; keep business and persistence decisions in C#.

## Animation guidance

Allow subtle opacity or transform transitions only where they improve perceived smoothness without adding lag.

## Render strategy

Use a hybrid strategy: DOM for rich card/editing content, SVG or canvas for geometric overlays, and CSS transforms for viewport movement.

## Test recommendations

Visual regression should cover the main card variants used by both canvases.

## Validation recommendations

- Validate all typed input contracts and reject invalid IDs or impossible bounds early.
- Add at least one component test and one targeted logic test for the primary behavior.
- Confirm no duplicate shared abstraction is created next to the existing framework entry points.
- Verify read-only/disabled behavior and error fallback handling.
- Check performance under large or rapidly changing scenes when the component participates in hot paths.

## Future extensions

- Declarative card templates, per-type slots, compact/expanded states, and inline toolbar anchors.
- Allow plugins or domain adapters to extend this component without forking the shared framework.
- Add diagnostics and performance counters before increasing runtime complexity.

## API proposal

```csharp
public sealed class NodeCardComposerProps
{
    public string Id { get; init; } = string.Empty;
    public bool IsSelected { get; init; }
    public bool IsDisabled { get; init; }
}
```

## Internal structure

- Props model
- Measurement hook
- Render template
- State/style mapper

## Responsiveness

Primary target is desktop, but the component should still avoid brittle assumptions about host size, zoom level, or maximized state.

## Theming / styling / skinning

Consume semantic theme tokens rather than hardcoded colors or spacing. Stay dark/light-ready even if the current product ships only one theme at first.

## ASCII sketch

```text
+------------------------------+
| icon  title            chip  |
| subtitle / helper text       |
| metadata / preview / action  |
+------------------------------+
```

## State diagram

```text
Idle -> Measuring -> Rendered -> Updating -> Rendered
               \-> ErrorFallback -> Rendered/Hidden
```

## Event list

- Measured
- Rendered
- Clicked
- Hovered
- Focused

## Callback list

- OnMeasured
- OnClicked
- OnHovered
- OnFocused

## Validation scenarios

- Happy-path render or interaction flow works from start to finish.
- Read-only and disabled states suppress actions without breaking layout.
- Error fallback preserves a stable UI and exposes diagnostics.
- Component integrates cleanly with its declared dependencies and does not bypass shared ownership boundaries.
- Old ownership point is genuinely reduced or removed after extraction.
- Interop contract is stable, coarse-grained, and does not spam cross-boundary calls during hot interactions.

## Key repository references

- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099`
- `src/CanDoItAll.ComponentKit/wwwroot/canvas-workbench.css#L1-L2309`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor#L1-L1399`
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866`
- `src/CanDoItAll.ComponentKit/Canvas/CanvasWorkbenchContracts.cs#L5-L340`
