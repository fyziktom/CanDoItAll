# File Map And Data Contract

## Source file map

| File | Responsibility |
| --- | --- |
| `C:\repositories\zyphonote-web\src\account-playlists.php` | Page shell, server data bootstrapping, DOM containers, form post target, script loading |
| `C:\repositories\zyphonote-web\src\assets\js\zy-playlist-builder-page.js` | Playlist-specific controller for manifest mutation, inspector rendering, outline rendering, library integration, canvas callbacks |
| `C:\repositories\zyphonote-web\src\assets\js\zy-learning-pack-canvas.js` | Generic canvas engine, scene building, rendering, input handling, selection model, view state, emitted actions |
| `C:\repositories\zyphonote-web\src\assets\js\zy-canvas-workbench.js` | Generic floating workbench helpers such as context menu, toolbar, ribbon, and dock |
| `C:\repositories\zyphonote-web\src\assets\js\zy-playlist-review-page.js` | Reuses the generic engine in `browse` mode |
| `C:\repositories\zyphonote-web\src\assets\js\zy-learning-study-page.js` | Reuses the generic engine in `study` mode |
| `C:\repositories\zyphonote-web\src\input.css` | Visual shell, host layout, canvas overlay controls, workbench chrome styles |

## Runtime composition

The playlist builder page loads scripts in this order:

1. `assets/js/zy-tabs.js`
2. `assets/js/zy-canvas-workbench.js`
3. `assets/js/zy-learning-pack-canvas.js`
4. `assets/js/zy-playlist-builder-page.js`

That order is deliberate:

- the page controller depends on the canvas engine
- the canvas engine optionally depends on `window.ZyCanvasWorkbench`

## Important DOM elements

`account-playlists.php` defines these IDs for the builder:

| Element id | Purpose |
| --- | --- |
| `playlist_canvas_host` | Bounding host for the canvas and its overlay chrome |
| `playlist_canvas` | Actual `<canvas>` used by the renderer |
| `playlist_canvas_inspector` | Inspector panel rendered by the page controller |
| `playlist_manifest_editor` | Outline and manifest summary area |
| `playlist_manifest_json` | Hidden textarea used as the saved JSON payload |
| `playlist_score_library` | Score library results container |
| `playlist_score_search` | Score filter input |
| `playlist_add_block_btn` | Add-block command |
| `playlist_focus_first_score_btn` | Quick focus command for the first song |
| `playlist_basics_card` | Overview card targeted by focus helpers |

## Server-to-client page payload

The PHP page writes:

```js
window.ZyPlaylistBuilderPageData = {
  playlistId,
  playlistTitle,
  playlistSubtitle,
  playlistPurpose,
  manifest,
  scoreMap,
  ownedScores
};
```

This payload is the only boot data required by `zy-playlist-builder-page.js`.

## Manifest contract used by the playlist builder

The playlist builder normalizes its manifest into this shape:

```json
{
  "schemaVersion": 1,
  "title": "Playlist title",
  "subtitle": "Playlist subtitle",
  "purpose": "Playlist purpose",
  "sections": [
    {
      "key": "block_1",
      "title": "Block 1",
      "subtitle": "",
      "summary": "",
      "required": true,
      "sortOrder": 10,
      "breakAfterSeconds": 0,
      "items": [
        {
          "key": "score_1",
          "type": "score",
          "title": "Song title",
          "scoreId": "abc",
          "scoreVersionId": "ver_1",
          "scoreFormat": "musicxml-v1",
          "required": true,
          "sortOrder": 10,
          "transposeSemitones": 0,
          "performanceNote": "",
          "estimatedDurationSeconds": 240,
          "estimatedMinutes": 4
        }
      ]
    }
  ]
}
```

### Root fields

- `schemaVersion`: currently hardcoded to `1`
- `title`, `subtitle`, `purpose`: synchronized from playlist metadata
- `sections`: ordered list of blocks

### Section fields

- `key`: stable section identity
- `title`, `subtitle`, `summary`: display and editorial fields
- `required`: currently normalized to `true`
- `sortOrder`: derived ordering field
- `breakAfterSeconds`: break duration after a block
- `items`: ordered block contents

### Item fields used by the current playlist builder

- `key`: stable item identity
- `type`: currently always `score`
- `title`
- `scoreId`
- `scoreVersionId`
- `scoreFormat`
- `required`: currently normalized to `true`
- `sortOrder`
- `transposeSemitones`
- `performanceNote`
- `estimatedDurationSeconds`
- `estimatedMinutes`

## Additional item types supported by the generic engine

The generic canvas engine is broader than the playlist builder page controller. It already knows how to carry metadata for:

- `score`
- `text`
- `checkpoint`
- `image`

The playlist builder page does not expose those types today, but the generic engine includes context actions for them. That is useful for the later generalized Blazor component.

## Canvas UI state contract

The playlist builder persists canvas UI state to `sessionStorage` under a playlist-specific key:

`zy-playlist-builder-canvas:<playlistId>`

The stored state shape comes from `controller.getState()`:

```json
{
  "selectedNodeId": "item:score_1",
  "selectedNodeIds": ["item:score_1", "item:score_2"],
  "collapsedNodeIds": ["section:block_2"],
  "collapsedSectionKeys": ["block_2"],
  "manualPositions": {
    "item:score_1": { "x": 24, "y": -16 }
  },
  "currentItemKey": "score_1",
  "isMaximized": false,
  "zoom": 0.92,
  "panX": 120,
  "panY": 88
}
```

## Save flow

The current page does not save through fetch or an API call. It uses a normal HTML form:

1. The JS controller mutates the in-memory manifest.
2. `syncManifestInput()` rewrites `#playlist_manifest_json`.
3. The user submits the surrounding form.
4. PHP receives `manifest_json`, version metadata, and the expected draft version id.

For the Blazor port, the hidden textarea and form-post flow should be replaced with:

- a strongly typed model in .NET
- explicit save commands
- either HTTP API persistence or server-side application services
