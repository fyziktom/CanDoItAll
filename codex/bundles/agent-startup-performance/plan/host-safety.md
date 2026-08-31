# Host Safety And Rollback

Preparation only; all operations below are deferred until execution is authorized.

## Preflight

Identify current source/binaries/image/process, exact port owner, app profile, workspace root, provider route and active runs. Reuse current hosts. Never infer identity solely from a port or an old artifact. Coordinate safe idle replacement; no running user operation may be interrupted. Record rollback and data backup/recovery availability without printing environment secrets.

## Native5032

Use its existing managed dotnetwatch session/cursor if present and follow the watch/Playwright skill. If it is a normal Release process, identify the exact PID/profile/assembly and reuse its repository-owned launch procedure; no second watcher and no generic dotnet/process kill. Preserve data/config/port/profile. Never claim a new isolated fixture app is5032 acceptance.

## Docker5214

Only the existing client `candoitall-shared-providers-manual-client-a-1` is a deployment target. Preserve its exact mounts, environment/secrets, network aliases, resource limits, user1654:1654, read-only root and loopback5214 binding. Retain the stopped rollback container and never run both against the same data. Preserve the Docker named-context bin/obj exclusion that fixed CSS.

Central publisher5210, PostgreSQL, upstream fixtures and unrelated8080 remain unchanged. Do not migrate the data bind mount or drop/reset volumes.

Existing `repo://codex/bundles/providers-shared-premerge-review/scripts/Restart-LiveTestInstances.ps1` replaces BOTH5210and5214: **do not run it unchanged**. The prior client-only script under ignored artifacts is an example, not durable authority; inspect and validate exact targets before any future adaptation.

## Test Harness Boundaries

Do not run the entire shared-provider external acceptance suite against these live instances; it issues tokens and edits/deletes provider/agent fixtures. Reuse inspected selectors/scenarios only. `PlaywrightAppFixture` may auto-launch another app on failed readiness; live proof must attach explicitly via MCP and fail on mismatch, not invoke that fallback.

Fault injection, corrupt journals/catalogs, permission/link swaps, concurrent synthetic writers, real cancellation and deterministic provider failures belong in disposable test roots/processes. Never apply them to live agent data. No outgoing messages, public publishing or destructive tools are needed for proof.

## Rollback

On a deployment or behavior regression, stop new validation requests, preserve evidence and restore only the affected known-good app binary/container through the validated target-specific procedure. Do not overwrite live workspace data with stale snapshots as a casual rollback. A suspected data inconsistency blocks further mutation until the established recovery contract is followed.
