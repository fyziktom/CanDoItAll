# Canvas Calendar Engine

This document covers `C:\repositories\zyphonote-web\src\assets\js\zy-canvas-calendar.js`.

This file is the reusable calendar engine. It is the main candidate for a future Blazor JS interop wrapper.

## Public entry point

The engine exports:

```js
window.ZyCanvasCalendar = {
  create: function(options) {
    return new CalendarController(options);
  }
};
```

## Important architectural difference from the playlist builder canvas

This engine does much more than paint a canvas. It also builds and manages:

- toolbar
- status chips
- list view
- side panel
- modal editor
- linked-playlist workflow
- live region

For parity, the first Blazor wrapper can mount the whole widget into one host div.

## Creation options

Important options accepted by the controller:

| Option | Purpose |
| --- | --- |
| `host` | Required host element |
| `events` | Initial event array |
| `initialView` | `day`, `week`, `month`, `year`, or `list` |
| `selectedDate` | Initial focused date key |
| `selectedEventId` | Initially selected event |
| `timezone` | Active display timezone |
| `locale` | Active locale |
| `weekStartsOn` | 0-6 week start |
| `slotMinutes` | Timed-grid slot granularity |
| `businessHoursStart` | First rendered hour |
| `businessHoursEnd` | Last rendered hour |
| `miniMonthCount` | Number of mini months in week sidebar |
| `allowCreate` | Enable creation interactions |
| `allowEdit` | Enable editor usage |
| `allowDelete` | Enable delete actions |
| `allowDragDrop` | Enable drag interactions |
| `allowResize` | Enable timed-event resizing |
| `enableListExport` | UI flag for export usage |
| `eventTypes` | Editor type options |
| `eventStatuses` | Editor status options |
| `timeZoneOptions` | Extra timezone suggestions |
| `emptyMessage` | List/empty state copy |
| `onEventCreate` | Persistence callback |
| `onEventUpdate` | Persistence callback |
| `onEventDelete` | Persistence callback |
| `onPlaylistSearch` | Playlist search callback |
| `onPlaylistLink` | Link existing playlist callback |
| `onPlaylistClone` | Clone-and-link callback |
| `onPlaylistUnlink` | Unlink callback |
| `onDateChange` | Date navigation callback |
| `onViewChange` | View change callback |
| `onTimezoneChange` | Timezone change callback |
| `onSelectionChange` | Event selection callback |
| `onExportRequest` | Export callback |

## Effective instance API

The returned controller exposes these methods and they are safe to treat as the practical component API:

- `setEvents(events)`
- `updateOptions(options)`
- `destroy()`
- `setMessage(message, tone)`
- `getSelectedEvent()`
- `getVisibleEvents(scope)`

There are also additional controller methods that are callable but should be considered more internal:

- `selectDate(dateKey, updateAnchor)`
- `setView(view, announce)`
- `shiftRange(direction)`
- `openEditor(event, mode)`

## Internal state model

The controller tracks:

- `view`
- `lastSpatialView`
- `listScope`
- `selectedDateKey`
- `anchorDateKey`
- `timezone`
- `locale`
- `hoveredRegion`
- `selectedEventId`
- `focusedDateKey`
- `interaction`
- `busy`
- `message`
- `messageTone`
- `layoutCache`
- `visibleEvents`
- `selectedEvent`
- `events`

Important note:

- `onSelectionChange` fires when an event is selected
- date-only selection does not emit the same event-selection callback

## DOM built by the engine

`buildDom()` creates all internal markup inside the host:

- `.zy-calendar-shell`
- `.zy-calendar-toolbar`
- `.zy-calendar-body`
- `.zy-calendar-canvas-shell`
- `.zy-calendar-list-shell`
- `.zy-calendar-panel`
- `.zy-calendar-live-region`
- `.zy-calendar-backdrop`
- `.zy-calendar-editor`
- playlist-search and playlist-choice subtrees

This means the current component is not just a canvas renderer. It is a full mini-application mounted into one element.

## Main controller responsibilities

### Lifecycle

- `buildDom()`
- `bindEvents()`
- `unbindEvents()`
- `scheduleRender()`
- `refreshUi()`
- `destroy()`

### Selection and view state

