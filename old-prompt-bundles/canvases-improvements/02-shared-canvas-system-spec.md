# Shared Canvas System Spec

## Goal

Build one reusable `CanDoItAll` canvas workbench system that both:

- `ProjectStructurePage`
- `PromptFactoryPage`

can consume without visual or behavioral drift.

The result should feel like one product family, not two separately styled editors.

## Shared architecture

### JavaScript owns

- canvas rendering
- scene graph preparation from already-normalized DTOs
- hit testing
- viewport math
- pointer gestures
- zoom and pan clamping
- branch controls
- multi-selection marquee
- context menu placement
- canvas chrome
- help overlay

### Blazor owns

- page composition
- domain data loading
- create/update/remove commands
- inspector form rendering
- lower supporting panels
- route/tab navigation
- persistence
- validation and save flows
- testable UI state outside the canvas engine

## Shared component target

Recommended shared location:

- `C:\repositories\CanDoItAll\src\CanDoItAll.ComponentKit`

Recommended shared surface:

- reusable Razor host component
- reusable DTO contracts for canvas input and emitted state
- one shared JS interop module
- one shared CSS token and component layer

Suggested shared file set:

- `src\CanDoItAll.ComponentKit\Components\CanvasWorkbench.razor`
- `src\CanDoItAll.ComponentKit\Components\CanvasWorkbench.razor.cs`
- `src\CanDoItAll.ComponentKit\Canvas\CanvasWorkbenchContracts.cs`
- `src\CanDoItAll.ComponentKit\wwwroot\js\canvasWorkbenchInterop.js`
- `src\CanDoItAll.ComponentKit\wwwroot\canvas-workbench.css`

The exact names can change, but the shared ownership cannot.

## Shared JS interop API

The first stable public JS API should support:

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

Optional but strongly recommended:

- `openCreateMenu`
- `toggleHelp`
- `toggleMaximize`

## Shared data contract

### Shared canvas input model

Both editors should normalize into one common canvas DTO shape:

```csharp
public sealed class CanvasWorkbenchSurface
{
    public string SurfaceId { get; set; } = string.Empty;
    public string Mode { get; set; } = "authoring";
    public CanvasWorkbenchNode RootNode { get; set; } = default!;
    public List<CanvasWorkbenchNode> Nodes { get; set; } = [];
    public List<CanvasWorkbenchLink> Links { get; set; } = [];
    public CanvasWorkbenchStats Stats { get; set; } = new();
    public CanvasWorkbenchUiState? UiState { get; set; }
    public CanvasWorkbenchChrome Chrome { get; set; } = new();
    public CanvasWorkbenchMenuSchema MenuSchema { get; set; } = new();
}
```

Shared node fields should cover:

- stable id
- parent id
- node family (`root`, `group`, `item`, `special`)
- domain kind (`project`, `phase`, `prompt-session`, `prompt-step`, `decision`, `checkpoint`, etc.)
- title
- subtitle
- lead text
- status
- branch status
- branch key
- chips
- accent palette
- icon
- shape treatment
- estimated minutes
- progress percent
- required/optional flag
- linked artifact metadata
- visibility flags
- canonical position
- optional manual offset

### Shared emitted UI state

The persisted UI state must support at least:

- `selectedNodeId`
- `selectedNodeIds`
- `collapsedNodeIds`
- `manualPositions`
- `zoom`
- `panX`
- `panY`
- `isMaximized`
- `activeInspectorTab`

Project- or prompt-specific extensions are allowed, but the shared state contract must not fork.

## Shared visual system

### Stage layout

Every canvas editor page should use this structure:

1. Page header or wizard header.
2. Main editor stage:
   - left: canvas stage
   - right: inspector stage
3. Lower supporting stage:
   - outline / structure controls / session tools / resource libraries / advanced editors

Recommended responsive behavior:

- desktop: two-column stage
- tablet: stacked stage with canvas first
- mobile: stacked stage, zoom panel moves to full-width top overlay, hint hides

### Surface tokens

Recommended shared CSS variables:

```css
:root {
  --cw-stage-radius: 28px;
  --cw-card-radius: 24px;
  --cw-panel-radius: 22px;
  --cw-border-soft: rgba(15, 23, 42, 0.10);
  --cw-shadow-soft: 0 18px 36px rgba(15, 23, 42, 0.10);
  --cw-shadow-strong: 0 24px 56px rgba(15, 23, 42, 0.16);
  --cw-bg-start: #fff4e5;
  --cw-bg-mid: #f7faf7;
  --cw-bg-end: #eef2ff;
  --cw-accent-purple-start: #8b5cf6;
  --cw-accent-purple-end: #6d28d9;
  --cw-dark-card: #111827;
  --cw-dark-card-border: #14b8a6;
}
```

Visual rules:

- large rounded corners everywhere
- very light surfaces, never flat sterile white
- subtle blur/airiness where appropriate
- purple utility chrome
- mint/teal accent used for positive authoring guidance
- clear dark root card for the top package/session/project card

