# MCP DotNetWatch Backend

This folder is the execution package for turning `CanDoItAll.Mcp.DotNetWatch` from a per-process in-memory runner into a persistent backend service that survives MCP server re-instancing.

Contents:

- `01-request-clarified.md`: cleaned-up version of the user request with explicit goals and constraints.
- `02-architecture-analysis.md`: current-state analysis, target architecture, and missing concerns that must be handled.
- `03-implementation-plan.md`: phased implementation plan with file-level targets.
- `04-checklists.md`: implementation, regression, and release checklists.
- `05-agent-prompts.md`: step-by-step prompts for executing the work without drifting.
- `06-validation-rules.md`: strict validation procedure and pass/fail conditions.
- `07-validation-evidence.md`: executed validation notes, observed results, and follow-up gaps.

Definition of done:

1. A background backend service owns live app/watch processes and survives MCP server restarts.
2. Re-instancing the MCP server reconnects to the same backend and the same live app session.
3. Multiple project watches can coexist when they do not conflict.
4. Stopping a watch is explicit and not the default happy path.
5. A lightweight manager UI exists for the backend service.
6. The project structure page layout issue is fixed and validated through a live watch session that survives MCP re-instancing.
7. The same server binary can be pointed at another C# workspace through a workspace-local settings file without weakening the existing workspace boundary.