- `getSelectedEvent()`
- `selectEventById(eventId, announce)`
- `selectDate(dateKey, updateAnchor)`
- `setView(view, announce)`
- `shiftRange(direction)`
- `announceSelection()`

### Data and options

- `setEvents(events)`
- `updateOptions(options)`
- `setMessage(message, tone)`

### Panel and list rendering

- `renderStatusChips()`
- `renderPanel()`
- `renderList()`

The side panel shows either:

- selected-event stats, metadata, and actions
- or visible-range stats and helper actions when no event is selected

### Editor and playlist workflows

- `supportsPlaylistLinking()`
- `renderEditorPlaylists(event)`
- `renderPlaylistSearchResults()`
- `requestPlaylistSearch(query)`
- `schedulePlaylistSearch(query, immediate)`
- `openPlaylistChoiceDialog(playlist)`
- `closePlaylistChoiceDialog()`
- `runPlaylistMutation(callback, successMessage)`
- `openEditor(event, mode)`
- `closeEditor()`
- `toggleEditorFields(enabled)`
- `setEditorMessage(message, tone)`
- `editorValue()`

Important behavior:

- playlist search is debounced at `180ms`
- search results are tokenized so stale responses are ignored
- linking an already-used playlist opens a reuse-or-copy dialog

### Persistence helpers

- `upsertEvent(event)`
- `removeEventById(eventId)`
- `persistEvent(mode, event)`
- `persistDelete(event)`
- `requestExport(format)`

## Callback contract

Callback contexts are already structured and useful for Blazor.

### Event create/update

The engine passes:

- the normalized event payload
- context with `mode`, `view`, `selectedDate`, and `timezone`

### Event delete

The engine passes:

- the selected event
- context with `view`, `selectedDate`, and `timezone`

### Playlist search and mutations

The engine passes:

- query or playlist plus current editor event
- context with `view`, `selectedDate`, and `timezone`

### View/date/timezone callbacks

- `onDateChange(dateKey, context)`
- `onViewChange(view, context)`
- `onTimezoneChange(timeZone, context)`

### Export callback

`onExportRequest(format, visibleEvents, context)` receives the already filtered visible set.

## Data normalization and timezone handling

Important helpers near the top of the file:

- `normalizeEvent`
- `formatRangeLabel`
- `toLocalInputValue`
- `localInputToUtcIso`
- `buildUtcIsoFromDateKeyMinutes`
- `getEventSpan`
- `buildDensityMap`
- `buildDefaultEvent`

Key conclusion:

- UTC stays canonical
- the component renders in the active display timezone
- editor inputs round-trip through timezone-aware conversion in JS
- no external date library is required

## Rendering entry points

`render()` chooses one of three canvas rendering paths:

- `renderTimedView(ctx, size, mode)` for `day` and `week`
- `renderMonthView(ctx, size)`
- `renderYearView(ctx, size)`

`list` view is DOM-based and clears the canvas.

## Interaction helpers

The engine uses:

- `regionAtEvent(event)`
- `updateCursor(region)`
- `resolveTimedPoint(point)`
- `buildMovedTimedEvent(event, dateKey, startMinutes)`
- `buildResizedEvent(event, handleType, dateKey, minutes)`
- `buildShiftedDayEvent(event, targetDateKey)`
- `activateRegion(region)`

These functions are the core of drag, resize, create, and selection behavior.

## Input handlers

Canvas and shell interaction is implemented by:

- `onCanvasPointerDown`
- `onCanvasPointerMove`
- `onCanvasLeave`
- `onWindowPointerMove`
- `finishInteraction`
- `onWindowPointerUp`
- `onCanvasDoubleClick`
- `onCanvasKeyDown`
- `onToolbarClick`
- `onToolbarChange`
- `onPanelClick`
- `onModalClick`
- `onModalChange`
- `onModalInput`
- `onModalSubmit`

## Known limitations

- no pan or zoom
- timed views are limited to configured business hours, not a virtual 24-hour scroller
- internal DOM is string-built rather than componentized
- styling is injected rather than shipped as a standalone stylesheet
- selection callback is event-centric rather than fully state-centric

Those are acceptable constraints for a first Blazor wrapper. They should just be documented, not hidden.
