# Resource ownership and cleanup

## Planned isolated PostgreSQL fixture

Reason: no `CANDOITALL_TESTS_POSTGRES_CONNECTION` is configured and no suitable isolated
test server is running. P01a fault tests and later persistence races require real
PostgreSQL 18. Reuse one fixture with the existing bounded database leases.

Before creation, reserve these exact owned names:

- Container: `candoitall-concurrent-mutations-pg18-20261016`.
- Volume: `candoitall-concurrent-mutations-pg18-20261016-data`.
- Ownership label: `candoitall.task=concurrent-mutations-20261016`.
- Image: existing `postgres:18.6-alpine`.
- Endpoint: random loopback-only published port; record actual binding after creation.
- Credentials/runtime record: ignored `.artifacts/concurrent-mutations/fixture`.
- Network: shared Docker default bridge; not owned and must not be removed.

Preserved unrelated resources observed before creation: ordinary
`candoitall-postgres18-5032` on host 5432 and `hotels-ai-content-studio` on 5534.
Do not stop, mutate or use them as fixtures.

Creation status: created container
`306f31947bed401ec0cd098e421af2aca31a88033f90df97840e33b632ed14f2`, endpoint
`127.0.0.1:50929`; owned named volume above and read-only owned password-file mount.
The ignored ownership record is `.artifacts/concurrent-mutations/fixture/ownership.json`.
Cleanup status: retained for P01a and subsequent database lanes. After the last required database lane, inspect
identity/labels/mounts/ports, remove this container and exclusively owned volume, verify
their absence and released port, then delete only the owned credential/runtime files.
The fixture is retained across related unfinished lanes, not provisioned per phase.

## P02a browser fixture ownership, declared before launch

The single new `ProjectsPortfolioBrowserTests.Two_browser_contexts_preserve_conflicting_drafts_and_require_explicit_current_based_recovery`
case owns one `LiveUiHost` / `PlaywrightAppFixture`, its hidden `dotnet run` Web process,
headless Chromium and two 1920x1080 contexts. The existing harness owns a unique
`CanDoItAllTestEnvironment` with its private workspace and bounded PostgreSQL database
lease on the fixture above. It selects a free loopback port; explicitly unset
`CANDOITALL_PLAYWRIGHT_BASEURL` before execution so it cannot reuse a supplied application.
Set `CANDOITALL_TEST_CONFIGURATION=Release` to use the refreshed standard output.

`await using` closes both contexts/host and the fixture kills only its owned process tree,
then disposes its database/environment. Preserve screenshots/host log only for the exact
new artifact directory under `output/playwright/crm-hr-live-agent-tools`; do not delete
other existing browser evidence.

The case discovered and passed once in 27 seconds. Retained evidence directory:
`output/playwright/crm-hr-live-agent-tools/20261016T154021597Z-b73198bdea1340c2bbb21634922f6a75`.
The server log identifies owned PID 11552, loopback port 51477 and database lease
`cditall_primary_40cf89f382984abd9374b37bafde6e4d` on the owned fixture. After the test,
PID 11552 was absent, no listener remained on 51477 and `pg_database` contained no
matching lease. Both screenshots were visually reviewed. The shared PostgreSQL
container remains intentionally retained for the unfinished bundle's next lanes.
