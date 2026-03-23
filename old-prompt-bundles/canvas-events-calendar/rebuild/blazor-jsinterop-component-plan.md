# Blazor JS Interop Component Plan

This document describes the recommended path for turning the events calendar into a reusable Blazor component.

## Core recommendation

Start with a full-widget wrapper, not a partial rewrite.

This engine already owns:

- canvas rendering
- toolbar
- list view
- side panel
- modal editor
- playlist-link workflow

That means the lowest-risk first version is a Blazor component that mounts the entire JS widget into one host element and forwards typed callbacks between JavaScript and .NET.

## Target architecture

### Layer 1: shared Blazor wrapper

Create a reusable component such as:

- `EventsCalendar.razor`
- `EventsCalendar.razor.cs`
- `eventsCalendarInterop.js`

This first wrapper can render only:

- one host `<div>`

The JS side can continue building the internal widget DOM.

### Layer 2: typed DTOs

Create models such as:

- `CalendarEventModel`
- `CalendarPlaylistLink`
- `CalendarConnectedEvent`
- `CalendarOptions`
- `CalendarVisibleRange`
- `CalendarSelectionChangedArgs`
- `CalendarViewChangedArgs`
- `CalendarDateChangedArgs`
- `CalendarExportRequest`
- `CalendarOperationContext`

Mirror the current JS contracts first. Do not abstract them too early.

### Layer 3: JS module adapter

Create a thin adapter that:

- loads `zy-canvas-primitives.js`
- loads `zy-canvas-calendar.js`
- creates the controller
- forwards callbacks into .NET
- exposes instance methods to Blazor

## Recommended Blazor API

The first wrapper should expose parameters like:

```csharp
[Parameter] public IReadOnlyList<CalendarEventModel> Events { get; set; } = Array.Empty<CalendarEventModel>();
[Parameter] public CalendarOptions Options { get; set; } = new();
[Parameter] public EventCallback<CalendarCreateRequest> EventCreateRequested { get; set; }
[Parameter] public EventCallback<CalendarUpdateRequest> EventUpdateRequested { get; set; }
[Parameter] public EventCallback<CalendarDeleteRequest> EventDeleteRequested { get; set; }
[Parameter] public EventCallback<CalendarPlaylistSearchRequest> PlaylistSearchRequested { get; set; }
[Parameter] public EventCallback<CalendarPlaylistMutationRequest> PlaylistLinkRequested { get; set; }
[Parameter] public EventCallback<CalendarPlaylistMutationRequest> PlaylistCloneRequested { get; set; }
[Parameter] public EventCallback<CalendarPlaylistMutationRequest> PlaylistUnlinkRequested { get; set; }
[Parameter] public EventCallback<CalendarDateChangedArgs> DateChanged { get; set; }
[Parameter] public EventCallback<CalendarViewChangedArgs> ViewChanged { get; set; }
[Parameter] public EventCallback<CalendarSelectionChangedArgs> SelectionChanged { get; set; }
[Parameter] public EventCallback<CalendarExportRequest> ExportRequested { get; set; }
```

## Recommended JS interop surface

Expose methods like:

- `create`
- `destroy`
- `setEvents`
- `updateOptions`
- `setMessage`
- `getSelectedEvent`
- `getVisibleEvents`

That mirrors the practical public surface of the current controller.

## Migration phases

### Phase 1: full-widget wrapper

Goal:

- preserve behavior with minimal risk

Deliverables:

- one Blazor component mounting the full JS widget
- typed callback bridge for CRUD, playlist linking, and export
- typed event models in .NET

This phase is the fastest path to a usable shared component.

### Phase 2: stabilize contracts and assets

Goal:

- move the engine, primitives, and styles into a dedicated shared package

Deliverables:

- shared JS asset bundle
- extracted calendar stylesheet instead of runtime string injection
- stable callback and DTO contracts

### Phase 3: optional Blazor shell decomposition

Goal:

- move some non-canvas surfaces into Razor components if there is a real product reason

Candidates:

- toolbar
- side panel
- list view
- modal editor

Do this only after the JS wrapper is already stable.

## Important implementation notes

### Keep rendering and hit testing in JavaScript

The calendar's value is in:

- fast canvas redraws
- region-based hit testing
- drag previews
- overlap layout
- timezone-aware slot math

That logic should remain on the JS side.

### Keep UTC canonical

The Blazor wrapper should exchange UTC-based event payloads with .NET and let the JS engine continue owning display-timezone projection for client interactions.

### Keep the single-host mount model first

Because the engine already builds its own shell, the simplest wrapper is:

- one host element
- one controller instance
- one cleanup path

### Extract CSS later, not first

The current engine injects its own styles. That is acceptable for the first wrapper. Converting that style text to a standalone asset can happen after parity.

## What not to do in version one

- do not replace it with a third-party calendar library
- do not move canvas layout math into C#
- do not split the shell into Blazor and JS without a stable contract
- do not remove views or interactions for convenience
- do not break export semantics based on visible events

## Codex implementation order

When Codex later starts the shared Blazor component work, it should follow this order:

1. Wrap the existing full widget in a Blazor component with JS interop.
2. Introduce typed .NET DTOs that mirror the current event and callback contracts.
3. Prove round-trip CRUD, drag, resize, playlist linking, and export.
4. Extract styles and scripts into a shared asset location.
5. Only then decide whether toolbar, panel, list, or editor should move into Razor.
