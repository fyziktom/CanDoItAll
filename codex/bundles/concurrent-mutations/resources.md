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
in final cleanup. No ordinary browser profile is used. Thirteen source cases are planned;
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

The repaired offline graph subsequently passed both cases, with no skips, in
49 seconds (`p02bc-offline-repaired.trx`). Afterward no `dotnet` process with a
dataset-activation command remained and the isolated PostgreSQL server had no
nonadministrative database sessions. The fixture remains owned and retained for
the unfinished native, browser, runtime and capacity lanes.

## P02c native browser recovery ownership, declared before launch

The standalone reference-dialog specimen also owns one hidden Projects.UiSandbox host
on loopback port 60921, launched only from its existing Release output. Its PID/start
record and stdout/stderr are retained under
`.artifacts/concurrent-mutations/validation/p02c-reference-sandbox-*`; the host has no
database dependency. A task-owned Playwright page uses 1920x1080 and saves reviewed
evidence under `output/playwright/concurrent-mutations-reference-sandbox`. Stop exactly
the recorded host and verify port release after this specimen check. No ordinary host
or browser profile is a test input.

The parent completed the real 1920x1080 browser proof and visually reviewed
`after-discard.png`; evidence includes `selected-unconfirmed.png`,
`after-discard.png`, `after-discard.yml` and `console.txt` in that exact directory.
The run exported metadata only, kept all three rows after canceling confirmation,
then removed exactly the selected Unconfirmed row after explicit confirmation.
Prepared/Accepted remained at 2 records / 1024 bytes, simulated writes/seeds stayed
zero, no horizontal overflow appeared and the console had no errors or warnings.
Owned hidden host PID **47876**, start **2026-10-16T18:01:58.7213924Z**, was stopped
after exact PID/start/command verification. The parent verified listener **60921**
was released and closed the task-owned browser page. No database or ordinary
browser/host cleanup was required for this specimen.

`ProjectsPendingRecoveryBrowserTests.Native_editor_reopens_the_same_durable_operation_after_browser_acceptance_persistence_fails`
owns a `LiveUiHost`, its private environment/database lease, hidden Web child process,
headless Chromium and large-desktop page. It injects failure only into the browser
database's accepted-record `put`, using an exact database/store/stage match and a
page-scoped prototype wrapper, then reloads the page with the unchanged actual
module and verifies the exact original operation against the real owner receipt.
The following legacy conflict-browser regression uses its own sequentially disposed
host through the same existing harness. Both runs must unset
`CANDOITALL_PLAYWRIGHT_BASEURL` and select standard Release output. Retain the exact
new screenshot/host-log directories, inspect their PID/port/database identity and
verify cleanup after each test. The ordinary application and browser profile are
not involved. The original failed attempt and passing native repair are recorded
below; standalone browser-store failures remain separate.

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

The next combined browser run retained these exact evidence directories:

- `20261016T190310356Z-3d470bc7d8c64bd587171b41b3e38605`: the passing
  two-context conflict/reconciliation case, owned host PID 496, port 58387,
  lease `cditall_primary_50138caffc874aecb0f04db6d9d34288`.
- `20261016T190349180Z-cd798e155fb04d04a36ef4e017868905`: the failed
  injected-persistence recovery case, owned host PID 35072, port 52528,
  lease `cditall_primary_936b69a145e24611b7071b55034ee3c9`.

All three screenshots were visually inspected. After execution the parent
verified both PIDs and listeners were absent and the isolated PostgreSQL fixture
contained no nonadministrative databases. The native test's logical module URL
did not match the fingerprinted static asset, so its failure injection did not
run. Repairing the route requires a new explicit interception/invocation proof;
the original failure remains retained.

The repaired native recovery case passed in `p02bc-browser-repair.trx` in
27.8702479 seconds. The combined failed-case selection was 11 passed / 3 failed /
0 skipped of 14; the remaining failures belong to the standalone store class.
Exact native evidence directory:
`output/playwright/crm-hr-live-agent-tools/20261016T191351755Z-9b1b49ad04454a6283bb76055446ee9d`.
Its `projects-durable-historical-recovery.png` was visually reviewed and `server.log`
confirms the injected accepted-write failure followed by recovery in a new circuit.
The owned host was PID **29376**, listener **58128**, with database lease
`cditall_primary_4d752cae1efe42118d68f971ed267557` on the verified owned PostgreSQL
fixture at `127.0.0.1:60920`. Read-only post-run inspection confirmed PID absent,
no listener, zero matching database rows and zero matching database sessions.
No cleanup mutation was needed. The awaited host disposal closed the browser context
and fixture; the shared PostgreSQL container remains retained for unfinished lanes.

The final native label-refresh case passed with the three remaining standalone
store repairs in `p02bc-browser-final-four.trx` (4 passed / 0 failed / 0 skipped,
30 seconds; native case 28.2202497 seconds). Refreshed native evidence:
`output/playwright/crm-hr-live-agent-tools/20261016T192104007Z-b167087f08184a2998c326c76393c5fc`.
The screenshot was visually reviewed, including the corrected `Your draft` heading
and separate historical/current names; `server.log` retains the expected injected
accepted-record write failure. Owned PID **28292**, listener **55244**, and lease
`cditall_primary_b3d209e9a5fe4abd94d4c0de032fe66f` were verified cleaned up by
read-only checks: process absent, zero listeners, zero matching database rows and
zero matching sessions on the verified owned fixture at `127.0.0.1:60920`.
No resource mutation was required. The shared database container remains retained
for the unfinished bundle; ordinary application resources were untouched.

## Source and proof secret-scan input

The parent owns `.artifacts/concurrent-mutations/validation/p02bc-source-scan/`,
containing a text snapshot of changed tracked and new non-ignored source/test/tool
files for the existing redaction scanner. This is bounded review evidence, not a
build output or runtime workspace. The scan reads the private fixture password as
an exact sentinel without copying or printing it. Only the sanitized report is
retained for closure; the disposable source snapshot is removed after review.

The first snapshot covered 159 changed/new files and 6,707,962 bytes. The default
5 MiB scan skipped that combined file and is not evidence; the rerun allowed
10,000,000 bytes, scanned it completely and found zero exact private-fixture
password matches. Three assignment matches were reviewed source expressions
(`string.Empty` twice and `password.Take()`), not embedded values. The raw report
retains its failing heuristic status. The exact snapshot file and now-empty owned
directory were removed and absence verified. Later formatting edits require a
refreshed manifest and source scan at the final checkpoint.

## Frozen foundation and subsequent implementation checkout

The Broad Stable foundation gate owns only its bounded logs/results under
`.artifacts/concurrent-mutations/validation/p02bc-stable/` and uses the existing
Release outputs and isolated PostgreSQL fixture. Its 36 assemblies inspect both
binaries and repository source, so the main checkout must remain frozen during
execution. After the foundation commit, create one managed checkout from that
exact commit for the next runtime, maintenance and Workbench source slices.
Record its returned absolute path and attachment identity before using it. Do not
build/test in that checkout or copy custom outputs while the main gate runs.
Integrate reviewed changes only after the frozen gate finishes; archive the
managed checkout after its last required use. Ordinary application resources and
sibling repositories are not temporary checkout resources.
