# Validation Rules

These are strict pass conditions. If any one of them fails, the task is not complete.

## Automated validation

1. `dotnet build CanDoItAll.slnx` must pass.
2. `tests/CanDoItAll.Mcp.DotNetWatch.Tests` must pass.
3. `tests/CanDoItAll.Mcp.DotNetWatch.IntegrationTests` must pass, including a new re-instancing continuity test.
4. No backend logging noise may corrupt MCP stdio protocol traffic during proxy-mode tests.

## Backend continuity validation

1. Start the MCP server in stdio mode through a real client/harness.
2. Call `candoitall_app_start`.
3. Wait for healthy.
4. Capture:
   - session ID
   - watcher PID
   - runtime PID
5. Dispose the first client so the stdio process goes away.
6. Start a second real client/harness.
7. Call `candoitall_app_status`.
8. Pass only if:
   - the app is still running
   - the session ID is the same backend-owned session
   - the watcher PID is unchanged
   - the runtime PID is still valid or has a valid watch-driven replacement with the same session continuity
   - the backend daemon PID is unchanged

## Browser validation

1. Open the live project structure page.
2. Confirm the lower section is visually improved.
3. Confirm the outline area can scroll instead of pushing the whole page down indefinitely.
4. Capture evidence with at least one screenshot.

## Watch propagation validation

1. After confirming backend continuity, make a mostly style-only edit on the project structure page or directly related shared workbench styling.
2. Do not restart the app manually.
3. Wait for watch to settle through the MCP surface.
4. Refresh or inspect the live browser.
5. Pass only if the visual change appears while the same backend-owned session remains active.

## Generic workspace validation

1. Start the same server binary against a second C# workspace using a workspace-local settings file.
2. Use a workspace that does not rely on the CanDoItAll-specific health endpoint.
3. Wait for `Ready` or `WatchSettled` through the MCP/backend surface.
4. Make a style-only change and confirm:
   - the backend daemon PID is unchanged after stdio re-instancing
   - the same session ID remains active
   - the watch session returns to `pendingChange=false`
   - the browser can observe the style change and the revert
5. Prefer an explicit sample workspace for regression coverage:
   - `C:\repositories\pveinvoicing\PVEInvoicing`

## Anti-regression validation

Fail the task if any of these happen during validation:

1. the second stdio proxy starts a fresh backend instead of reusing the existing one
2. the second stdio proxy cannot see the app started by the first one
3. the app is stopped just because the first stdio process exited
4. the layout fix requires a full manual stop/start outside the backend-owned watch loop
5. duplicate watch sessions appear for the same launch shape without an explicit conflict/replacement decision
