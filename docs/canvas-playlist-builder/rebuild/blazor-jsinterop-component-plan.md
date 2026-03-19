# Blazor JS Interop Component Plan

This document describes the recommended path for turning the current canvas system into a reusable Blazor component.

## Core recommendation

Do not rebuild the canvas rendering and interaction engine in C# first.

First build a Blazor wrapper around the existing JavaScript engine. Then move the reusable engine and its styles into a dedicated shared solution.

That approach is lower risk because the hardest parts are already working:

- high-frequency canvas rendering
- viewport math
- hit testing
- drag and marquee interactions
- animated collapse and expand
- context-menu anchoring

## Target architecture

### Layer 1: shared Blazor component

Create a reusable component such as:

- `PlaylistCanvas.razor`
- `PlaylistCanvas.razor.cs`
- `PlaylistCanvas.razor.css` or shared stylesheet
- `playlistCanvasInterop.js`

This component should render:

- canvas host container
- actual `<canvas>`
- optional surrounding slots or panels if needed

### Layer 2: C# DTOs

Create strongly typed models such as:

- `PlaylistCanvasManifest`
- `PlaylistCanvasSection`
- `PlaylistCanvasItem`
- `PlaylistCanvasPackageMeta`
- `PlaylistCanvasSelection`
- `PlaylistCanvasNode`
- `PlaylistCanvasUiState`
- `PlaylistCanvasAction`
- `PlaylistCanvasProgressEntry`

These DTOs should mirror the current JS contracts closely in version one.

### Layer 3: JS module adapter

Create a thin JS adapter that:

- imports or wraps the existing engine
- creates and caches controller instances
- forwards callbacks to .NET
- exposes imperative methods callable from Blazor

## Recommended Blazor public API

The first reusable component should support parameters like:

```csharp
[Parameter] public PlaylistCanvasManifest Manifest { get; set; } = default!;
[Parameter] public PlaylistCanvasPackageMeta PackageMeta { get; set; } = default!;
[Parameter] public IReadOnlyDictionary<string, PlaylistScoreMeta>? ScoreMap { get; set; }
[Parameter] public IReadOnlyDictionary<string, PlaylistAssetMeta>? AssetMap { get; set; }
[Parameter] public PlaylistCanvasUiState? InitialUiState { get; set; }
[Parameter] public string Mode { get; set; } = "authoring";
[Parameter] public string? CurrentItemKey { get; set; }
[Parameter] public EventCallback<PlaylistCanvasSelection> SelectionChanged { get; set; }
[Parameter] public EventCallback<PlaylistCanvasUiState> UiStateChanged { get; set; }
[Parameter] public EventCallback<PlaylistCanvasAction> ActionRaised { get; set; }
[Parameter] public Func<PlaylistCanvasNode, IReadOnlyList<PlaylistCanvasCommand>>? BuildContextActions { get; set; }
```

Use strongly typed command DTOs instead of passing raw anonymous objects between C# and JavaScript.

## Recommended JS interop surface

The JS adapter should expose methods like:

- `create`
- `destroy`
- `setData`
- `setMode`
- `setSelection`
- `focusNode`
- `fitView`
- `setZoomPercent`
- `getState`
- `resize`

This should map almost directly to the existing controller API.

## Event flow recommendation

### JS to .NET

Forward these events into .NET:

- selection changed
- UI state changed
- canvas action raised
- open-score action raised

### .NET to JS

Use Blazor to push:

- new manifest state
- score metadata changes
- current item changes
- restored UI state
- external focus commands

## Component ownership split

### Keep in JavaScript

- canvas rendering
- scene building
- viewport math
- hit testing
- pointer interactions
- help overlay and zoom chrome

### Move to Blazor

- inspector forms
- playlist outline
- score library
- persistence
- validation
- save and publish commands
- route and tab integration
- application services

## Migration phases

### Phase 1: wrap the existing engine

Goal:

- keep behavior nearly identical
- prove clean JS interop contract
- move playlist page logic into Razor components and C# services

Deliverables:

- reusable Blazor canvas component
- typed C# models
- inspector and outline rewritten in Razor
- hidden textarea removed

### Phase 2: extract reusable canvas package

Goal:

- move canvas JS, CSS, and Blazor wrapper into a dedicated shared project

Deliverables:

- shared component package
- stable command and state contracts
- reusable theme assets

### Phase 3: generalize beyond playlist builder

Goal:

- support project structures, learning maps, mindmaps, or editorial maps

Likely changes:

- more node types
- optional deeper hierarchies
- optional docked side panels
- richer keyboard support
- snapping or layout modes

## Important implementation notes

### Preserve ids

Node ids must stay stable across renders:

- `package:root`
- `section:<sectionKey>`
- `item:<itemKey>`

Stable ids are what make persisted selection, collapsed state, and manual positions reliable.

### Preserve manual positions separately from semantic order

Do not confuse:

- dragging for visual arrangement
- reordering for data order

The current engine keeps those concerns separate. That is correct and should remain.

### Keep context-action vocabulary stable

The current command family includes:

- edit node
- add section
- add score
- remove node
- duplicate node
- open score detail
- open score workspace

Keep that vocabulary stable while porting so the rest of the editor can migrate incrementally.

### Replace HTML-string inspector rendering

The current page controller renders large HTML strings. In Blazor, replace that with:

- strongly typed forms
- smaller components
- normal event callbacks
- validation-aware models

### Replace sessionStorage policy with explicit state control

The first Blazor version can still use browser storage, but it should be behind a clear abstraction.

Recommended shape:

- component emits `UiStateChanged`
- host decides whether to persist
- host decides where to persist

## What not to do in version one

- do not rewrite the renderer in SVG
- do not rewrite the engine in pure C#
- do not generalize into arbitrary graphs before the wrapper exists
- do not combine data ordering and visual offsets
- do not hide engine limitations behind vague names

## Codex implementation checklist

When Codex later starts the Blazor component work, it should follow this order:

1. Reuse the engine behavior documented in this folder, not assumptions about generic diagram editors.
2. Create typed C# models that mirror the current manifest and UI state.
3. Implement a thin JS adapter around the existing controller.
4. Port inspector, outline, and library panels into Razor components.
5. Add persistence and save commands only after the wrapper can round-trip selection, zoom, collapse, and drag state.
