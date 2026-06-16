# SB17 Story Coverage

| Story | Coverage | Proof |
| --- | --- | --- |
| US-018 Definition canvas renders graph, routes, selection, and node context. | Solved. Canvas projection DTOs include typed node kinds, ports, edges, selection, viewport, and route/role/artifact/subprocess concepts; UI renders nodes/edges and selection panel. | `Canvas_projection_reads_steps_routes_roles_artifacts_and_toolbox`, `Canvas_renders_nodes_toolbox_selection_and_route_edges`, `processes-definition-canvas.png`. |
| US-019 Toolbox actions and recomposition update the definition canvas through commands. | Solved. Toolbox and recompose buttons emit typed commands with expected version tokens; application returns accepted/rejected receipts and updated projections. | `Canvas_toolbox_commands_add_elements_and_recompose`, `Canvas_rejects_stale_version_tokens`, `Canvas_toolbox_action_uses_typed_canvas_command_boundary`, `Canvas_recompose_uses_typed_canvas_command_boundary`, Playwright add/recompose receipt assertions. |

## Notes

SB17 intentionally does not implement detailed step editor forms. SB18 owns those forms and will consume the selected element DTO shape and canvas command receipts introduced here.