### Canvas background

Required:

- warm-left to cool-right gradient
- subtle dot pattern
- no raw grid-only background for the final editor

### Node families

#### Root nodes

Visual rules:

- strongest contrast
- dark surface
- highest emphasis
- compact but information-dense

Used by:

- project root in project structure
- prompt session root in prompt wizard

#### Group nodes

Visual rules:

- white or near-white surface
- accent rail or accent outline
- duration/state pill in top-right
- footer chips

Used by:

- phases or group-like project clusters
- prompt branches or major flow groups
- section-like containers

#### Item nodes

Visual rules:

- pastel by type
- type pill on top-left
- duration or state pill on top-right
- requirement/status chip in footer
- selection ring or glow

Used by:

- repositories, files, notes, decisions, milestones, prompt steps, linked artifacts, media items

### Shared utility chrome

Required placements:

- top-left: add launcher and focus action
- top-right: fit, maximize, help, zoom rail
- bottom-left: mode-specific hint
- center overlay: help card

The zoom cluster must be a single pill rail, not separate detached controls.

### Shared create/menu language

Two related but distinct menus are required:

#### Persistent add launcher

Purpose:

- fast create without right-click
- always visible entry point
- supports typed creation

Visual target:

- screenshot-observed vertical hex rail
- anchored below or beside the top-left add button

#### Node context menu

Purpose:

- node-aware add/edit/remove actions
- right-click and possibly long-press

Visual target:

- hexagonal/radial or reference-styled context menu
- action count depends on node family and mode

## Shared interaction contract

Required shared behavior:

- click selects
- right-click opens node-aware actions
- wheel zoom anchors under pointer
- empty-space drag pans
- middle-mouse drag pans
- Ctrl/Cmd + drag moves node(s)
- Alt + drag starts marquee selection
- `+`, `-`, `0`, `?`, `h`, `Escape` shortcuts
- package/group double-click toggles collapse
- domain-specific double-click can open artifact

Required shared persistence:

- selection survives refresh when node ids still exist
- collapse survives refresh
- maximize survives refresh
- manual positioning survives refresh
- zoom/pan survives refresh

## Shared inspector contract

### Empty state

Every editor needs a polished empty state with:

- kicker
- title
- short explanation
- shortcut reminder

### Single selection state

Inspector layout:

- header card
- optional stat chip row
- editor body card

Package/root nodes may use tabs.

Group/item nodes may use mirrored editor forms or strongly typed Blazor forms, but the final visual result must match the reference surface hierarchy.

### Multi-selection state

Required capabilities:

- summary count
- listed selected nodes
- batch duplicate where valid
- batch remove where valid
- batch status/branch action where valid

### Preview/read-only state

The same canvas engine must support a non-authoring mode where:

- editing controls are hidden
- preview-specific details remain visible
- the inspector becomes read-only

## Editor-specific mappings

### Project structure mapping

#### Visual mapping

| Current domain object | Shared family | Visual intent |
| --- | --- | --- |
| `ProjectRoot` | root | Dark package-style card |
| `Phase` | group | Section-like branch card |
| `PromptSession` | group | Prompt cluster card |
| `PromptStep` | item | Typed prompt step card |
| `Repository`, `File`, `Link`, `Connector` | item | Resource-type cards with distinct type palettes |
| `Milestone`, `Decision`, `ValidationRun`, `TestPlan` | item or special | Action-heavy status cards |
| `Note`, `SecretReference` | item or special | Distinct palette and icon treatment |

#### Action mapping

Project structure must keep existing domain actions:

- open
- branch
- validate
- test
- skip
- mark used
- link

It must also gain the richer create affordances:

- quick add from root
- quick add adjacent to source
- typed add palette in the persistent create launcher

### Prompt wizard mapping

#### Visual mapping

| Prompt domain object | Shared family | Visual intent |
| --- | --- | --- |
| session | root | Dark package/session root card |
| main branch / follow-up branch groups | group | Section-like branch cards |
| prompt run node | item | Typed prompt step card |

#### Action mapping

Prompt wizard must surface:

- branch step
- open prompt artifact
- mark used / validated / skipped where applicable
- focus step
- build / save session / export / send from surrounding panels

The prompt wizard can keep its step navigation at the top, but the flow editor itself must become a canvas workbench stage rather than a plain stacked list.

## Modal spec

Any modal introduced for either editor must reuse the reference modal language:

- large white rounded surface
- soft backdrop
- pill step indicators where the modal is multi-step
- top-right pill close button
- same border/shadow family as the canvas inspector surfaces

Do not mix in a different modal design system for canvas-adjacent tasks.

## Accessibility baseline

Required:

- keyboard focus for host and actionable overlay controls
- ARIA labels for fit/maximize/help/zoom controls
- visible focus states
- inspector actions reachable without canvas-only gestures
- context actions reachable by non-right-click pathways through the add launcher and inspector actions
