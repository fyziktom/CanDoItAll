# Architecture Changes

## Change 1: replace direct shadow launch with a self-repairing wrapper

Current:
- Codex launches `dotnet <shadow-dll> --settings ...`
- shadow freshness depends on a manual build comment in `config.toml`

Target:
- Codex launches a PowerShell wrapper
- wrapper decides whether shadow artifacts need refresh
- wrapper runs incremental `dotnet build` into versioned folders under `.artifacts/mcp-server-shadow/builds`
- wrapper launches the shadow DLL only after the repair step succeeds
- wrapper writes bootstrap events to a repo-local log file

Why it matters:
- removes manual shadow refresh from the critical path
- avoids rebuilding into DLL paths that are already loaded by a live shadow host
- keeps Codex bound to current code, not forgotten artifacts
- turns "tool is dead" into "tool self-repaired or failed with evidence"

## Change 2: add persistent bootstrap diagnostics inside the stdio host

Current:
- startup exceptions can reach stderr, but there is no guaranteed persistent diagnostic trail

Target:
- stdio and backend startup exceptions append diagnostic records to a file under `.mcp-state/logs`
- timeout messages include:
  - registration path
  - launch lock path
  - workspace root
  - server assembly path
  - last known registration identity

Why it matters:
- `Transport closed` becomes debuggable after the fact
- agents can inspect bootstrap failures without guessing from memory

## Change 3: validate the wrapper path as a first-class startup contract

Current:
- integration tests start the server DLL directly
- Codex uses a different startup path than the tests

Target:
- add integration coverage for the wrapper path
- prove the wrapper can launch the server and answer `workspace_info`

Why it matters:
- closes the gap between "tests pass" and "Codex tool still died"

## Change 4: strengthen default agent policy

Current:
- config tells the agent to prefer the MCP server
- config still allows a fail-safe skip when the tool fails

Target:
- config explicitly says a broken `candoitall_dotnetwatch` transport becomes a repair task
- only after repair and validation should the agent continue with feature work

Why it matters:
- makes the desired behavior explicit and repeatable across sessions

## Change 5: remove hidden backend proxy timeouts

Current:
- backend tool calls inherit the default `HttpClient.Timeout` of 100 seconds
- long waits can fail before the requested MCP timeout expires

Target:
- backend proxy requests rely on MCP/request cancellation, not a separate 100-second client timeout

Why it matters:
- `app_wait` and `operation_wait` honor the timeout the caller requested
- transport behavior no longer overrides tool contracts

## Change 6: reuse app build artifacts by template instead of by session id

Current:
- every managed app session gets a fresh `--artifacts-path`
- repeated starts of the same large app force cold builds

Target:
- derive a stable artifacts cache root from the app template
- reuse that cache across compatible sessions

Why it matters:
- repeated `dotnet watch` starts are materially faster
- agent-driven stop/start loops waste less time and generate fewer avoidable timeouts
