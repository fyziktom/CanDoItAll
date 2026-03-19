# Recommended Missing Components

The current shared library is usable, but it is still missing several components that a large Blazor application will need repeatedly.

## Priority 0: foundation gaps

### 1. Real modal dialog system

Why:

- `Dialog` is currently a placeholder
- large apps inevitably need confirmation flows, editors, pickers, and blocking actions

Recommended shape:

- `DialogService`
- `DialogHost`
- `ConfirmDialog`
- `ModalShell`

### 1A. Internal workbench tab system

Why:

- the application needs many concurrent work surfaces without opening many browser tabs
- Blazor Interactive Server benefits from deliberate tab sleep and restore behavior

Recommended shape:

- `AppTabStrip`
- `AppTab`
- `TabOverflowMenu`
- `DirtyStateDot`
- `SleepStateBadge`
- `ITabHostService`
- `ITabPersistenceStore`

### 2. Real tooltip / popover / context-menu system

Why:

- both `Tooltip` and `ContextMenu` are placeholders
- richer information density and command menus will otherwise be rebuilt ad hoc

Recommended shape:

- `TooltipHost`
- `Popover`
- `ContextMenuService`
- `ContextMenuHost`

### 3. Validation-aware field layer

Why:

- `FormField` is visual only
- large forms need validation summary, field messages, and standard error styling

Recommended shape:

- `ValidatedFormField`
- `FieldHint`
- `FieldMessage`
- `FormSection`

## Priority 1: productivity components

### 4. Searchable select / autocomplete

Why:

- `DropDown<TValue>` only handles small static lists
- real business apps need searchable pickers for users, projects, songs, tags, and entities

Recommended features:

- async search
- custom item template
- keyboard navigation
- empty/loading states

### 5. Date and time controls

Why:

- no `DatePicker`, `TimePicker`, or combined `DateTimePicker`
- scheduling-heavy apps will need these immediately

### 6. Tag / chip input

Why:

- current library has no structured token input
- tags, labels, categories, and filters are common

### 7. File upload / media picker

Why:

- no upload dropzone, preview card, or progress-aware uploader
- content-heavy workflows will need a shared pattern

## Priority 2: richer display components

### 8. Better data grid

Why:

- current `DataGrid<TItem>` is intentionally minimal
- a serious admin or planning surface will need sorting, filtering, empty states, sticky headers, and selection

Recommended path:

- either extend the current grid carefully
- or create a separate advanced grid component instead of mutating the simple one beyond recognition

### 9. Better charts

Why:

- current `Chart` is a lightweight line renderer only
- future analytics pages will need more robust charts

Recommended path:

- a JS-backed chart wrapper with strongly typed Blazor models

### 10. Empty state, skeleton, and loading primitives

Why:

- current pages rely on ad hoc markup
- large apps benefit from a standard loading and empty-state language

Recommended additions:

- `EmptyState`
- `SkeletonBlock`
- `Spinner`
- `InlineBusy`

## Priority 3: navigation and chrome

### 11. Menu primitives

Why:

- no shared breadcrumb, action menu, secondary nav, or segmented control

### 12. Badges, chips, avatar, and status pills

Why:

- these are repeated inline in the app with raw HTML and app-specific classes

### 13. Accordion distinct from fieldset

Why:

- `Fieldset` is currently doing double duty
- content accordions deserve a purpose-built component with explicit state

## Priority 4: canvas and editor strategy

### 14. Blazor wrapper for the shared canvas engine

Why:

- `zyphonote-web` already has a capable canvas engine
- the Blazor side will soon need the same capability for planners, maps, and structured editors

Recommended deliverables:

- a Blazor `CanvasWorkbench` component
- a JS interop bridge
- strongly typed manifest and selection models
- host components for ribbon, dock, and context menu chrome

### 15. Project calendar wrapper and scheduling primitives

Why:

- project work needs milestone, review, deadline, and release scheduling
- the architecture package now treats the calendar as a first-class workbench surface

Recommended deliverables:

- a Blazor `ProjectCalendar` wrapper
- typed event and linkage models
- shared date and time editing primitives
- workbench integration so calendar items open related artifacts

## Priority 5: development acceleration surfaces

### 16. Development watch and tuning primitives

Why:

- the architecture now depends on a local manager, watch-ready feedback, and targeted tuning mode
- these should not be rebuilt ad hoc inside feature pages

Recommended deliverables:

- `DevWatchStatusBadge`
- `TunableComponentBoundary`
- `TuningHandle`
- `TuningRequestPanel`
- `ClipboardImagePasteZone`
- `CapsuleSummaryCard`
- `ManagerNotificationToast`

## Recommended sequencing

1. Dialog / tooltip / context menu foundation
2. Internal workbench tab system
3. Validation-aware form layer
4. Searchable select + date/time controls
5. Canvas wrapper and project calendar wrapper
6. Development watch and tuning primitives
7. Better grid and chart story

This order removes the biggest blockers for real application development without prematurely over-engineering the smaller display primitives.
