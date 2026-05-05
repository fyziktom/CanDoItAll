# Normalized Requirements

| Id | Requirement | Acceptance |
| --- | --- | --- |
| R-001 | Preserve ProjectStructure and Processes MCP tool guidance before removal. | Bundle analysis and new API skills include the typed-node, Mermaid/file asset, lease, analytics, template detail, baseline scenario, runtime control, and HR matching guidance. |
| R-002 | Close API gaps discovered from the MCP surface review. | `/api/processes` exposes baseline scenario listing and detailed template payload with compatibility notes/supporting files. |
| R-003 | Remove the two MCP server projects and their dedicated tests from the solution and code tree. | Solution no longer references those projects; source/test directories are removed; active tests no longer reference those assemblies. |
| R-004 | Remove install/config support for the two MCPs. | Reinstall scripts, separate install scripts, VS Code MCP config, DotNetWatch test settings, and local Codex `config.toml` no longer contain those MCP entries. |
| R-005 | Remove MCP-specific Settings UI. | Settings page no longer exposes the Project Structure MCP tab or panel; related component tests are removed or updated. |
| R-006 | Add repo-managed API skills and install them locally. | Skills for project-structure API, processes API, and agents API exist under `codex\skills`, are copied to `C:\Users\lucys\.codex\skills`, and are synced by existing skill install flow. |
| R-007 | Validate build/test impact and architecture direction. | Targeted tests/build pass or blockers are documented; execution report contains architecture review and raw-note closure. |
