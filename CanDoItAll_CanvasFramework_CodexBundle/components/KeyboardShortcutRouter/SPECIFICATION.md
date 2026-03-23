# KeyboardShortcutRouter Specification

## Purpose

Standardize canvas keyboard shortcuts, scope management, and conflict handling across graph and calendar surfaces.

## User scenarios

- Undo/redo, fit view, zoom in/out, focus primary node, and open help overlay.
- Context-sensitive shortcuts that change between graph editing and inline editing modes.
- Future clipboard, selection transform, and command palette shortcuts.

## Functional scope

### Minimum viable version

Shortcut registry with scope-aware enablement and host focus ownership checks.

### Target advanced version

Customizable bindings, shortcut help panel generation, and collision diagnostics.

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

- src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.History.cs#L1-L234
- src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099
- src/CanDoItAll.ComponentKit/Components/CanvasWorkbench.razor#L1-L572
- src/CanDoItAll.ComponentKit/wwwroot/js/zy-canvas-calendar.js#L1-L3720
- src/CanDoItAll.ComponentKit/Canvas/Graph/KeyboardShortcutRouter.cs
- src/CanDoItAll.ComponentKit/Components/KeyboardShortcutRouter.razor
- src/CanDoItAll.ComponentKit/wwwroot/js/keyboard-shortcut-router.js
- tests/CanDoItAll.Tests.Components/KeyboardShortcutRouterTests.cs

## Dependencies

- SelectionModel
- HoverFocusRouter
- CommandHistoryStore

## Performance requirements

Use delegated listeners at host scope and keep handlers side-effect-light.

## UX/UI notes

Shortcuts must never break text input fields or modal editing flows.

## Accessibility notes

All critical shortcut actions need visible UI alternatives and discoverable help.

## Recommended implementation in Blazor + C#

Define typed props/state in C# and keep business semantics there. JS should only own the pieces that are directly tied to browser APIs or hot-path rendering.

## Recommended JS layer

Implement a dedicated JS module with a narrow public API. Own pointer-heavy math, rendering loops, hit testing, and host integration there; keep business and persistence decisions in C#.

## Animation guidance

Allow subtle opacity or transform transitions only where they improve perceived smoothness without adding lag.

## Render strategy

Prefer lightweight SVG or canvas layers for geometry-heavy visuals; keep rich editable content in DOM where necessary.

## Test recommendations

Cover focus scoping, text-input suppression, and multi-surface collision handling.

## Validation recommendations

- Validate all typed input contracts and reject invalid IDs or impossible bounds early.
- Add at least one component test and one targeted logic test for the primary behavior.
- Confirm no duplicate shared abstraction is created next to the existing framework entry points.
- Verify read-only/disabled behavior and error fallback handling.
- Check performance under large or rapidly changing scenes when the component participates in hot paths.

## Future extensions

- Customizable bindings, shortcut help panel generation, and collision diagnostics.
- Allow plugins or domain adapters to extend this component without forking the shared framework.
- Add diagnostics and performance counters before increasing runtime complexity.

## API proposal

```csharp
public sealed class KeyboardShortcutRouterOptions
{
    public bool IsEnabled { get; init; } = true;
    public double Tolerance { get; init; } = 8;
}

public sealed record KeyboardShortcutRouterChangedEventArgs(string[] TargetIds, string Reason);
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

- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.History.cs#L1-L234`
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099`
- `src/CanDoItAll.ComponentKit/Components/CanvasWorkbench.razor#L1-L572`
- `src/CanDoItAll.ComponentKit/wwwroot/js/zy-canvas-calendar.js#L1-L3720`
