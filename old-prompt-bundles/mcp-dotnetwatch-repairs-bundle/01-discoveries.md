# Discoveries

## What happened

- Codex calls to `candoitall_dotnetwatch` failed with `Transport closed`.
- The repo-local backend registration file still pointed to a live detached backend process.
- The backend HTTP ping was still healthy when called with the stored auth token.
- The Codex config launched the MCP stdio proxy from a shadow copy at:
  - `C:\repositories\CanDoItAll\.artifacts\mcp-server-shadow\bin\CanDoItAll.Mcp.DotNetWatch\debug\CanDoItAll.Mcp.DotNetWatch.dll`
- The current repo build output existed at:
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.DotNetWatch\bin\Debug\net10.0\CanDoItAll.Mcp.DotNetWatch.dll`
- The shadow copy and the repo build output were not aligned in size or timestamp.
- The first wrapper implementation used `SHA256.HashData`, which is not available in the PowerShell runtime Codex launches here.
- `BackendToolInvoker` still used the default `HttpClient.Timeout` of 100 seconds, so long-lived tools like `candoitall_app_wait` could fail before their own timeout contract.
- Managed app sessions wrote `--artifacts-path` to a unique session-id folder, which forced expensive cold builds even for repeat starts of the same web project.

## What that means

- The live detached backend can be healthy while the stdio MCP host seen by Codex is stale or dead.
- A static shadow artifact path without automatic refresh is an architectural weak point.
- Wrapper scripts must target the actual host PowerShell/.NET runtime, not just the .NET API surface used by the repo projects.
- Transport-level timeouts can silently override MCP tool contracts if the backend proxy is not aligned with tool wait semantics.
- Session-scoped artifacts are clean but too expensive for a large Blazor solution when agents repeatedly stop/start the same app.
- Current failure feedback is too thin. `Transport closed` at the agent layer hides whether the cause was:
  - shadow proxy drift
  - invalid settings
  - backend launch timeout
  - registration mismatch
  - process crash after startup

## Existing strengths we should preserve

- Detached backend architecture is already in place and avoids losing live watch sessions when the MCP stdio host restarts.
- Integration tests already cover core runtime and backend behavior.
- Backend identity already includes:
  - workspace root
  - settings hash
  - binary version marker
- Manager aggregation already cleans stale backend catalog entries when it can inspect them.

## Main gap to fix

The Codex config depends on a shadow build that must currently be refreshed manually. That makes reliability depend on human memory. The server also lacks persistent bootstrap diagnostics when stdio startup fails before an agent can call any tools.

There is a second reliability layer beneath bootstrap: once the server is running, it still needs to honor long waits and reuse build outputs efficiently. Otherwise the MCP path remains technically alive but operationally frustrating.
