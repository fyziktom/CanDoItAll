# AccessibilityMirrorLayer Specification

## Purpose

Maintain a hidden but semantic DOM representation of interactive canvas content for screen readers and keyboard navigation fallbacks.

## User scenarios

- Expose selected node summaries and actionable items to assistive tech.
- Mirror calendar event selection and navigation outside the visual canvas.
- Provide keyboard-only navigation through scene entities when direct canvas semantics are insufficient.

## Functional scope

### Minimum viable version

Mirror current selection and actionable node list in a hidden DOM region with ARIA labels.

### Target advanced version

Full semantic tree, focus synchronization, region announcements, and alternative list/grid representations.

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

- src/CanDoItAll.ComponentKit/Components/CanvasWorkbench.razor#L1-L572
- src/CanDoItAll.ComponentKit/Components/CanvasCalendar.razor#L1-L258
- src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099
- src/CanDoItAll.ComponentKit/wwwroot/js/canvasCalendarInterop.js#L13-L335
- src/CanDoItAll.ComponentKit/Canvas/Core/AccessibilityMirrorLayer.cs
- src/CanDoItAll.ComponentKit/Components/AccessibilityMirrorLayer.razor
- src/CanDoItAll.ComponentKit/wwwroot/js/accessibility-mirror-layer.js
- tests/CanDoItAll.Tests.Components/AccessibilityMirrorLayerTests.cs

## Dependencies

- CanvasSceneHost
- SelectionModel
- HoverFocusRouter
- SerializationPersistencePack

## Performance requirements

Publish only semantic deltas and avoid rebuilding the full mirror on every hover change.

## UX/UI notes

The fallback layer should support assistive use without imposing visible complexity on the primary canvas UI.

## Accessibility notes

This component exists for accessibility; design it from semantic-first principles.

## Recommended implementation in Blazor + C#

Keep orchestration, DTO normalization, persistence seams, and feature flags in C#. Use JS only for direct rendering, measurement, and browser-native event work.

## Recommended JS layer

Implement a dedicated JS module with a narrow public API. Own pointer-heavy math, rendering loops, hit testing, and host integration there; keep business and persistence decisions in C#.

## Animation guidance

Do not animate by default. Add only small transition hooks where they improve continuity, such as focus or mount transitions.

## Render strategy

Renderer-agnostic surface in C# with JS implementation details hidden behind the host/runtime boundary.

## Test recommendations

Add accessibility tree tests and keyboard navigation scenarios.

## Validation recommendations

- Validate all typed input contracts and reject invalid IDs or impossible bounds early.
- Add at least one component test and one targeted logic test for the primary behavior.
- Confirm no duplicate shared abstraction is created next to the existing framework entry points.
- Verify read-only/disabled behavior and error fallback handling.
- Check performance under large or rapidly changing scenes when the component participates in hot paths.

## Future extensions

- Full semantic tree, focus synchronization, region announcements, and alternative list/grid representations.
- Allow plugins or domain adapters to extend this component without forking the shared framework.
- Add diagnostics and performance counters before increasing runtime complexity.

## API proposal

```csharp
public sealed class AccessibilityMirrorLayerOptions
{
    public string Id { get; init; } = string.Empty;
    public bool IsReadOnly { get; init; }
    public bool EnableDiagnostics { get; init; }
}

public interface IAccessibilityMirrorLayer
{
    ValueTask AttachAsync(ElementReference host, AccessibilityMirrorLayerOptions options);
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
- Interop contract is stable, coarse-grained, and does not spam cross-boundary calls during hot interactions.

## Key repository references

- `src/CanDoItAll.ComponentKit/Components/CanvasWorkbench.razor#L1-L572`
- `src/CanDoItAll.ComponentKit/Components/CanvasCalendar.razor#L1-L258`
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099`
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasCalendarInterop.js#L13-L335`
