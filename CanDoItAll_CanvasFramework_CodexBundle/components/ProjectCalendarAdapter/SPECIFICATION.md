# ProjectCalendarAdapter Specification

## Purpose

Map project-specific calendar domain models and view-state persistence to the shared CanvasCalendar contract.

## User scenarios

- Map ProjectCalendarSurface and ProjectCalendarEvent to CanvasCalendarSurface and CanvasCalendarEvent.
- Persist view state through ProjectWorkbenchService using typed calendar state objects.
- Hide legacy wrapper details from the page.

## Functional scope

### Minimum viable version

Adapter class/service used by ProjectCalendarPage to populate CanvasCalendar and consume typed callbacks.

### Target advanced version

Separate mapper + state policy + selection policy with migration helpers from legacy JSON state.

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
- Timezone or DST boundary.
- Overlapping events.
- Persisted view state from older schema.
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

- src/CanDoItAll.Modules.Workbench/Pages/ProjectCalendarPage.razor#L1-L161
- src/CanDoItAll.Modules.Workbench/Components/ProjectEventsCalendar.razor#L1-L79
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs#L128-L806
- src/CanDoItAll.ComponentKit/Components/CanvasCalendar.razor#L1-L258
- src/CanDoItAll.ComponentKit/Canvas/CanvasCalendarContracts.cs#L3-L223
- src/CanDoItAll.Modules.Workbench/Calendar/ProjectCalendarAdapter.cs
- tests/CanDoItAll.Tests.Components/ProjectCalendarAdapterTests.cs

## Dependencies

- CanvasCalendarHost
- SerializationPersistencePack
- CalendarCrudBridge

## Performance requirements

Map domain models once per load/update and avoid repeated JSON conversions in the page.

## UX/UI notes

Migration should be visually neutral while removing brittle view-state parsing.

## Accessibility notes

Preserve focus and selection semantics during migration from legacy wrapper to shared wrapper.

## Recommended implementation in Blazor + C#

Implement the full adapter in C#. Domain projection, action resolution, placement policies, and state serialization should remain typed and testable.

## Recommended JS layer

No dedicated JS bridge should be required. If browser-specific behavior appears later, add it through the shared JsInteropBridge instead of ad hoc scripts.

## Animation guidance

Do not animate by default. Add only small transition hooks where they improve continuity, such as focus or mount transitions.

## Render strategy

Keep the existing calendar engine as the rendering owner, but modularize by concern inside the runtime and expose typed update points through the wrapper.

## Test recommendations

Add page-level tests verifying legacy wrapper removal and typed state persistence.

## Validation recommendations

- Validate all typed input contracts and reject invalid IDs or impossible bounds early.
- Add at least one component test and one targeted logic test for the primary behavior.
- Confirm no duplicate shared abstraction is created next to the existing framework entry points.
- Verify read-only/disabled behavior and error fallback handling.
- Check performance under large or rapidly changing scenes when the component participates in hot paths.

## Future extensions

- Separate mapper + state policy + selection policy with migration helpers from legacy JSON state.
- Keep the domain-specific layer thin enough that future product modules can mirror the same pattern.

## API proposal

```csharp
public sealed class ProjectCalendarAdapter
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

The component should define how it collapses, docks, or repositions on narrower stage widths. Avoid assuming a permanent ultra-wide desktop canvas.

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
- Domain-specific rules remain in the adapter and do not leak into shared framework components.

## Key repository references

- `src/CanDoItAll.Modules.Workbench/Pages/ProjectCalendarPage.razor#L1-L161`
- `src/CanDoItAll.Modules.Workbench/Components/ProjectEventsCalendar.razor#L1-L79`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs#L128-L806`
- `src/CanDoItAll.ComponentKit/Components/CanvasCalendar.razor#L1-L258`
- `src/CanDoItAll.ComponentKit/Canvas/CanvasCalendarContracts.cs#L3-L223`
