# Structured Input

## Extracted Notes

- `N001` The selection panel wastes space with repeated information, including the node title appearing more than once.
- `N002` Secondary information such as Artifact, Kind, and Location should move into an advanced accordion section near the end of the panel.
- `N003` Progress, Priority, and Marker must sit on the same row.
- `N004` Node action buttons need a cleaner shared layout: equal-width treatment, icons, and Delete placed last.
- `N005` Every node type needs an edit action that opens a modal on the canvas and lets the user edit node parameters.

## Working Assumption

- The new edit modal will own the node title, subtitle/context, notes, and typed metadata fields for the node type.
- Existing inline status, progress, priority, and marker controls remain the primary quick-edit surface unless implementation reveals a hard requirement to move them into the modal as well.

## Validation Expectations

- prove the inspector layout changes with focused component coverage
- prove the typed edit flow with automated tests against the workbench page and update path
- record residual UI risk honestly if no live browser pass is captured
