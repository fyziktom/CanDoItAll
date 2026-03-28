# Normalized Requirements

- `R001` The validation must prove this Codex session can reach the live CanDoItAll project-structure MCP and read existing project data.
- `R002` The supplied XMind source must be preserved in the bundle and converted into a valid import payload that the MCP can consume.
- `R003` The validation must create a dedicated validation workspace under `CanDoItAll Main` so defects and evidence are isolated and reviewable.
- `R004` The validation must exercise project creation or update, subproject linking, structure reads, node creation, and XMind import on the live MCP.
- `R005` The transferred structure must use richer project-structure semantics where the source meaning clearly warrants them, including subprojects for larger branches.
- `R006` The validation must read back the created structure after mutation and confirm the created hierarchy is visible through both MCP data and the browser UI.
- `R007` The validation must capture checklist and analytics evidence for the executed operations, or explicitly record any missing MCP or API surface that prevents that proof.
- `R008` Any broken behavior, missing capability, or weak proof discovered during the run must be captured into `project-structure-mcp-validation-1` instead of being silently skipped.
- `R009` The bundle must remain synchronized with execution status, proof, and raw-note closure throughout the workflow.
