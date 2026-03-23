# CanvasCalendarHost Specification

## Purpose

Primary reusable Blazor component that hosts the calendar runtime with typed event/save/delete/export callbacks.

## User scenarios

- Project calendar integration.
- Potential future calendar views in other modules.
- Export-ready typed wrapper around the specialized JS calendar engine.

## Functional scope

### Minimum viable version

Keep the current wrapper and harden its contracts, tests, and domain adapter usage.

### Target advanced version

Slot-based chrome, shared stage-shell integration, and optional diagnostics/test hooks.

## Visual behavior

Visual behavior follows the current calendar widget language but must be wrapped in stable boundaries so view transitions, selection panels, and edit modals remain visually coherent.

## Interaction behavior

Support pointer and keyboard interaction according to current calendar behavior, while exposing typed state/save/delete/export callbacks to C#.

## Component states

- Viewing
- Selecting
- Editing
- Saving
- Exporting
- Error

## Edge cases

- Null or incomplete input model arriving during a mid-refresh state.
- Rapid successive updates caused by selection changes, drag loops, or remote persistence echoes.
- Read-only or disabled mode where visuals still need to render but actions must not execute.
- Timezone or DST boundary.
- Overlapping events.
- Persisted view state from older schema.

## Error behavior

Preserve the previous visible calendar state, surface operation errors through the wrapper and panel, and avoid silently dropping edits.

## Responsibilities

- Wrap or modularize calendar-specific behavior behind a stable boundary.
- Keep typed operations and state sync reliable.
- Preserve current UX while enabling safer evolution.

## Responsibility boundaries

- Does not move calendar business workflows into page-local JS hacks.
- Does not force calendar internals into the graph scene model.
- Does not own unrelated graph interactions.

## Inputs

- CanvasCalendarSurface or calendar runtime options.
- Current view state.
- Operation callbacks.
- Theme and read-only flags.

## Outputs

- Selection/state change callbacks.
- Save/delete/export requests.
- Visible event summaries or diagnostics hooks.

## Parameters / props / configuration

- Surface
- ViewState
- TimeZone
- OnSaveAsync
- OnDeleteAsync
- OnExportAsync

## Data model

Calendar surface + event models + view-state model + operation request records.

## Integration points

- src/CanDoItAll.ComponentKit/Components/CanvasCalendar.razor#L1-L258
- src/CanDoItAll.ComponentKit/Canvas/CanvasCalendarContracts.cs#L3-L223
- src/CanDoItAll.ComponentKit/wwwroot/js/canvasCalendarInterop.js#L13-L335
- src/CanDoItAll.ComponentKit/wwwroot/js/zy-canvas-calendar.js#L1-L3720
- docs/canvas-events-calendar/rebuild/blazor-jsinterop-component-plan.md#L1-L186
- src/CanDoItAll.ComponentKit/Components/CanvasCalendar.razor
- src/CanDoItAll.ComponentKit/Canvas/Calendar/CanvasCalendarHost.cs
- src/CanDoItAll.ComponentKit/wwwroot/js/canvasCalendarInterop.js
- tests/CanDoItAll.Tests.Components/CanvasCalendarTests.cs

## Dependencies

- CanvasSceneHost
- CalendarCrudBridge
- CalendarSelectionPanel

## Performance requirements

Prefer full-widget updates only when needed; route granular option changes through typed update APIs.

## UX/UI notes

Preserve full-widget parity while making the wrapper easier to evolve safely.

## Accessibility notes

Surface clear labels for selection, save, delete, and export actions.

## Recommended implementation in Blazor + C#

Keep wrapper contracts, operation callbacks, and state parsing in C#. Let JS continue owning dense calendar rendering and low-level hit math.

## Recommended JS layer

Implement a dedicated JS module with a narrow public API. Own pointer-heavy math, rendering loops, hit testing, and host integration there; keep business and persistence decisions in C#.

## Animation guidance

Allow subtle opacity or transform transitions only where they improve perceived smoothness without adding lag.

## Render strategy

Keep the existing calendar engine as the rendering owner, but modularize by concern inside the runtime and expose typed update points through the wrapper.

## Test recommendations

Add wrapper lifecycle tests and typed callback payload tests.

## Validation recommendations

- Validate all typed input contracts and reject invalid IDs or impossible bounds early.
- Add at least one component test and one targeted logic test for the primary behavior.
- Confirm no duplicate shared abstraction is created next to the existing framework entry points.
- Verify read-only/disabled behavior and error fallback handling.
- Check performance under large or rapidly changing scenes when the component participates in hot paths.

## Future extensions

- Slot-based chrome, shared stage-shell integration, and optional diagnostics/test hooks.
- Allow plugins or domain adapters to extend this component without forking the shared framework.
- Add diagnostics and performance counters before increasing runtime complexity.

## API proposal

```csharp
public sealed class CanvasCalendarHostOptions
{
    public string SurfaceId { get; init; } = string.Empty;
    public string TimeZone { get; init; } = "UTC";
    public bool IsReadOnly { get; init; }
}

public sealed record CanvasCalendarHostState(string View, DateOnly VisibleDate, string? SelectedId);
```

## Internal structure

- Typed wrapper boundary
- Runtime submodule
- State sync adapter
- Operation callback bridge

## Responsiveness

The component should define how it collapses, docks, or repositions on narrower stage widths. Avoid assuming a permanent ultra-wide desktop canvas.

## Theming / styling / skinning

Consume semantic theme tokens rather than hardcoded colors or spacing. Stay dark/light-ready even if the current product ships only one theme at first.

## ASCII sketch

```text
+------------------------------------------------------+
| Toolbar / Scope / Export                             |
+----------------------+-------------------------------+
| Mini month / filters | Main calendar surface         |
| Selection summary    | timed grid / month / year     |
| Context panel        | event blocks / hit targets    |
+----------------------+-------------------------------+
```

## State diagram

```text
Viewing -> Selecting -> Editing -> Saving -> Viewing
      \-> ChangingScope --------^
      \-> Exporting ------------^
```

## Event list

- SelectionChanged
- StateChanged
- SaveRequested
- DeleteRequested
- ExportRequested

## Callback list

- OnSelectionChanged
- OnStateChanged
- OnSaveAsync
- OnDeleteAsync
- OnExportAsync

## Validation scenarios

- Happy-path render or interaction flow works from start to finish.
- Read-only and disabled states suppress actions without breaking layout.
- Error fallback preserves a stable UI and exposes diagnostics.
- Component integrates cleanly with its declared dependencies and does not bypass shared ownership boundaries.
- Old ownership point is genuinely reduced or removed after extraction.
- Interop contract is stable, coarse-grained, and does not spam cross-boundary calls during hot interactions.

## Key repository references

- `src/CanDoItAll.ComponentKit/Components/CanvasCalendar.razor#L1-L258`
- `src/CanDoItAll.ComponentKit/Canvas/CanvasCalendarContracts.cs#L3-L223`
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasCalendarInterop.js#L13-L335`
- `src/CanDoItAll.ComponentKit/wwwroot/js/zy-canvas-calendar.js#L1-L3720`
- `docs/canvas-events-calendar/rebuild/blazor-jsinterop-component-plan.md#L1-L186`
