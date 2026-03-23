# DragDropController Specification

## Purpose

Centralize pointer capture, drag start heuristics, multi-node move, group-frame move, drop targets, and drag lifecycle events.

## User scenarios

- Drag one or many nodes in the workbench.
- Drag group frames or future connector handles.
- Support future external drag/drop into the canvas, branch reorder, and template insertion.

## Functional scope

### Minimum viable version

Extract startDragForNodeIds and related pointer lifecycle into a reusable drag controller service.

### Target advanced version

Drop zones, drag previews, transactional drag commands, external drop data, and touch-safe drag heuristics.

## Visual behavior

Visual behavior is mostly transient: selection outlines, hover cues, drag previews, guides, handles, or active-target affordances should appear only when intent is clear and disappear immediately after completion.

## Interaction behavior

This component owns interaction semantics within its boundary and must coordinate cleanly with SelectionModel, HoverFocusRouter, KeyboardShortcutRouter, and the surrounding shell.

## Component states

- Idle
- Armed
- Active
- Completed
- Cancelled
- Disabled

## Edge cases

- Null or incomplete input model arriving during a mid-refresh state.
- Rapid successive updates caused by selection changes, drag loops, or remote persistence echoes.
- Read-only or disabled mode where visuals still need to render but actions must not execute.

## Error behavior

Render a safe fallback visual state, preserve the last good layout when possible, and surface diagnostics/test hooks for investigation.

## Responsibilities

- Own the interaction state machine.
- Publish typed state changes.
- Coordinate with neighboring selection/focus/viewport systems.

## Responsibility boundaries

- Does not persist domain state directly.
- Does not define visual style tokens by itself.
- Does not own page composition outside the interaction concern.

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
- IsEnabled
- Tolerance
- ModifierPolicy

## Data model

State machine model + active target reference + geometry data + typed change event payload.

## Integration points

- src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099
- src/CanDoItAll.ComponentKit/Components/CanvasWorkbench.razor#L1-L572
- src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor#L1-L1399
- src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866
- src/CanDoItAll.ComponentKit/Canvas/Graph/DragDropController.cs
- src/CanDoItAll.ComponentKit/wwwroot/js/drag-drop-controller.js
- tests/CanDoItAll.Tests.Components/DragDropControllerTests.cs

## Dependencies

- HitTestService
- SelectionModel
- SnapGuideSystem
- InvalidationScheduler

## Performance requirements

Keep drag math on the JS side and publish consolidated movement payloads back to C#.

## UX/UI notes

Drag should begin only when intent is clear and must feel smooth under high-frequency pointer updates.

## Accessibility notes

Provide keyboard alternatives for moving selected items.

## Recommended implementation in Blazor + C#

Define typed props/state in C# and keep business semantics there. JS should only own the pieces that are directly tied to browser APIs or hot-path rendering.

## Recommended JS layer

Implement a dedicated JS module with a narrow public API. Own pointer-heavy math, rendering loops, hit testing, and host integration there; keep business and persistence decisions in C#.

## Animation guidance

Allow subtle opacity or transform transitions only where they improve perceived smoothness without adding lag.

## Render strategy

Prefer lightweight SVG or canvas layers for geometry-heavy visuals; keep rich editable content in DOM where necessary.

## Test recommendations

Add drag threshold, multi-select drag, and cancellation tests.

## Validation recommendations

- Validate all typed input contracts and reject invalid IDs or impossible bounds early.
- Add at least one component test and one targeted logic test for the primary behavior.
- Confirm no duplicate shared abstraction is created next to the existing framework entry points.
- Verify read-only/disabled behavior and error fallback handling.
- Check performance under large or rapidly changing scenes when the component participates in hot paths.

## Future extensions

- Drop zones, drag previews, transactional drag commands, external drop data, and touch-safe drag heuristics.
- Allow plugins or domain adapters to extend this component without forking the shared framework.
- Add diagnostics and performance counters before increasing runtime complexity.

## API proposal

```csharp
public sealed class DragDropControllerOptions
{
    public bool IsEnabled { get; init; } = true;
    public double Tolerance { get; init; } = 8;
}

public sealed record DragDropControllerChangedEventArgs(string[] TargetIds, string Reason);
```

## Internal structure

- Input event gateway
- State machine
- Geometry helper
- Callback publisher

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
Idle -> Armed -> Active -> Completed -> Idle
             \-> Cancelled -----------^
```

## Event list

- Started
- Changed
- Completed
- Cancelled

## Callback list

- OnStarted
- OnChanged
- OnCompleted
- OnCancelled

## Validation scenarios

- Happy-path render or interaction flow works from start to finish.
- Read-only and disabled states suppress actions without breaking layout.
- Error fallback preserves a stable UI and exposes diagnostics.
- Component integrates cleanly with its declared dependencies and does not bypass shared ownership boundaries.
- Old ownership point is genuinely reduced or removed after extraction.
- Interop contract is stable, coarse-grained, and does not spam cross-boundary calls during hot interactions.

## Key repository references

- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099`
- `src/CanDoItAll.ComponentKit/Components/CanvasWorkbench.razor#L1-L572`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor#L1-L1399`
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866`
