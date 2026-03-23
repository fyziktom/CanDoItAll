# SnapGuideSystem Specification

## Purpose

Calculate snapping candidates, alignment guides, and near-edge/center snapping behavior during move and resize operations.

## User scenarios

- Snap project nodes to grid or sibling edges.
- Align prompt nodes and branch lanes visually during drag.
- Show guide lines and distance hints for precise editing.

## Functional scope

### Minimum viable version

Grid snap plus simple edge/center alignment guides for selected node drag.

### Target advanced version

Multi-object smart guides, connector-aware snapping, distance labels, and user-configurable tolerances.

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
- src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor#L1-L1399
- src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866
- docs/canvases-improvements/01-reference-and-gap-analysis.md#L1-L203
- src/CanDoItAll.ComponentKit/Canvas/Graph/SnapGuideSystem.cs
- src/CanDoItAll.ComponentKit/wwwroot/js/snap-guide-system.js
- tests/CanDoItAll.Tests.Components/SnapGuideSystemTests.cs

## Dependencies

- GridBackdrop
- SelectionModel
- DragDropController
- ViewportController

## Performance requirements

Use indexed candidate sets and limit guide calculations to visible nearby objects.

## UX/UI notes

Guides should appear only when useful and disappear cleanly after the action ends.

## Accessibility notes

Expose snapping state through numeric position indicators for keyboard move flows.

## Recommended implementation in Blazor + C#

Define typed props/state in C# and keep business semantics there. JS should only own the pieces that are directly tied to browser APIs or hot-path rendering.

## Recommended JS layer

Implement a dedicated JS module with a narrow public API. Own pointer-heavy math, rendering loops, hit testing, and host integration there; keep business and persistence decisions in C#.

## Animation guidance

Allow subtle opacity or transform transitions only where they improve perceived smoothness without adding lag.

## Render strategy

Prefer lightweight SVG or canvas layers for geometry-heavy visuals; keep rich editable content in DOM where necessary.

## Test recommendations

Cover guide candidate ranking, tolerance edges, and disabled-snap scenarios.

## Validation recommendations

- Validate all typed input contracts and reject invalid IDs or impossible bounds early.
- Add at least one component test and one targeted logic test for the primary behavior.
- Confirm no duplicate shared abstraction is created next to the existing framework entry points.
- Verify read-only/disabled behavior and error fallback handling.
- Check performance under large or rapidly changing scenes when the component participates in hot paths.

## Future extensions

- Multi-object smart guides, connector-aware snapping, distance labels, and user-configurable tolerances.
- Allow plugins or domain adapters to extend this component without forking the shared framework.
- Add diagnostics and performance counters before increasing runtime complexity.

## API proposal

```csharp
public sealed class SnapGuideSystemOptions
{
    public bool IsEnabled { get; init; } = true;
    public double Tolerance { get; init; } = 8;
}

public sealed record SnapGuideSystemChangedEventArgs(string[] TargetIds, string Reason);
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
- Interop contract is stable, coarse-grained, and does not spam cross-boundary calls during hot interactions.

## Key repository references

- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor#L1-L1399`
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866`
- `docs/canvases-improvements/01-reference-and-gap-analysis.md#L1-L203`
