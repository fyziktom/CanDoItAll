# SelectionModel Specification

## Purpose

Own single-select, multi-select, primary selection, range semantics, and selection-state publication.

## User scenarios

- Track primary node and selected node IDs in Project Structure and Prompt Factory.
- Drive inspector content, toolbar enablement, and selection frame rendering.
- Prepare for selection-based copy/paste and transform handles.

## Functional scope

### Minimum viable version

Selection store with primary ID, ordered selection IDs, additive/toggle semantics, and publish hooks.

### Target advanced version

Selection sets by type, grouped selection rules, temporary selection previews, and command-history integration.

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
- Selected item deleted remotely.
- Mixed-type multi-selection with partial permissions.

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

- src/CanDoItAll.ComponentKit/Canvas/CanvasWorkbenchContracts.cs#L5-L340
- src/CanDoItAll.ComponentKit/Components/CanvasWorkbench.razor#L1-L572
- src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099
- src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor#L1-L1399
- src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866
- src/CanDoItAll.ComponentKit/Canvas/Graph/SelectionModel.cs
- src/CanDoItAll.ComponentKit/wwwroot/js/selection-model.js
- tests/CanDoItAll.Tests.Components/SelectionModelTests.cs

## Dependencies

- HitTestService
- MarqueeSelectionOverlay
- HoverFocusRouter

## Performance requirements

Publish deltas rather than rebuilding all node view models on every change.

## UX/UI notes

Selection changes must be visually clear and predictable under modifier keys.

## Accessibility notes

Keyboard navigation and screen-reader summaries should announce primary selection changes.

## Recommended implementation in Blazor + C#

Define typed props/state in C# and keep business semantics there. JS should only own the pieces that are directly tied to browser APIs or hot-path rendering.

## Recommended JS layer

Use a thin JS helper only for geometry, measurement, DOM positioning, or runtime integration. Keep the public contract coarse-grained and typed.

## Animation guidance

Use very short fade/scale transitions and respect reduced-motion preferences. State clarity is more important than decoration.

## Render strategy

Prefer lightweight SVG or canvas layers for geometry-heavy visuals; keep rich editable content in DOM where necessary.

## Test recommendations

Cover additive, toggle, replace, and clear scenarios including group-frame membership updates.

## Validation recommendations

- Validate all typed input contracts and reject invalid IDs or impossible bounds early.
- Add at least one component test and one targeted logic test for the primary behavior.
- Confirm no duplicate shared abstraction is created next to the existing framework entry points.
- Verify read-only/disabled behavior and error fallback handling.
- Check performance under large or rapidly changing scenes when the component participates in hot paths.

## Future extensions

- Selection sets by type, grouped selection rules, temporary selection previews, and command-history integration.
- Allow plugins or domain adapters to extend this component without forking the shared framework.
- Add diagnostics and performance counters before increasing runtime complexity.

## API proposal

```csharp
public sealed class SelectionModelOptions
{
    public bool IsEnabled { get; init; } = true;
    public double Tolerance { get; init; } = 8;
}

public sealed record SelectionModelChangedEventArgs(string[] TargetIds, string Reason);
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
- PrimarySelectionChanged

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

- `src/CanDoItAll.ComponentKit/Canvas/CanvasWorkbenchContracts.cs#L5-L340`
- `src/CanDoItAll.ComponentKit/Components/CanvasWorkbench.razor#L1-L572`
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor#L1-L1399`
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866`
