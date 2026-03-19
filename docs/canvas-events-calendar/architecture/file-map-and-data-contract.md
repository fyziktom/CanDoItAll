# File Map And Data Contract

## Source file map

| File | Responsibility |
| --- | --- |
| `C:\repositories\zyphonote-web\src\account-events.php` | Page shell, initial host element, initial JSON payload, script loading |
| `C:\repositories\zyphonote-web\src\assets\js\zy-account-events-page.js` | Page adapter that wires CRUD, export, and playlist mutations to the API |
| `C:\repositories\zyphonote-web\src\assets\js\zy-canvas-calendar.js` | Main reusable calendar engine and DOM builder |
| `C:\repositories\zyphonote-web\src\assets\js\zy-canvas-primitives.js` | Shared canvas/date primitives used by the calendar engine |
| `C:\repositories\zyphonote-web\src\api\account-events-calendar.php` | API surface for list, save, delete, playlist linking, playlist search, and export |
| `C:\repositories\zyphonote-web\src\docs\canvas-calendar.md` | Existing local reference note in the source repo |

## Runtime composition

`account-events.php` loads scripts in this order:

1. `assets/js/zy-canvas-primitives.js`
2. `assets/js/zy-canvas-calendar.js`
3. `assets/js/zy-account-events-page.js`

That order is required because:

- the calendar engine depends on `window.ZyCanvasPrimitives`
- the page adapter depends on `window.ZyCanvasCalendar`

## Host DOM contract

Unlike the playlist builder, the PHP page does not define a large prebuilt DOM tree for the feature. It only renders:

`<div id="zy_account_events_calendar"></div>`

The calendar engine owns the internal markup and replaces the host content with:

- toolbar
- status bar
- canvas shell
- list shell
- side panel
- live region
- modal editor
- playlist-choice dialog

This is important for the Blazor wrapper. The first version can mount into a single host element.

## Page data contract

`account-events.php` writes:

```js
window.ZyAccountEventsPageData = {
  csrfToken,
  apiUrl,
  initialEvents,
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
  eventTypes,
  eventStatuses,
  allowCreate,
  allowEdit,
  allowDelete,
  allowDragDrop,
  allowResize,
  enableListExport
};
```

## Event payload contract

The API normalizes event payloads into this shape:

```json
{
  "id": "evt_123",
  "eventId": "evt_123",
  "title": "Wedding rehearsal",
  "description": "",
  "startUtc": "2026-03-08T15:00:00Z",
  "endUtc": "2026-03-08T17:00:00Z",
  "allDay": false,
  "timezone": "Europe/Prague",
  "timezoneName": "Europe/Prague",
  "location": "Grand Hall",
  "locationLabel": "Grand Hall",
  "locationAddress": "",
  "locationLat": null,
  "locationLng": null,
  "customerName": "",
  "customerEmail": "",
  "customerPhone": "",
  "priceAmount": null,
  "currency": "USD",
  "category": "Wedding",
  "eventType": "Wedding",
  "status": "Draft",
  "color": "#8fbf6a",
  "readOnly": false,
  "notes": "",
  "logisticsNote": "",
  "linkedPlaylistCount": 0,
  "checklistItemCount": 0,
  "linkedPlaylists": [],
  "checklistRows": [],
  "playlistsBuilderUrl": "account-playlists.php?event_id=evt_123&tab=builder",
  "createdUtc": "",
  "updatedUtc": ""
}
```

Important rules:

- UTC is canonical storage.
- Rendering uses a selected display timezone.
- Event-local timezone is still preserved on the event model.
- The editor converts between local input values and UTC in JavaScript.

## Linked playlist payload contract

Each linked playlist entry is shaped for editor usage:

- `playlistId`
- `title`
- `subtitle`
- `purpose`
- `status`
- `builderUrl`
- `connectedEventCount`
- `connectedEvents`
- `isPrimaryEvent`
- optional score counts and metadata used in playlist cards

Each connected event entry also carries:

- `eventId`
- `eventUrl`
- `isPrimary`

## API action surface

`api/account-events-calendar.php` supports these actions:

| Action | Method | Purpose |
| --- | --- | --- |
| `list` | `GET` | Return all owner events |
| `get` | `GET` | Return one event |
| `save` | `POST` | Create or update an event |
| `delete` | `POST` | Delete an event |
| `playlist_search` | `GET` | Search playlists to connect to the edited event |
| `playlist_link` | `POST` | Connect an existing playlist to the event |
| `playlist_unlink` | `POST` | Disconnect a playlist from the event |
| `playlist_clone` | `POST` | Clone a playlist for the event and connect it |
| `export` | `POST` | Export a selected set of visible events |

## Save contract used by the page adapter

The page adapter posts:

- `event_id` or `eventId` when updating
- `title`
- `eventType`
- `status`
- `description`
- `startUtc`
- `endUtc`
- `allDay`
- `timezoneName`
- `locationLabel`
- `locationAddress`
- `locationLat`
- `locationLng`
- `customerName`
- `customerEmail`
- `customerPhone`
- `priceAmount`
- `currency`
- `category`
- `color`
- `readOnly`
- `notes`
- `logisticsNote`

## Export contract

The export path posts:

- `format`
- `export_name`
- `event_ids` as a JSON array

Export intentionally uses only the currently visible event set returned by the calendar engine.

## Architectural implication for Blazor

The data contract is already component-friendly:

- one host element
- one boot payload
- one event model
- callback-based persistence

That makes the first Blazor version a good fit for a single JS interop component that receives typed event data and delegates persistence back to .NET callbacks.
