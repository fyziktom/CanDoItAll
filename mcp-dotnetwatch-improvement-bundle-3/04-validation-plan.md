# Validation Plan

## Functional validation

- build the tray project
- run focused integration tests for wrapper and backend persistence
- launch the tray app and confirm:
  - icon appears
  - manager page opens
  - log folder opens
  - backend recovery action works

## Failure-state validation

- simulate no-backend state
- simulate duplicate backend records for the same workspace
- confirm tray state and notification behavior

## Resetup validation

- run the resetup script
- confirm:
  - dotnetwatch wrapper path is configured
  - tray artifact is published
  - repo-managed skill is copied to `%USERPROFILE%\.codex\skills`
  - startup shortcut is created or updated if enabled

## Performance validation

- rerun the simple-edit hot-reload benchmark with tray inactive
- rerun the same benchmark with tray active
- compare browser-visible timing to bundle-2 results

Target:

- no meaningful regression from the bundle-2 baseline of roughly `8-12s` visible updates on the validated simple edits
