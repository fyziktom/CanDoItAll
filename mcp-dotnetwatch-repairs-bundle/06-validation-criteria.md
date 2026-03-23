# Validation Criteria

## Startup

- launching through the wrapper starts a functioning MCP server
- `candoitall_workspace_info` succeeds through the wrapper path
- the wrapper can recreate shadow artifacts if they are missing
- a stale shadow copy is rebuilt before launch
- the wrapper remains compatible with the PowerShell runtime Codex actually uses

## Diagnostics

- startup failures create a persistent bootstrap log entry under `.mcp-state/logs`
- backend startup timeout messages include enough context to repair the issue without guessing
- stderr remains actionable and does not pollute stdout

## Runtime and cooperation

- the detached backend remains reachable after wrapper startup
- backend proxy calls do not fail at 100 seconds when the requested tool timeout is longer
- managed app start and managed wait still work after wrapper launch
- repeat starts of the same app template reuse a stable artifacts cache root
- the path remains compatible with Playwright-driven validation flows

## Policy

- `config.toml` clearly states that a broken `candoitall_dotnetwatch` transport must be repaired before continuing normal feature work
- the default app wait timeout is large enough for a cold CanDoItAll web app start on this workstation
