# PromptFactorySessionGraphAdapter Specification

## Purpose

Project Prompt Factory editor/session state into shared graph nodes, links, groups, and selection metadata.

## User scenarios

- Build the session graph, selection graph, branch nodes, and run-node projections.
- Attach node kinds, labels, chips, and contextual actions.
- Persist and rehydrate canvas UI state coherently with the domain model.

## Functional scope

### Minimum viable version

Extract BuildCanvasNodes/BuildCanvasLinks and selection-graph assembly into a dedicated adapter class.

### Target advanced version

Full projection pipeline with lane strategies, validation annotations, and scene diffing.

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

- src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866
- src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.Catalog.cs#L1-L1090
- src/CanDoItAll.Modules.Factory/FactoryDomain.cs#L359-L536
- src/CanDoItAll.Modules.Factory/PromptFactoryService.cs#L238-L715
- src/CanDoItAll.ComponentKit/Canvas/CanvasWorkbenchContracts.cs#L5-L340
- src/CanDoItAll.Modules.Factory/CanvasAdapters/PromptFactorySessionGraphAdapter.cs
- tests/CanDoItAll.Tests.Components/PromptFactorySessionGraphAdapterTests.cs

## Dependencies

- CanvasWorkbenchShell
- NodeCardComposer
- PromptFactoryCatalogToolbox
- PromptRunBranchLane
- PromptSessionAttachmentNode

## Performance requirements

Avoid rebuilding the full graph when only one prompt node or branch changed.

## UX/UI notes

The adapter should preserve the current mental model while making future graph growth manageable.

## Accessibility notes

Provide stable labels and structural summaries for complex session graphs.

## Recommended implementation in Blazor + C#

Implement the full adapter in C#. Domain projection, action resolution, placement policies, and state serialization should remain typed and testable.

## Recommended JS layer

No dedicated JS bridge should be required. If browser-specific behavior appears later, add it through the shared JsInteropBridge instead of ad hoc scripts.

## Animation guidance

Do not animate by default. Add only small transition hooks where they improve continuity, such as focus or mount transitions.

## Render strategy

Renderer-agnostic surface in C# with JS implementation details hidden behind the host/runtime boundary.

## Test recommendations

Add graph projection tests for session states, selected node types, and branch configurations.

## Validation recommendations

- Validate all typed input contracts and reject invalid IDs or impossible bounds early.
- Add at least one component test and one targeted logic test for the primary behavior.
- Confirm no duplicate shared abstraction is created next to the existing framework entry points.
- Verify read-only/disabled behavior and error fallback handling.
- Check performance under large or rapidly changing scenes when the component participates in hot paths.

## Future extensions

- Full projection pipeline with lane strategies, validation annotations, and scene diffing.
- Keep the domain-specific layer thin enough that future product modules can mirror the same pattern.

## API proposal

```csharp
public sealed class PromptFactorySessionGraphAdapter
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
- Domain-specific rules remain in the adapter and do not leak into shared framework components.

## Key repository references

- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866`
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.Catalog.cs#L1-L1090`
- `src/CanDoItAll.Modules.Factory/FactoryDomain.cs#L359-L536`
- `src/CanDoItAll.Modules.Factory/PromptFactoryService.cs#L238-L715`
- `src/CanDoItAll.ComponentKit/Canvas/CanvasWorkbenchContracts.cs#L5-L340`
