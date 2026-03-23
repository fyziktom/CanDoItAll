# Validation Rules

## Strict pass conditions
1. Manager aggregation
   - Opening the CanDoItAll backend manager must show both live backends.
   - Each backend card must show its workspace root and live session inventory.
2. Manager controls
   - The UI must expose stop and force stop for a live session.
   - The UI must expose a rebuild-oriented control for a watch session or backend.
   - At least one action must be executed successfully from the manager UI during validation.
3. Watch confirmation handling
   - Implementation and docs must demonstrate that rude edits do not block on confirmation.
4. Backend persistence
   - After a fresh stdio re-instance, the same backend registration and same app session must remain usable.
5. Generic behavior
   - `CanDoItAll` validation must pass.
   - `pveinvoicing` validation must pass.
6. Log reduction
   - Real sample reduction must show a meaningful decrease in returned agent-facing log volume.
   - Final documentation must include the reduction method and concrete savings numbers.
7. Static asset validation fidelity
   - If a CSS change is served by the running app but not reflected in the browser, validation must distinguish browser caching from backend/watch failure.
   - Cache-busted stylesheet reload is allowed as a validation aid when proving that the running app is serving the updated asset.

## Failure conditions
1. The manager page still shows only the current backend.
2. Remote manager actions cannot be executed.
3. The backend app restarts only because the MCP server was re-instanced.
4. Log reduction hides build failures or runtime exceptions.
5. The implementation only works for `CanDoItAll` and not for `pveinvoicing`.
6. Validation reports a hot-reload failure when the actual problem is only stale browser-side static asset caching.
