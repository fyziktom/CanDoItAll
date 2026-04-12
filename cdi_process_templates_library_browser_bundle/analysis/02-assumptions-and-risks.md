# Assumptions And Risks

## Working Assumptions

- The requested `Templates` action replaces `Seed development baseline` in the header and in the empty-state actions.
- `Add to my processes` means importing a full template as a new process definition in the existing definition library.
- `Add to my roles` means appending a role template into the currently open process definition editor.
- `Add to my artefacts` means appending an artifact expectation into a selected step of the currently open process definition editor because no standalone artifact library entity exists today.
- The current BaseLib modal, list-detail, tree, and notification primitives should be reused instead of inventing a new page shell.

## Critical Path Risks

- `MermaidJS.Blazor` adds DI requirements, so both the real app and the component-test bootstrap must be updated or the preview will fail at render time.
- Artifact import can become confusing if the target-step requirement is hidden, so the modal must make the target step explicit and block the action when no valid step exists.
- The requested preview surface mixes markdown, mermaid, json, and tree state in one modal, which can become unwieldy unless the right pane is segmented into clear tabs or panels.

## Validation Risks

- Loose component tests are not enough for mermaid rendering because the real proof must confirm script-backed rendering in a browser.
- Notification z-index can look correct in markup but still be occluded visually, so browser screenshots must capture a toast over the open modal.
- Json and markdown previews can silently render empty states if the sidecar path resolution is wrong, so preview tests must cover both process and resource items.

## Reopen Triggers

- Reopen if the modal cannot import an artifact without losing the target-step context.
- Reopen if the mermaid panel renders raw text or an empty container in the browser.
- Reopen if the notification appears behind the modal overlay.
- Reopen if role import from a process preview only works through the role category and not directly from the process preview itself.
