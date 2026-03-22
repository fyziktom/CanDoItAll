# Checklists

## Architecture checklist

- `stdio` proxy and backend daemon modes are separate.
- Backend registry includes PID, start time, URL, auth token, settings hash, workspace root, and version marker.
- Backend bootstrap is race-safe.
- Backend bind is loopback-only.
- Backend API requires a shared secret.
- Backend launch is detached from MCP stdio handles.
- Port selection/collision handling is deterministic.

## Runtime checklist

- Backend owns app sessions and operations.
- App sessions survive stdio MCP disposal.
- `app_start` reuses compatible live sessions.
- Multiple project sessions can coexist.
- Conflicting launches are detected deterministically.
- Stop behavior is explicit, not accidental.

## Payload checklist

- Status includes backend-owned session ID.
- Status includes watcher PID and runtime PID.
- Status includes observed URLs.
- Workspace info can surface more than one live app session.
- Operation preemption metadata identifies all affected sessions.

## Manager UI checklist

- Backend dashboard has a health/identity section.
- Backend dashboard lists live app sessions.
- Backend dashboard lists active/recent operations.
- Backend dashboard exposes recent logs or log links.
- Backend dashboard shows enough data to detect duplicate launches.

## Automated test checklist

- Unit tests cover backend registry validation.
- Unit tests cover compatibility reuse logic.
- Integration tests cover cross-proxy session continuity.
- Integration tests cover stale-backend replacement.
- Integration tests cover log/status continuity after proxy re-instancing.
- Integration tests cover multi-session or conflict behavior.
- Integration tests prove MCP stdio output stays clean while backend mode is used.

## Manual validation checklist

- Start app from MCP proxy instance A.
- Confirm healthy session and note session ID, watcher PID, runtime PID.
- Dispose proxy A.
- Start proxy B.
- Call status without restarting the app.
- Confirm the same backend-owned session is still active.
- Confirm no duplicate backend daemon was spawned during proxy B startup.
- Open the project structure page in the browser.
- Apply the layout/scrolling fix.
- Confirm the page improves visually.
- Apply a mostly styling edit after proxy re-instancing.
- Confirm the browser reflects the change while the same app session remains active.

## Release checklist

- `dotnet build CanDoItAll.slnx` passes.
- dotnetwatch unit tests pass.
- dotnetwatch integration tests pass.
- Manual validation artifacts are captured.
- Final notes explain any remaining limitations, especially around multi-session conflict rules.
