# Current State

- The source CRM/HR bundle already exists as a completed B01-B13 initiative with dependency ordering, implementation notes, and traceability.
- The local CanDoItAll app exposes a project-structure MCP API under `/api/project-structure-mcp` plus dev database endpoints that can create and switch managed SQLite profiles.
- The project-structure settings UI can create an enabled agent profile and generate a token in a fresh database, which is necessary because agent authorization is enforced for the MCP API.
- The public project-structure API supports project creation, subproject connections, node create/update/reparent/move/delete, metadata/status/progress/priority/marker changes, imports, checklists, dependencies, leases, and analytics.
- The public project-structure API does not expose a direct arbitrary link-mutation endpoint, so user-authored dependency proof must currently rely on the import pipeline that converts Mermaid flowchart edges into `DependsOn` links.
- The structure catalog already supports typed participant nodes with subtype `ai-agent` and work-item metadata with an assignee reference, which is enough to model AI ownership explicitly.
- The canvas route for proof is `/projects/{projectId}/structure`.
