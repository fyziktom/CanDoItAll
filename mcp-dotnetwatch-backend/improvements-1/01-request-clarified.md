# Clarified Request

## Goal
Improve the persistent dotnet watch backend so AI agents can re-instance the MCP server without losing app ownership, while also making the backend manager and backend-provided logs more useful for agent-driven development.

## Functional requirements
1. The backend manager must show all live backend daemons on the machine that belong to this MCP server family, not only the current workspace backend.
2. The backend manager must show the active sessions and operations for each discovered backend.
3. The backend manager must provide basic controls for live sessions and backend actions:
   - stop
   - force stop
   - rebuild / restart for watch-owned sessions
   - build trigger for the backend workspace
4. `dotnet watch` must never block on rude-edit confirmation when used through this MCP server.
5. Logs returned to agents from backend tools must be reduced so they contain the highest-value information while avoiding context waste from low-value noise.
6. Raw logs must remain available in persisted files for deep diagnostics.

## Validation requirements
1. Open the manager UI and verify it shows both:
   - `C:\repositories\CanDoItAll`
   - `C:\repositories\pveinvoicing\PVEInvoicing`
2. Verify manager controls exist and work for at least stop / force stop / rebuild-oriented actions.
3. Verify the persistent backend behavior still works when the MCP stdio process is re-instanced.
4. Verify `CanDoItAll` stays alive across MCP re-instancing and responds to status after a small live change.
5. Verify `pveinvoicing` also stays alive across MCP re-instancing and responds to a tiny UI change and revert.
6. Measure log reduction with real samples and estimate:
   - token savings
   - relative context savings
   - approximate number of additional build/start cycles that fit inside a large Codex context window

## Important interpretation decisions
1. "Display all" means a machine-level backend catalog, not only workspace-local registration.
2. "Turn warnings off for AI agent" means filter or summarize them in agent-facing log APIs, not change the project build itself.
3. "Maximum effective information" means:
   - keep errors, failures, exceptions, lifecycle transitions, watch state, and concise summaries
   - summarize repetitive warnings and framework chatter
   - preserve raw persisted logs for escalation
