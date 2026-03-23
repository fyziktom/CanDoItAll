# MCP DotNetWatch Repairs Bundle

This bundle is the execution contract for hardening `CanDoItAll.Mcp.DotNetWatch` so Codex, Playwright, and the detached watch backend cooperate reliably.

Goals:
- make `Transport closed` a repairable startup condition, not a dead end
- keep the shadow-host binary aligned with the current repo source automatically
- preserve clean MCP stdio behavior while still producing persistent bootstrap diagnostics
- make the backend lifecycle and catalog resilient to stale state
- remove hidden transport-layer timeouts that break long waits
- reuse managed app build artifacts so repeated `dotnet watch` sessions warm-start faster
- reduce agent context waste by making the MCP server trustworthy again

Observed trigger for this bundle:
- the Codex MCP tool returned `Transport closed`
- the detached backend registration in `.mcp-state/backend/registration.json` was still alive and reachable
- the shadow server binary under `.artifacts/mcp-server-shadow/...` was older and smaller than the current repo build output
- the wrapper itself had a PowerShell runtime compatibility bug (`SHA256.HashData` not available)
- long `app_wait` calls were capped by the backend proxy `HttpClient.Timeout`
- app sessions always used fresh session-id artifacts folders, forcing cold `dotnet watch` builds

Execution order:
1. validate and document the real failure modes
2. add a self-repairing MCP launcher wrapper
3. add persistent bootstrap diagnostics inside the server
4. add regression coverage for wrapper-based startup
5. validate wrapper, stdio startup, backend registration, and Playwright-friendly app flow

Files:
- `01-discoveries.md`
- `02-failure-scenarios.md`
- `03-architecture-changes.md`
- `04-implementation-plan.md`
- `05-checklists.md`
- `06-validation-criteria.md`
- `07-role-prompts.md`
- `08-qa-review.md`
