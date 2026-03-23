# SerializationPersistencePack Specification

## Purpose

Normalize serializable scene state, viewport state, selection state, and UI-state persistence contracts across graph and calendar surfaces.

## User scenarios

- Persist manual positions, collapse state, selection, and viewport for workbench-like surfaces.
- Persist calendar view state in a typed model instead of string parsing.
- Support export/import-ready scene manifests and future collaboration scenarios.

## Functional scope

### Minimum viable version

Typed state envelopes for graph UI state and calendar view state with version fields.

### Target advanced version

Schema versioning, migration helpers, import/export envelopes, and per-component serialization adapters.

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
- src/CanDoItAll.ComponentKit/Canvas/CanvasCalendarContracts.cs#L3-L223
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs#L128-L806
- src/CanDoItAll.Modules.Factory/FactoryDomain.cs#L359-L536
- src/CanDoItAll.Modules.Factory/PromptFactoryService.cs#L238-L715
- src/CanDoItAll.Modules.Workbench/Pages/ProjectCalendarPage.razor#L1-L161
- src/CanDoItAll.ComponentKit/Canvas/Core/SerializationPersistencePack.cs
- src/CanDoItAll.ComponentKit/wwwroot/js/serialization-persistence-pack.js
- tests/CanDoItAll.Tests.Components/SerializationPersistencePackTests.cs

## Dependencies

- SceneNodeModel
- CommandHistoryStore

## Performance requirements

Persist deltas when possible and debounce frequent view changes.

## UX/UI notes

Persistence must make refresh and return-to-page behavior feel trustworthy and unsurprising.

## Accessibility notes

Restore focus target and selection semantics when rehydrating state.

## Recommended implementation in Blazor + C#

Keep orchestration, DTO normalization, persistence seams, and feature flags in C#. Use JS only for direct rendering, measurement, and browser-native event work.

## Recommended JS layer

Use a thin JS helper only for geometry, measurement, DOM positioning, or runtime integration. Keep the public contract coarse-grained and typed.

## Animation guidance

Do not animate by default. Add only small transition hooks where they improve continuity, such as focus or mount transitions.

## Render strategy

Renderer-agnostic surface in C# with JS implementation details hidden behind the host/runtime boundary.

## Test recommendations

Add serialization round-trip tests and backwards-compatible migration tests.

## Validation recommendations

- Validate all typed input contracts and reject invalid IDs or impossible bounds early.
- Add at least one component test and one targeted logic test for the primary behavior.
- Confirm no duplicate shared abstraction is created next to the existing framework entry points.
- Verify read-only/disabled behavior and error fallback handling.
- Check performance under large or rapidly changing scenes when the component participates in hot paths.

## Future extensions

- Schema versioning, migration helpers, import/export envelopes, and per-component serialization adapters.
- Allow plugins or domain adapters to extend this component without forking the shared framework.
- Add diagnostics and performance counters before increasing runtime complexity.

## API proposal

```csharp
public sealed class SerializationPersistencePackOptions
{
    public string Id { get; init; } = string.Empty;
    public bool IsReadOnly { get; init; }
    public bool EnableDiagnostics { get; init; }
}

public interface ISerializationPersistencePack
{
    ValueTask AttachAsync(ElementReference host, SerializationPersistencePackOptions options);
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
- Old ownership point is genuinely reduced or removed after extraction.
- Interop contract is stable, coarse-grained, and does not spam cross-boundary calls during hot interactions.

## Key repository references

- `src/CanDoItAll.ComponentKit/Canvas/CanvasWorkbenchContracts.cs#L5-L340`
- `src/CanDoItAll.ComponentKit/Canvas/CanvasCalendarContracts.cs#L3-L223`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs#L128-L806`
- `src/CanDoItAll.Modules.Factory/FactoryDomain.cs#L359-L536`
- `src/CanDoItAll.Modules.Factory/PromptFactoryService.cs#L238-L715`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectCalendarPage.razor#L1-L161`
