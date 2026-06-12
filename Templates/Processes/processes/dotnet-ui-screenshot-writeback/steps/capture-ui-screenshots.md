# Capture UI screenshots when required

For UI targets, start or reuse the app using the recorded Run app node, navigate to each concrete route, capture screenshots, collect console messages, and write screenshot files as current-run managed artifacts. Capture browser state with `browser_snapshot` using a durable `.yml` filename, capture screenshots with durable `.png` filenames, and capture console messages with durable `.log` filenames under the current process-run artifact root. For no-UI targets, do not launch a browser; write an explicit no-UI screenshot receipt instead. Do not create project-structure nodes or image assets here; the storage step owns Screenshots writeback.

## Contract
- Inputs: Screenshot target manifest and Run app command node reference.
- Outputs: Screenshot files and browser evidence for UI targets, or explicit no-UI receipt for non-UI targets.
- Evidence: Screenshots, route URLs, console state, runtime command references, cleanup receipt, or no-UI evidence.
- Operation target scope: `ExternalProductTargetReadOnly`
