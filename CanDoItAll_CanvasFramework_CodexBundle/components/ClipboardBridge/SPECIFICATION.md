# ClipboardBridge Specification

## Purpose

Enable copy, cut, paste, duplicate, and clipboard-serialization scenarios for selected canvas entities.

## User scenarios

- Duplicate a selected prompt subgraph with preserved relative positions.
- Copy Project Structure nodes or groups into another location or project.
- Support future cross-canvas clipboard formats and import/export-lite flows.

## Functional scope

### Minimum viable version

Internal duplicate and paste-in-place support for shared graph selections.

### Target advanced version

System clipboard integration, cross-surface paste transforms, and MIME-versioned payload envelopes.

## Visual behavior

The component should appear above the canvas content with consistent elevation, radius, spacing, and theme tokens. It must avoid occluding critical content whenever an anchored placement can solve that.

## Interaction behavior

This component owns interaction semantics within its boundary and must coordinate cleanly with SelectionModel, HoverFocusRouter, KeyboardShortcutRouter, and the surrounding shell.

## Component states

- Closed
- Opening
- Editing
- Validating
- Saving
- ValidationError
- Cancelled

## Edge cases

- Null or incomplete input model arriving during a mid-refresh state.
- Rapid successive updates caused by selection changes, drag loops, or remote persistence echoes.
- Read-only or disabled mode where visuals still need to render but actions must not execute.
- Pasting stale payload version.
- Cross-surface paste with unsupported node kinds.

## Error behavior

Show a localized inline error state when user-facing actions fail, keep the previous stable state visible, and preserve focus for retry or cancel.

## Responsibilities

- Present a focused high-level interaction surface.
- Own visibility/focus/commit lifecycle within its boundary.
- Coordinate cleanly with the shell and selection state.

## Responsibility boundaries

- Does not become a catch-all for unrelated page actions.
- Does not duplicate shared shell responsibilities.
- Does not bypass selection/focus rules.

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
- IsOpen
- AnchorRect
- PlacementMode
- ZIndex

## Data model

Visibility state + anchor/placement state + local draft/action state + callback payloads.

## Integration points

- src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.History.cs#L1-L234
- src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor#L1-L1399
- src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866
- src/CanDoItAll.ComponentKit/Canvas/CanvasWorkbenchContracts.cs#L5-L340
- src/CanDoItAll.ComponentKit/Canvas/Graph/ClipboardBridge.cs
- src/CanDoItAll.ComponentKit/Components/ClipboardBridge.razor
- src/CanDoItAll.ComponentKit/wwwroot/js/clipboard-bridge.js
- tests/CanDoItAll.Tests.Components/ClipboardBridgeTests.cs

## Dependencies

- SelectionModel
- SerializationPersistencePack
- KeyboardShortcutRouter
- CommandHistoryStore

## Performance requirements

Serialize only relevant subgraphs and avoid blocking the UI thread with large payloads.

## UX/UI notes

Pasted content should appear predictably relative to viewport and preserve semantic selection.

## Accessibility notes

Expose clipboard actions in menus and inspectors, not only via keyboard.

## Recommended implementation in Blazor + C#

Use Blazor components for structure and state orchestration. Only push to JS the overlay positioning, pointer-heavy geometry, or runtime-owned rendering details.

## Recommended JS layer

Implement a dedicated JS module with a narrow public API. Own pointer-heavy math, rendering loops, hit testing, and host integration there; keep business and persistence decisions in C#.

## Animation guidance

Allow subtle opacity or transform transitions only where they improve perceived smoothness without adding lag.

## Render strategy

Use a hybrid strategy: DOM for rich card/editing content, SVG or canvas for geometric overlays, and CSS transforms for viewport movement.

## Test recommendations

Cover copy/paste with links, group frames, and invalid target scenarios.

## Validation recommendations

- Validate all typed input contracts and reject invalid IDs or impossible bounds early.
- Add at least one component test and one targeted logic test for the primary behavior.
- Confirm no duplicate shared abstraction is created next to the existing framework entry points.
- Verify read-only/disabled behavior and error fallback handling.
- Check performance under large or rapidly changing scenes when the component participates in hot paths.

## Future extensions

- System clipboard integration, cross-surface paste transforms, and MIME-versioned payload envelopes.
- Allow plugins or domain adapters to extend this component without forking the shared framework.
- Add diagnostics and performance counters before increasing runtime complexity.

## API proposal

```csharp
public sealed class ClipboardBridgeProps
{
    public string Id { get; init; } = string.Empty;
    public bool IsOpen { get; init; }
    public bool IsReadOnly { get; init; }
}
```

## Internal structure

- Draft state model
- Validation adapter
- Commit/cancel pipeline
- Focus return policy

## Responsiveness

Primary target is desktop, but the component should still avoid brittle assumptions about host size, zoom level, or maximized state.

## Theming / styling / skinning

Consume semantic theme tokens rather than hardcoded colors or spacing. Stay dark/light-ready even if the current product ships only one theme at first.

## ASCII sketch

```text
+--------------------------+
| Title                    |
| [ editable field      ]  |
| [ save ] [ cancel ]      |
+--------------------------+
```

## State diagram

```text
Closed -> Opening -> Editing -> Validating -> Saving -> Closed
                       \-> Cancelled ---------------> Closed
                       \-> ValidationError -> Editing
```

## Event list

- Opened
- ValueChanged
- Committed
- Cancelled
- ValidationFailed
- CopyRequested
- PasteRequested

## Callback list

- OnCommitAsync
- OnCancelAsync
- OnValidationRequestedAsync

## Validation scenarios

- Happy-path render or interaction flow works from start to finish.
- Read-only and disabled states suppress actions without breaking layout.
- Error fallback preserves a stable UI and exposes diagnostics.
- Component integrates cleanly with its declared dependencies and does not bypass shared ownership boundaries.
- Interop contract is stable, coarse-grained, and does not spam cross-boundary calls during hot interactions.

## Key repository references

- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.History.cs#L1-L234`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor#L1-L1399`
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866`
- `src/CanDoItAll.ComponentKit/Canvas/CanvasWorkbenchContracts.cs#L5-L340`
