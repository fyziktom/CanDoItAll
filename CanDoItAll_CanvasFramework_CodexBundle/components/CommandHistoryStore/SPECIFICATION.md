# CommandHistoryStore Specification

## Purpose

Provide a shared undo/redo-ready history abstraction for editor state snapshots or domain commands.

## User scenarios

- Undo selection-safe graph edits in Prompt Factory.
- Undo node create/move/link actions in Project Structure after shared command integration.
- Feed toolbar button enabled state and keyboard shortcut routing from one consistent source.

## Functional scope

### Minimum viable version

Snapshot-based history store with max-entry policy, semantic labels, and change deduplication.

### Target advanced version

Hybrid snapshot/command history with merge rules, transactional groups, and collaboration-safe checkpoints.

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

- src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.History.cs#L1-L234
- src/CanDoItAll.Modules.Factory/FactoryDomain.cs#L359-L536
- src/CanDoItAll.Modules.Factory/PromptFactoryService.cs#L238-L715
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs#L128-L806
- src/CanDoItAll.ComponentKit/Canvas/Core/CommandHistoryStore.cs
- tests/CanDoItAll.Tests.Components/CommandHistoryStoreTests.cs

## Dependencies

- SerializationPersistencePack

## Performance requirements

Store normalized lightweight state snapshots or reversible commands rather than full heavy object graphs when possible.

## UX/UI notes

Undo should preserve selection and viewport where possible and must never feel lossy.

## Accessibility notes

Expose undo/redo state to keyboard and semantic toolbar controls.

## Recommended implementation in Blazor + C#

Keep orchestration, DTO normalization, persistence seams, and feature flags in C#. Use JS only for direct rendering, measurement, and browser-native event work.

## Recommended JS layer

No dedicated JS bridge should be required. If browser-specific behavior appears later, add it through the shared JsInteropBridge instead of ad hoc scripts.

## Animation guidance

Do not animate by default. Add only small transition hooks where they improve continuity, such as focus or mount transitions.

## Render strategy

Renderer-agnostic surface in C# with JS implementation details hidden behind the host/runtime boundary.

## Test recommendations

Cover no-op dedupe, transaction grouping, and restore integrity after deep edits.

## Validation recommendations

- Validate all typed input contracts and reject invalid IDs or impossible bounds early.
- Add at least one component test and one targeted logic test for the primary behavior.
- Confirm no duplicate shared abstraction is created next to the existing framework entry points.
- Verify read-only/disabled behavior and error fallback handling.
- Check performance under large or rapidly changing scenes when the component participates in hot paths.

## Future extensions

- Hybrid snapshot/command history with merge rules, transactional groups, and collaboration-safe checkpoints.
- Allow plugins or domain adapters to extend this component without forking the shared framework.

## API proposal

```csharp
public sealed class CommandHistoryStoreOptions
{
    public string Id { get; init; } = string.Empty;
    public bool IsReadOnly { get; init; }
    public bool EnableDiagnostics { get; init; }
}

public interface ICommandHistoryStore
{
    ValueTask AttachAsync(ElementReference host, CommandHistoryStoreOptions options);
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
- UndoRequested
- RedoRequested

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

## Key repository references

- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.History.cs#L1-L234`
- `src/CanDoItAll.Modules.Factory/FactoryDomain.cs#L359-L536`
- `src/CanDoItAll.Modules.Factory/PromptFactoryService.cs#L238-L715`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs#L128-L806`
