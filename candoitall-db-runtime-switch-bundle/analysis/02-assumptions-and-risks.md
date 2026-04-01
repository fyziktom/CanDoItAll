# Assumptions And Risks

## Working Assumptions

- v1 may treat the active database as an **app-wide** selection because the product is currently described and implemented as a local single-user workspace app.
- A forced navigation/circuit reload to a safe route is an acceptable way to guarantee data and service reload after a database switch.
- SQLite support should become first-class through profile modeling and UI flows even though the repo already has partial startup-time SQLite support.
- Snapshot/versioning should capture database data plus profile-scoped storage, while app-level manager artifacts may remain outside the profile.
- IPFS should be implemented as a snapshot transport or cached SQLite source path, not as a live mutable database engine.
- Explicit `Database:Provider` / `Database:ConnectionString` overrides must keep working for tests and headless startup, even if that temporarily locks the runtime selector.

## Critical Path Risks

- **Migration risk:** existing SQLite databases were created via `EnsureCreatedAsync()` and raw SQL initializers, so a bad baseline strategy could corrupt or strand legacy user data.
- **Storage risk:** current managed-file serving is rooted at startup and would silently serve the wrong profile's files if not replaced with request-time resolution.
- **Reload risk:** because the active database is app-wide, switching must fan out to all open browser tabs/circuits; proving only the current tab would be weak proof.
- **Credential risk:** PostgreSQL and IPFS endpoints need credentials or connection metadata outside the selected DB, so control-plane encryption and persisted key-ring setup are foundational.
- **Clone risk:** clone/snapshot flows are incomplete if they copy DB rows but miss storage files or lose provider-specific data fidelity.
- **Proof risk:** the user explicitly warned about fake validation, so every subbundle must stop on missing commands, missing screenshots, or skipped downstream dependency checks.

## Validation Risks

- The current preparation environment does not contain the .NET SDK, so all runtime proof is planned, not executed, during bundle preparation.
- PostgreSQL E2E proof will depend on either a running local service or `docker compose up -d postgres`; if that environment is missing, the subbundle must close as blocked, not complete.
- Playwright proof depends on Chromium/browser tooling and a runnable app instance; missing browser setup is a real blocker.
- IPFS live-transport proof depends on a reachable node/API contract; unit/integration tests with a fake HTTP server are mandatory even if a real node is not available.
- If the execution agent changes the UI but omits screenshot review questions and large-screen browser checks, the proof is not strong enough to pass.

## Reopen Triggers

- Reopen subbundle 02 if control-plane data or secrets still depend on the selected DB.
- Reopen subbundle 03 if any service still reads provider/connection data only from startup configuration after the switchable factory is introduced.
- Reopen subbundle 04 if normal-path startup or test harnesses still rely on `EnsureCreatedAsync()` instead of migration/bootstrap logic.
- Reopen subbundle 05 if `/managed-files` is still bound to a fixed `PhysicalFileProvider`.
- Reopen subbundle 06 if a switch from one profile to another can still restore stale workbench state or crash on missing artifact routes.
- Reopen subbundle 07 if the UI can activate a switch without surfacing the active database clearly, or without guarding unsafe/dirty-state transitions.
- Reopen subbundle 08 if clone/snapshot tests omit profile-scoped storage, if PostgreSQL proof is missing, or if the execution report marks blocked proof as passed.
