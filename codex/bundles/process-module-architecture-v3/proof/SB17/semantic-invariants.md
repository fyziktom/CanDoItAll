# SB17 Semantic Invariants

## Invariants

| ID | Invariant | Proof |
| --- | --- | --- |
| SB17-INV-001 | Canvas UI renders from `ProcessDefinitionCanvasEditorProjection` DTOs, not from runtime entities, persistence models, or DOM-derived state. | `projection-boundary-scan.txt`, `source-assertions.txt`, component test `Canvas_renders_nodes_toolbox_selection_and_route_edges`. |
| SB17-INV-002 | Selection is explicit and testable for step, branch route, role, artifact, and subprocess-capable nodes. | `ProcessDefinitionCanvasPanel.razor` source assertions and component artifact-selection assertion. |
| SB17-INV-003 | Toolbox actions cross the UI/application boundary as `ProcessDefinitionCanvasCommand` values with typed action and selected-node keys. | Component test `Canvas_toolbox_action_uses_typed_canvas_command_boundary`; Playwright toolbox click and accepted receipt. |
| SB17-INV-004 | Recomposition is deterministic projection behavior and does not rely on UI canvas coordinates as source of truth. | Unit test `Canvas_toolbox_commands_add_elements_and_recompose`; component test `Canvas_recompose_uses_typed_canvas_command_boundary`; Playwright recomposed receipt. |
| SB17-INV-005 | Stale canvas submissions fail predictably instead of silently overwriting newer projection state. | Unit test `Canvas_rejects_stale_version_tokens`. |

## Shallow-Pass Trap

A shallow implementation could render a static diagram or test IDs and still pass basic visual checks while bypassing typed command routing, stale-version rejection, or projection-only boundaries. SB17 rejects that trap by testing the command payload, returned receipt, stale-token rejection, and Playwright action flow rather than only checking for non-empty markup.

## Adversarial Negative Proof

`Canvas_rejects_stale_version_tokens` submits a command with an outdated `ProcessDefinitionCanvasVersionToken`; the service returns `Rejected` and keeps the projection version unchanged. This catches the unsafe fallback where UI edits would silently apply against stale layout state.

## Semantic Positive Proof

The unit projection tests load a realistic template canvas with step, branch route, role, artifact expectation, and toolbox actions. Component tests select rendered nodes and assert command payloads. Playwright loads the shell, selects a decision step, adds an implementation step from the toolbox, recomposes, and captures `processes-definition-canvas.png`.

## Production Behavior Artifact Matrix

| Production artifact | Producer | Consumer | Lifecycle | Proof citation |
| --- | --- | --- | --- | --- |
| `ProcessDefinitionCanvasEditorProjection` | Application canvas projection service | Process shell and canvas panel | Created from template canvas defaults and refreshed after commands. | `test-unit-canvas-sb17.txt`, `test-components-process-shell-sb17.txt`. |
| `ProcessDefinitionCanvasCommand` | Canvas panel | Projection client and application service | Created from UI selection/toolbox actions with typed keys and expected version token. | `source-assertions.txt`, component command-boundary tests. |
| `ProcessDefinitionCanvasCommandReceipt` | Application canvas projection service | Shell state and receipt UI | Accepted/rejected command outcome is shown and carried with returned projection. | Playwright receipt assertions and component fake-client assertions. |
| Template canvas authoring defaults | Template loader/canvas summary mapper | Canvas projection service | Canonical template JSON fields and step-template toolbox JSON feed authoring projections. | `test-unit-canvas-sb17.txt`. |
