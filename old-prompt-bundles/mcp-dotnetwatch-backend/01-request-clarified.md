# Clarified Request

## Problem statement

`CanDoItAll.Mcp.DotNetWatch` works when a single MCP server instance owns the whole lifecycle. In practice, Codex and GitHub Copilot often re-create MCP server processes during one work session. When that happens, the new MCP instance no longer owns the already-running `dotnet watch` process, so the tool surface becomes unreliable and agents start launching more server instances and more watch attempts.

## Required outcome

Build a persistent backend layer for `CanDoItAll.Mcp.DotNetWatch` so the first MCP server instance starts or connects to a long-lived local backend service, and every later MCP server instance reconnects to that same backend for the same workspace/settings.

## Functional goals

1. The backend service, not the stdio MCP process, must own `dotnet watch`, `dotnet run`, build, and test execution.
2. A later MCP server instance must reconnect to the same backend instead of starting a fresh runtime controller.
3. The backend must manage more than one watched app session at a time when the sessions target different projects or otherwise non-conflicting launch shapes.
4. Starting the same project with the same launch shape should reuse the already-running session by default.
5. Stopping a watched app must be explicit and uncommon. The happy path is keep running, reconnect, inspect status, keep editing.
6. The backend must provide a simple local UI for visibility and manual inspection. `CanDoItAll.Manager` can be used as the UI/template reference.

## Behavioral constraints

1. Re-instancing the MCP server must not stop the app.
2. Losing the MCP process must not lose the backend-owned app/watch session.
3. The system must detect stale backend registrations and recover cleanly.
4. Concurrent MCP server startups must not race into spawning multiple backend daemons for the same workspace/settings.
5. Tool descriptions and defaults should strongly bias agents toward reuse instead of restart/stop.

## Validation target

Validation must prove the new backend works in a real edit loop:

1. Start the app through the MCP server backed by the new persistent backend.
2. Open the project structure page.
3. Fix the bad bottom layout on that page:
   - improve the column/card layout in the lower section
   - add scrolling so large outlines do not endlessly extend the page
4. Re-instance the MCP server.
5. Confirm the same app session is still alive after re-instancing.
6. Make a small style-only or mostly style-only change that `dotnet watch` can propagate quickly.
7. Confirm the live page reflects the change while the same backend-owned watch session continues running.
8. Query status after re-instancing and confirm it points to the same running app/watch session.

## Non-goals

1. Replacing MCP stdio transport with a different agent-facing transport.
2. Turning the backend UI into a full product surface. It only needs to be useful for diagnostics and manual control.
3. Solving every possible multi-workspace orchestration concern in one pass. The minimum target is one backend per workspace/settings identity.
