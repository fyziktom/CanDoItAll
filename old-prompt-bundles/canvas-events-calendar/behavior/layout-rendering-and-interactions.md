# Layout, Rendering, And Interactions

This document describes how the calendar looks and behaves today so it can be rebuilt accurately for Blazor.

## Overall shell layout

The engine renders a full shell inside one host:

- top toolbar
- main body grid
- stage column
- side panel column
- canvas or list surface
- modal editor overlay

Important CSS structure:

- `.zy-calendar-shell`
- `.zy-calendar-toolbar`
- `.zy-calendar-body`
- `.zy-calendar-stage-shell`
- `.zy-calendar-canvas-shell`
- `.zy-calendar-list-shell`
- `.zy-calendar-panel`
- `.zy-calendar-backdrop`

## Fixed view model

The calendar has five views:

- `day`
- `week`
- `month`
- `year`
- `list`

There is no pan or zoom. Navigation works by:

- changing the active view
- shifting the anchor date
- selecting dates or events

## Timed day and week layout

Timed views are rendered by `renderTimedView`.

### Geometry

Important numbers from the current layout:

- outer padding: `18`
- week sidebar width: `258`
- gap between sidebar and main stage: `16`
- mini-month count defaults to `2`
- all-day strip height is calculated and clamped between `44` and `118`

### Week view structure

Week view includes:

- left sidebar with mini months
- main timed grid with seven day columns
- all-day strip above the timed body
- timed event blocks inside the day columns

### Day view structure

Day view uses the same timed grid logic without the week sidebar.

### Timed grid contract

`drawTimedGrid(...)` returns geometry used by the engine:

- left axis width
- day rects
- body top and height
- minute height

The engine uses that data to place:

- timed event blocks
- resize handles
- drag previews
- current time line

## Month view layout

Month view is rendered as a matrix from `DateMath.buildMonthMatrix(...)`.

Important behavior:

- each cell shows day number and event chips
- only a limited number of visible chips render directly
- overflow becomes a `month-more` region
- selecting overflow switches to list view scoped to the day

This is a canvas-rendered month grid, not a DOM table.

## Year view layout

Year view renders:

- 12 mini-month panels
- arranged in a `3 x 4` grid

Each month panel is interactive:

- clicking a day selects it
- clicking the month panel focuses month view

## List view layout

List view is DOM-based, not canvas-based.

It shows:

- visible rows only
- current list scope
- export-friendly tabular data
- per-row select and edit actions

The canvas is hidden while list view is active.

## Side panel layout

When an event is selected, the panel shows:

- title
- range label
- status, type, linked playlist count, checklist count
- timezone, location, customer, category, description, notes
- actions such as edit, focus, open connected playlist, open playlist builder, delete

When no event is selected, the panel shows:

- visible range stats
- current view and anchor date
- timezone and locale
- keyboard and create hints
- add-event and open-list actions

## Canvas rendering flow

`render()` does this:

1. compute visible events
2. refresh DOM UI
3. clear the hit registry
4. reset layout cache
5. clear the surface
6. dispatch to timed, month, or year rendering

The canvas is redrawn per frame. There is no retained scene graph.

## Overlap layout for timed events

Timed events use `layoutOverlapColumns(items)`.

Algorithm:

1. sort by start time, then by longer end
2. group overlapping events into clusters
3. assign each event to the first available column
4. compute total column count for the cluster

This is why overlapping timed events remain readable side by side.

## All-day layout

All-day spans use `layoutAllDayRows(segments)`.

Algorithm:

1. walk all-day segments
2. place each into the first row without column overlap
3. create more rows as needed

This keeps long spans readable across multiple days.

## Hit regions

The calendar is fully region-driven. Important hit types include:

- `mini-day`
- `all-day-slot`
- `time-column`
- `timed-event`
- `resize-start`
- `resize-end`
- `all-day-event`
- `month-day`
- `month-event`
- `month-more`
- `year-month`
- `year-day`

These region types are what the interaction layer responds to. The Blazor wrapper should preserve them rather than inventing a new gesture contract.

## Mouse and pointer behavior

### Single click

- clicking an event selects it
- clicking a day region selects the date
- clicking a month overflow region opens list view scoped to that day
- clicking a year month region moves into month view

### Drag timed event

- pointer down on `timed-event`
- drag inside timed grid
- preview event follows snapped slots
- pointer up persists update through `onEventUpdate`

### Resize timed event

- pointer down on `resize-start` or `resize-end`
- drag vertically through timed grid
- preview event resizes live
- pointer up persists update

### Drag all-day span

- pointer down on `all-day-event`
- drag to another compatible day region
- preview event shifts to the target date
- pointer up persists update

### Drag month event chip

- pointer down on `month-event`
- drag to another month or day region
- preview event shifts by day
- pointer up persists update

### Create timed event

- pointer down on empty `time-column`
- drag to define the range
- preview draft expands as the pointer moves
- pointer up opens the editor pre-filled with that draft

### Create all-day span

- pointer down on empty `all-day-slot`
- drag across days
- preview all-day span updates live
- pointer up opens the editor pre-filled with that draft

### Double click

- double click event -> open editor
- double click empty day region -> open create editor for that date
- double click empty timed column -> open create editor for that date and time

## Keyboard behavior

The canvas handles:

- `ArrowLeft` -> previous day
- `ArrowRight` -> next day
- `ArrowUp` -> minus seven days
- `ArrowDown` -> plus seven days
- `Enter` -> edit selected event or create on selected date
- `Delete` or `Backspace` -> delete selected event when allowed
- `t` -> jump to today

## Cursor behavior

Cursor feedback is explicit:

- `ns-resize` for resize handles
- `grab` for draggable events
- `pointer` for clickable day and month regions
- `default` otherwise

## Interaction details worth preserving exactly

- drag previews should feel immediate and continuous
- timed moves and resizes should snap to `slotMinutes`
- current-time line should appear in the appropriate timed column
- month overflow should switch to list, not open a modal
- visible-events filtering should match the current view or list scope
- export should use the same visible set the user sees

These details are the practical value of the current component. They should be preserved before any broader redesign.
