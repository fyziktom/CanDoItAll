# Current State

- The left thread card uses `SelectionListItem`, which lets the date/status content overflow inside a narrow rail.
- Thread card previews are taken from `ChatSessionSummaryRecord.LastMessagePreview` and currently occupy too much vertical and horizontal space.
- `ChatWorkspacePanel` renders the selected thread title as static text.
- `AgentSwitchDialog` displays cards but has no text search, tag filter, or favourite affordance.
- `AgentDefinition.Tags` already exists and can carry an internal `favorite` marker without adding a new settings UI.
