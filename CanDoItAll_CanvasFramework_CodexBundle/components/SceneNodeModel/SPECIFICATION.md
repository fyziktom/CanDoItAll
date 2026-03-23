# SceneNodeModel Specification

## Purpose

Define the internal scene graph contract used by shared graph components: nodes, children, bounds, transforms, visibility, hit regions, and state flags.

## User scenarios

- Represent container nodes, connectors, overlays, and decorations in one normalized graph.
- Support grouping, clipping, z-ordering, and dirty-region invalidation.
- Allow domain adapters to project domain data into a stable shared rendering model.

## Functional scope

### Minimum viable version

Immutable-ish node descriptors with IDs, parent IDs, layout bounds, transforms, visibility flags, and semantic type identifiers.

### Target advanced version

Typed scene node hierarchy with diffing, invalidation flags, caching hints, accessibility metadata, serialization adapters, and plugin-extensible capabilities.

## Visual behavior

This component is mostly infrastructural. Its visible behavior is limited to stable mount/unmount, optional debug framing, and ensuring overlays/host layers appear without flicker.

## Interaction behavior

Primary interactions are lifecycle-driven rather than user-driven. User input should reach downstream renderers through typed, documented callback routes.

## Component states

- Uninitialized
- Mounting
- Ready
- Updating
- Error
- Disposed

## Edge cases

- Null or incomplete input model arriving during a mid-refresh state.
- Rapid successive updates caused by selection changes, drag loops, or remote persistence echoes.
- Read-only or disabled mode where visuals still need to render but actions must not execute.

## Error behavior

Fail closed with diagnostics: return a safe default projection or host state, log structured diagnostics, and avoid leaving pages in a half-mounted state.

## Responsibilities

- Own the minimal shared boundary for this concern.
- Expose a typed, reusable contract.
- Keep page/domain code from reimplementing the same responsibility.

## Responsibility boundaries

- Does not own domain persistence semantics.
- Does not decide domain-specific create/action rules.
- Does not render product-specific inspector content.

## Inputs

- Host element reference.
- Typed surface or runtime payload.
- Feature flags and diagnostics settings.
- Theme tokens and read-only mode.

## Outputs

- Mounted runtime state.
- Lifecycle callbacks to C#.
- Resize or invalidation notifications.
- Diagnostics/test handles.

## Parameters / props / configuration

- Id
- IsReadOnly
- IsDisabled
- ThemeKey
- EnableDiagnostics
- SurfaceKind
- TestHookId

## Data model

Options model + runtime state + diagnostics/test-hook envelope.

## Integration points

- src/CanDoItAll.ComponentKit/Canvas/CanvasWorkbenchContracts.cs#L5-L340
- src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099
- src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866
- src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor#L1-L1399
- src/CanDoItAll.ComponentKit/Canvas/Core/SceneNodeModel.cs
- tests/CanDoItAll.Tests.Components/SceneNodeModelTests.cs

## Dependencies

- CanvasSceneHost
- LayerStack
- InvalidationScheduler

## Performance requirements

The model should support structural diffing so refreshes do not rebuild the entire scene on every state mutation.

## UX/UI notes

A stable scene model is the prerequisite for consistent layering, selection, and predictable keyboard behavior.

## Accessibility notes

Each node type should carry semantic labels and a DOM mirror strategy where appropriate.

## Recommended implementation in Blazor + C#

Keep orchestration, DTO normalization, persistence seams, and feature flags in C#. Use JS only for direct rendering, measurement, and browser-native event work.

## Recommended JS layer

No dedicated JS bridge should be required. If browser-specific behavior appears later, add it through the shared JsInteropBridge instead of ad hoc scripts.

## Animation guidance

Do not animate by default. Add only small transition hooks where they improve continuity, such as focus or mount transitions.

## Render strategy

Renderer-agnostic surface in C# with JS implementation details hidden behind the host/runtime boundary.

## Test recommendations

Add pure C# tests for node normalization, grouping rules, and serialization compatibility.

## Validation recommendations

- Validate all typed input contracts and reject invalid IDs or impossible bounds early.
- Add at least one component test and one targeted logic test for the primary behavior.
- Confirm no duplicate shared abstraction is created next to the existing framework entry points.
- Verify read-only/disabled behavior and error fallback handling.
- Check performance under large or rapidly changing scenes when the component participates in hot paths.

## Future extensions

- Typed scene node hierarchy with diffing, invalidation flags, caching hints, accessibility metadata, serialization adapters, and plugin-extensible capabilities.
- Allow plugins or domain adapters to extend this component without forking the shared framework.

## API proposal

```csharp
public sealed class SceneNodeModelOptions
{
    public string Id { get; init; } = string.Empty;
    public bool IsReadOnly { get; init; }
    public bool EnableDiagnostics { get; init; }
}

public interface ISceneNodeModel
{
    ValueTask AttachAsync(ElementReference host, SceneNodeModelOptions options);
    ValueTask UpdateAsync(object payload);
    ValueTask DisposeAsync();
}
```

## Internal structure

- Public C# contract
- State holder
- Interop adapter (if needed)
- Diagnostics/test hooks

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
Uninitialized -> Mounting -> Ready -> Updating -> Ready
                           \-> Error
Ready -> Disposing -> Disposed
```

## Event list

- Mounted
- Updated
- Resized
- Disposed
- DiagnosticsToggled

## Callback list

- OnMounted
- OnSurfaceChanged
- OnViewportChanged
- OnDisposed

## Validation scenarios

- Happy-path render or interaction flow works from start to finish.
- Read-only and disabled states suppress actions without breaking layout.
- Error fallback preserves a stable UI and exposes diagnostics.
- Component integrates cleanly with its declared dependencies and does not bypass shared ownership boundaries.

## Key repository references

- `src/CanDoItAll.ComponentKit/Canvas/CanvasWorkbenchContracts.cs#L5-L340`
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099`
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor#L1-L1399`
