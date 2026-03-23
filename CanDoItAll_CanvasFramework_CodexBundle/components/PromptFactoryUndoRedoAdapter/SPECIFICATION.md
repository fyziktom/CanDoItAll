# PromptFactoryUndoRedoAdapter Specification

## Purpose

Integrate Prompt Factory editing operations with the shared CommandHistoryStore and shortcut system.

## User scenarios

- Track prompt editor state snapshots after meaningful edits.
- Enable toolbar buttons and shortcuts from shared command-history state.
- Prepare for future clipboard and branch-reorder operations to participate in undo/redo.

## Functional scope

### Minimum viable version

Wrap current snapshot history logic in a shared adapter used by PromptFactoryPage.

### Target advanced version

Action-labeled transactional history with merge rules and selective undo for canvas operations.

## Visual behavior

The component should render using the shared canvas/workbench visual language, with consistent spacing, typography, contrast, and selected/hover/disabled states.

## Interaction behavior

The adapter itself is not a direct interaction surface. It reacts to page or runtime events by producing new shared surface state and persistence payloads.

## Component states

- Idle
- Projecting
- Ready
- InvalidInput
- MigratingLegacyState

## Edge cases

- Null or incomplete input model arriving during a mid-refresh state.
- Rapid successive updates caused by selection changes, drag loops, or remote persistence echoes.
- Read-only or disabled mode where visuals still need to render but actions must not execute.
- Domain object exists but referenced linked entity is missing.
- Legacy persisted state that lacks new fields.

## Error behavior

Fail closed with diagnostics: return a safe default projection or host state, log structured diagnostics, and avoid leaving pages in a half-mounted state.

## Responsibilities

- Translate domain state into shared framework state.
- Hide domain quirks from the shared UI components.
- Centralize domain-specific policies and keep pages lean.

## Responsibility boundaries

- Does not own low-level rendering loops.
- Does not bypass shared component contracts.
- Does not replace domain services; it orchestrates their outputs.

## Inputs

- Domain model or service result.
- Persisted UI/view state.
- Current selection or viewport context.
- Feature flags and permission context if applicable.

## Outputs

- Shared surface DTOs or component props.
- Resolved action catalogs.
- Typed persistence payloads.
- Diagnostics markers when the domain projection is incomplete.

## Parameters / props / configuration

- DomainModel
- PersistedState
- SelectionState
- PermissionContext
- FeatureFlags

## Data model

Typed domain input -> normalized shared surface DTO -> optional persisted view state envelope -> typed callback/persistence payload.

## Integration points

- src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.History.cs#L1-L234
- src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866
- src/CanDoItAll.Modules.Factory/FactoryDomain.cs#L359-L536
- src/CanDoItAll.Modules.Factory/PromptFactoryService.cs#L238-L715
- src/CanDoItAll.Modules.Factory/CanvasAdapters/PromptFactoryUndoRedoAdapter.cs
- src/CanDoItAll.Modules.Factory/wwwroot/js/prompt-factory-undo-redo-adapter.js
- tests/CanDoItAll.Tests.Components/PromptFactoryUndoRedoAdapterTests.cs

## Dependencies

- CommandHistoryStore
- KeyboardShortcutRouter
- SerializationPersistencePack

## Performance requirements

Avoid redundant snapshots when nothing materially changed.

## UX/UI notes

Undo/redo should restore both domain state and canvas selection/viewport in a way that feels trustworthy.

## Accessibility notes

Surface undo/redo availability through toolbar semantics and shortcut help.

## Recommended implementation in Blazor + C#

Implement the full adapter in C#. Domain projection, action resolution, placement policies, and state serialization should remain typed and testable.

## Recommended JS layer

Use a thin JS helper only for geometry, measurement, DOM positioning, or runtime integration. Keep the public contract coarse-grained and typed.

## Animation guidance

Do not animate by default. Add only small transition hooks where they improve continuity, such as focus or mount transitions.

## Render strategy

Renderer-agnostic surface in C# with JS implementation details hidden behind the host/runtime boundary.

## Test recommendations

Cover add/edit/delete/branch operations and canvas-state restoration after undo/redo.

## Validation recommendations

- Validate all typed input contracts and reject invalid IDs or impossible bounds early.
- Add at least one component test and one targeted logic test for the primary behavior.
- Confirm no duplicate shared abstraction is created next to the existing framework entry points.
- Verify read-only/disabled behavior and error fallback handling.
- Check performance under large or rapidly changing scenes when the component participates in hot paths.

## Future extensions

- Action-labeled transactional history with merge rules and selective undo for canvas operations.
- Keep the domain-specific layer thin enough that future product modules can mirror the same pattern.
- Add diagnostics and performance counters before increasing runtime complexity.

## API proposal

```csharp
public sealed class PromptFactoryUndoRedoAdapter
{
    public object BuildSurface(object domainModel, object? persistedState);
    public IReadOnlyList<string> ResolveSelection(object domainModel, object selectionState);
    public object BuildActionCatalog(object domainModel, string? sourceId);
    public Task PersistStateAsync(object domainModel, object state);
}
```

## Internal structure

- Domain projection
- State mapping
- Action catalog mapping
- Persistence policy

## Responsiveness

Primary target is desktop, but the component should still avoid brittle assumptions about host size, zoom level, or maximized state.

## Theming / styling / skinning

Consume semantic theme tokens rather than hardcoded colors or spacing. Stay dark/light-ready even if the current product ships only one theme at first.

## ASCII sketch

```text
Domain model --> adapter --> shared surface/component --> runtime
```

## State diagram

```text
Idle -> Projecting -> Ready -> Updating -> Ready
               \-> InvalidInput -> Recovery/Defaulting
```

## Event list

- Projected
- ProjectionInvalidated
- SelectionMapped
- ActionRequested
- UndoRequested
- RedoRequested

## Callback list

- BuildSurface
- MapSelection
- ResolveActions
- PersistStateAsync

## Validation scenarios

- Happy-path render or interaction flow works from start to finish.
- Read-only and disabled states suppress actions without breaking layout.
- Error fallback preserves a stable UI and exposes diagnostics.
- Component integrates cleanly with its declared dependencies and does not bypass shared ownership boundaries.
- Old ownership point is genuinely reduced or removed after extraction.
- Interop contract is stable, coarse-grained, and does not spam cross-boundary calls during hot interactions.
- Domain-specific rules remain in the adapter and do not leak into shared framework components.

## Key repository references

- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.History.cs#L1-L234`
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866`
- `src/CanDoItAll.Modules.Factory/FactoryDomain.cs#L359-L536`
- `src/CanDoItAll.Modules.Factory/PromptFactoryService.cs#L238-L715`
