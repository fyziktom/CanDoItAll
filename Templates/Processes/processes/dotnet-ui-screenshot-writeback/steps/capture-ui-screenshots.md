# Capture UI screenshots when required

For UI targets, start or reuse the app using the recorded Run app node, navigate to each concrete route, capture screenshots, collect console messages, and write screenshot files as current-run managed artifacts. Capture browser state with `browser_snapshot` using a durable `.yml` filename, capture screenshots with durable `.png` filenames, and capture console messages with durable `.log` filenames under the current process-run artifact root. For no-UI targets, do not launch a browser; write an explicit no-UI screenshot receipt instead. Do not create project-structure nodes or image assets here; the storage step owns Screenshots writeback.

Prefer the recorded Run app node for launch. If the manifest marks command-node references as missing but cites verified upstream QA/runtime browser evidence, use the cited base URL as the launch target and capture fresh current-run artifacts from that URL. Treat prior screenshots as source evidence only; do not submit screenshots from another process run as this step's output. If the verified URL is unreachable and no launch command is available, block with the exact missing launch reference, URL, route, and browser error.

## Contract
- Inputs: Screenshot target manifest, Run app command node reference when available, or verified upstream runtime URL/browser evidence from the manifest.
- Outputs: Screenshot files and browser evidence for UI targets, or explicit no-UI receipt for non-UI targets.
- Evidence: Current-run screenshots, route URLs, console state, runtime command references or cited degraded runtime evidence, cleanup receipt, or no-UI evidence.
- Operation target scope: `ExternalProductTargetReadOnly`
