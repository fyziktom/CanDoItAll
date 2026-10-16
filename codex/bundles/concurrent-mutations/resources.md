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

For the P02b guard comparison, only this owned container was restarted with
`max_connections=160` to admit 128 physical writers plus observer/maintenance headroom.
Its identity, volume, ownership label, CPU and memory limits stayed unchanged. Docker
reassigned its loopback endpoint to `127.0.0.1:60920`; the ignored ownership record is
authoritative for subsequent commands. The comparative databases were dropped and
their absence verified. Ordinary application and unrelated containers were preserved.

## P02c standalone browser store ownership, declared before launch

`ProjectPendingSubmissionBrowserTests` owns headless Chromium and 1920x1080 browser
contexts. The tests route `https://projects-pending.test` entirely inside Playwright
and serve the exact production IndexedDB module; they do not launch Web or use a
database. The restart case owns a unique temporary browser profile through the test
fixture, closes it before reopening, and deletes only its own resolved profile path
in final cleanup. No ordinary browser profile is used. Nine source cases are planned;
execution and cleanup verification remain pending.

## P02b offline activation process ownership, declared before launch

`DatasetActivationCommandProcessTests` owns two bounded PostgreSQL database leases,
one per fact, and their `CanDoItAllTestEnvironment` roots. Each actual Web executable
launch records its command kind, resolved executable, environment root and subsequently
its PID in that owned root before/after `Process.Start`. Connection credentials stay
in the child environment and are excluded from arguments, records and diagnostic tails.
The test holds a randomly assigned loopback listener while running the offline command
to prove that the command does not start the Web host. Its 90-second process deadline
and `finally` cleanup kill only the recorded child tree, wait for exit, then release
the listener, database and temporary root. These two LiveProcess/HostPlatform facts
first ran with two failures before activation preparation. Both tests completed their
cleanup: the isolated fixture contained no nonadministrative databases and no matching
offline activation child process remained after execution. Their repaired command
composition requires another run; no passing process proof is claimed.

## P02c native browser recovery ownership, declared before launch

`ProjectsPendingRecoveryBrowserTests.Native_editor_reopens_the_same_durable_operation_after_browser_acceptance_persistence_fails`
owns a `LiveUiHost`, its private environment/database lease, hidden Web child process,
headless Chromium and large-desktop page. It injects failure only into the browser
adapter's accepted-result persistence response, then reloads the page with the actual
module and verifies the exact original operation against the real owner receipt.
The following legacy conflict-browser regression uses its own sequentially disposed
host through the same existing harness. Both runs must unset
`CANDOITALL_PLAYWRIGHT_BASEURL` and select standard Release output. Retain the exact
new screenshot/host-log directories, inspect their PID/port/database identity and
verify cleanup after each test. The ordinary application and browser profile are
not involved. Source cases are prepared; execution is pending.

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
