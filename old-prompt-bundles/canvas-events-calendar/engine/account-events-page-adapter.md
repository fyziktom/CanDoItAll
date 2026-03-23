# Account Events Page Adapter

This document covers `C:\repositories\zyphonote-web\src\assets\js\zy-account-events-page.js`.

This file is not the calendar component itself. It is the host-page adapter for `account-events.php`.

## What this file owns

- Reading `window.ZyAccountEventsPageData`
- Detecting browser timezone and locale
- Wrapping fetch and JSON error handling
- Mapping calendar callbacks to the PHP API
- Instantiating the calendar engine
- Exposing a small debug API on `window.ZyAccountEventsPage`

## Helper functions

### Basic helpers

- `asText`
- `safeArray`
- `eventIdOf`
- `playlistIdOf`
- `findEventById`

### Browser defaults

- `browserTimeZone(fallback)`
- `browserLocale(fallback)`

The adapter prefers the browser timezone over the server default when it mounts the calendar.

### Request helper

- `jsonRequest(url, options)`

This is the uniform fetch wrapper used for all JSON actions. It enforces:

- `credentials: 'same-origin'`
- JSON parsing
- normalized error throwing when `payload.ok === false`

## API wiring functions

### Event CRUD

- `buildSaveBody(event, mode)`
- `saveEvent(event, context)`
- `deleteEvent(event)`

These map the normalized JS event model to the PHP API fields.

### Export

- `exportVisible(format, visibleEvents)`

This posts only the currently visible event ids and downloads the returned blob using a temporary `<a>` element.

### Refresh

- `refreshEvents(calendar)`

This re-fetches the full event set and calls `calendar.setEvents(...)`.

### Playlist linking

- `searchPlaylists(query)`
- `mutatePlaylist(action, event, playlist)`

`mutatePlaylist` is used for:

- `playlist_link`
- `playlist_unlink`
- `playlist_clone`

After mutation, it refreshes the full calendar event set so editor state and linked playlist counts stay consistent.

## Calendar creation

The adapter mounts:

```js
calendar = window.ZyCanvasCalendar.create({
  host,
  events,
  initialView,
  selectedDate,
  selectedEventId,
  timezone,
  locale,
  weekStartsOn,
  slotMinutes,
  businessHoursStart,
  businessHoursEnd,
  miniMonthCount,
  allowCreate,
  allowEdit,
  allowDelete,
  allowDragDrop,
  allowResize,
  enableListExport,
  eventTypes,
  eventStatuses,
  timeZoneOptions,
  onEventCreate,
  onEventUpdate,
  onEventDelete,
  onPlaylistSearch,
  onPlaylistLink,
  onPlaylistClone,
  onPlaylistUnlink,
  onExportRequest
});
```

## Important callback mapping

- `onEventCreate` -> `saveEvent`
- `onEventUpdate` -> `saveEvent`
- `onEventDelete` -> `deleteEvent`
- `onPlaylistSearch` -> `searchPlaylists`
- `onPlaylistLink` -> `mutatePlaylist('playlist_link', ...)`
- `onPlaylistClone` -> `mutatePlaylist('playlist_clone', ...)`
- `onPlaylistUnlink` -> `mutatePlaylist('playlist_unlink', ...)`
- `onExportRequest` -> `exportVisible(...)`

This is a clean adapter boundary and should map well to Blazor callbacks.

## Public page API

The adapter exposes:

```js
window.ZyAccountEventsPage = {
  calendar: calendar,
  refresh: function() {
    return refreshEvents(calendar);
  }
};
```

That is useful for diagnostics, but it is not the component contract that should be copied into Blazor.

## Guidance for the Blazor port

- Do not carry this exact file forward as a shared component.
- Keep this layer conceptually as a host adapter or service bridge.
- Replace `fetch` and `FormData` calls with .NET callbacks or typed HTTP services.
- Preserve the callback shape because it already separates the UI engine from persistence.
