# Normalized Requirements

- `R001` Path-backed project-structure nodes must stop rendering the full raw path as plain lead text and instead show a single compact path control.
- `R002` When a path-backed node points at a file, the node must surface the file name prominently on the card instead of spending that space on the full path string.
- `R003` The compact path control must expose the full path on hover, copy the full path on click, and replace the copy icon with a visible success state for about two seconds after the click.
- `R004` Double-clicking a node that does not have preview support must open a centered in-canvas quick-action modal instead of immediately executing the open command.
- `R005` The quick-action modal must use square action buttons and present `Edit` first when the node supports edit, followed by the most probable secondary action for that node type.
- `R006` Secondary quick actions must be explicit per reachable node type and must reuse existing Workbench command semantics, such as `Run PowerShell` for script nodes and `Open Wizard in New Tab` for prompt-related nodes.
- `R007` Node types that reach the non-preview double-click flow but do not support edit must be handled explicitly in the modal and execution report. They must not silently pretend to be editable.
- `R008` The toolbar settings affordance must use settings iconography instead of the literal `cfg` text.
- `R009` The settings overlay must render fully below the toolbar band and remain visibly usable on both maximized and narrower-width layouts.
- `R010` Completion requires focused automated proof plus browser screenshots that demonstrate node-path presentation, non-preview quick actions, and settings-overlay placement.
