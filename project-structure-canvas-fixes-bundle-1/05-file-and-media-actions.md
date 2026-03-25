# File And Media Actions

This file defines how file and media nodes should behave after the panel moves into the canvas.

## Current Baseline

Already supported:

- image preview
- video preview
- document preview for supported types
- expand preview
- open in new tab

Missing:

- audio preview
- clear preview policy for spreadsheet and office files
- local-open action for files that should open in desktop apps instead of the browser

## Required Capability Matrix

Bundle 1 should implement this behavior model.

### Image

- inline preview: yes
- expanded preview: yes
- open in new tab: yes
- open locally: optional when trusted local path exists

### Video

- inline preview: yes
- expanded preview: yes
- open in new tab: yes
- open locally: optional when trusted local path exists

### Audio

- inline preview: yes, via audio player
- expanded preview: optional, same player in larger shell if useful
- open in new tab: yes
- open locally: optional when trusted local path exists

### PDF and text-like documents

- inline preview: yes when embeddable
- expanded preview: yes
- open in new tab: yes
- open locally: optional when trusted local path exists

### Spreadsheet, presentation, and office-style documents

- inline preview: no by default in bundle 1 unless there is already a stable viewer path
- expanded preview: no by default
- open in new tab: yes if a browser-safe route exists
- open locally: yes when trusted local path exists

### Generic binary file

- inline preview: no
- expanded preview: no
- open in new tab: yes if route exists
- open locally: yes when trusted local path exists

## Open Locally Architecture

Important constraint:

- a browser app cannot safely and reliably open an arbitrary file from the local drive by itself

Bundle 1 must therefore use a trusted bridge.

Acceptable implementation direction:

- structure page requests `Open locally`
- backend resolves the trusted file path from the node or managed file metadata
- backend launches the file via OS shell only for approved roots and file types
- UI receives success or failure and falls back gracefully if launch fails

Not acceptable:

- `file://` links generated into the browser UI
- pretending a browser download is the same as local open
- opening arbitrary host paths without validation

## UI Rules

When local open is supported:

- show `Open locally` before secondary browser actions
- keep `Open in new tab` available when the route is browser-safe
- avoid duplicate buttons that mean almost the same thing

When local open is not supported:

- do not show a dead or misleading action
- keep preview plus new-tab behavior if available
- state clearly that the file can only be previewed or opened in the browser

## Security And Audit

The local-open action should be implemented with these constraints:

- trusted project-relative or managed file roots only
- no arbitrary path injection from raw client input
- logging of open requests
- optional capability flag so the UI can hide the action when the bridge is unavailable

## Bundle Requirement

The bundle is only approved if local file opening is treated as an explicit bridge feature, not left as an unresolved note.
