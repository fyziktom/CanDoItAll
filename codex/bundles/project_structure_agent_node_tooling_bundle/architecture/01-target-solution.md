# Target Solution

## End State

- Project-structure page title is computed from loaded project state through a local helper, using `PS - ` plus a truncated project name.
- Contextual project-structure chat receives `SelectedNodeIds` from the page and injects them into prompt context and invocation metadata.
- Project-structure agent contracts expose a node catalog DTO and selected-node-subproject DTOs so HTTP/internal tools share the same language.
- `ProjectStructureAgentService` exposes node catalog/read guidance built from the authoritative canvas catalog where possible.
- `ProjectWorkbenchCrossModuleMutationService` gains arbitrary selected-node move support by reusing the existing cross-module assignment reconciliation pattern.
- MAF attaches new default internal tools for the catalog and selected-node subproject workflow, and descriptions for node creation/tasks/dependencies become explicit.
- The XLSX workbook records additional generic scenarios and recommendations so not every scenario has to be implemented immediately.

## Boundaries

- Do not recreate the removed ProjectStructure MCP.
- Do not change project-object enum names for typed variants.
- Do not implement every workbook scenario in this bundle.
- Do not move system-managed projection nodes as editable selected nodes.

## Dependency Handling

- Existing `DependsOn` semantics remain: source node depends on target node.
- Internal links where both endpoints move are retained under the target project.
- Links to source-project-only nodes are removed during transfer to avoid invalid cross-project project-structure links.
- Dependency query remains the readback proof for Gantt-facing data.
