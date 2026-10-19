# Resource ownership and cleanup

## Agent policy receipt checkpoint (2026-10-19)

Owned `c4-agent-policy-receipts` and `c4-agent-policy-receipts-ready` reuse the existing
PostgreSQL 18 server and standard Release outputs. Their ownership records precede
creation. The first retains 35 passes/five failures; the repaired candidate passes all
40. Both source/runtime inventories were verified, proof retained and freezes released.
Both entire test commands were observed, with zero remaining observed processes and
zero new database delta. Six/eight unattributed TEMP paths respectively were preserved;
matching a family does not authorize deletion. Existing cleanup blockers remain open
and were not retried. See [the checkpoint](validation/2026-10-19-agent-policy-receipts.md).

## Process policy admission checkpoint (2026-10-19)

Owned candidates `c4-process-policy-admission` and `c4-process-policy-admission-ready`
reused the existing PostgreSQL 18 server and standard Release outputs. Ownership was
recorded before directory creation. The first retained 47 passing/21 failing cases;
the corrected successor passed all 68. Both verified unchanged source/runtime, retained
proof and released their freezes. Observers covered both entire commands and reconciled
with zero remaining observed processes and zero new database delta; six unattributed
TEMP deltas per candidate were preserved. Existing cleanup blockers were not retried or
bypassed. See [the checkpoint](validation/2026-10-19-process-policy-admission.md).

## External Agent preconditions (2026-10-19)

Owned validation directories `c4-external-agent-preconditions` and
`c4-external-agent-preconditions-ready` reused the existing PostgreSQL 18 fixture
and standard Release outputs. Ownership records precede creation. The first stopped
before execution on a copied freeze-command list; no tests or new stack ran there.
Its observer ended with zero remaining observed processes/database delta and one
unattributed TEMP delta preserved. The successor passed 54/54, verified unchanged
source/runtime, retained proof and released its freeze. Its observer covered both
commands; reconciliation found zero remaining observed processes/database delta and
preserved six unattributed TEMP deltas. Earlier cleanup blockers remain unresolved;
none was retried or bypassed. See [the checkpoint](validation/2026-10-19-external-agent-preconditions.md).

## Product planning entry deadlock cleanup blocker (2026-10-19)

Candidate `c4-product-planning-entry-validation` stopped its exact observed test process
tree after a captured DI deadlock. Its new database
`cditall_primary_f238f4df4e3f424bb4efe0ccf43665d9` and directory
`C:/Users/lucys/AppData/Local/Temp/integration-agentframework-structured-output-continuation-failure/b00a57e5e348487db6aa8a506d09dbc2`
share the exact dataset namespace `d4f862b3-f7e2-422a-b7b1-50e51ee7a827`. The candidate
observer recorded creation; the directory contains 11 files and no reparse points. The
isolated container identity and ownership label were rechecked. Automatic approval review
rejected the exact cleanup command as `blocked by policy`; it did not execute. No alternate
deletion mechanism was attempted. Earlier blocked resources remain untouched. The repaired
candidate reuses the same isolated PostgreSQL server and includes these resources in its
baseline; complete cleanup remains blocked.

## Current additional cleanup blocker, 2026-10-19

The rejected `c4-planning-admission-reviewed` test tree was stopped after a diagnosed
service-construction deadlock. Exact PID/start-time reconciliation found no remaining
owned process. Its abandoned resources were linked by the unique fixture Agent and
matching SQL/file activation namespace, with ownership evidence retained in
[the candidate proof](proof/2026-10-19-planning-authority/c4-planning-admission-reviewed.json):

- Database on the existing isolated fixture: `cditall_primary_cdf76c3af0574dbd9ce299eb90002a76`.
- `C:/Users/lucys/AppData/Local/Temp/candoitall-tests/e62070c71e0544a0ad77f2db0f64bbfc`.
- `C:/Users/lucys/AppData/Local/Temp/tool-admission/760b53a19e4f4592968c5df00bca127d`.

Automatic approval review rejected the verified cleanup command with `blocked by policy`;
the command did not execute. These resources remain, alongside the earlier profiler
directory blocker. No alternate deletion mechanism was attempted. The repaired candidate
reused the same PostgreSQL container, included these resources explicitly in its baseline,
passed all 60 cases and added no database/process residue. Full bundle cleanup remains open.

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
`.artifacts/concurrent-mutations/validation/p02bc-stable-qualified/` and uses the existing
Release outputs and isolated PostgreSQL fixture. Its 36 assemblies inspect both
binaries and repository source, so the main checkout must remain frozen during
execution. After the foundation commit, create one managed checkout from that
exact commit for the next runtime, maintenance and Workbench source slices.
Record its returned absolute path and attachment identity before using it. Do not
build/test in that checkout or copy custom outputs while the main gate runs.
Integrate reviewed changes only after the frozen gate finishes; archive the
managed checkout after its last required use. Ordinary application resources and
sibling repositories are not temporary checkout resources.

The earlier `p02bc-stable/` record retains a runner failure after product restore,
before any build or test. The repaired runner's separate `p02bc-stable-qualified/`
run is the active evidence. Neither directory is a custom build output.

The managed continuation checkout is now attached with identity/root
`C:\Users\lucys\.codex\worktrees\concurrent-mutations-runtime\CanDoItAll`,
created from foundation commit `702d0a93b1081c42d5a06893e7070219aee5d089`.
Creation operation: `7881a9f7-91ad-4d1f-ab99-9ab7c415ca92`; attachment:
`01a1462e-3aea-77f1-81c6-d3a05d5672d1`. The parent records ownership in the main
checkout's ignored `p02bc-future-worktree-ownership.json` before edits. It is
source-only while the main gate is running: no builds, tests, fixture creation
or automatic dependency setup. Runtime journal, Projects retention, Workbench
create and Web schema slices use nonoverlapping ownership in this one checkout.
The main checkout and its Components/FileTools/Mcp source identities remain frozen.
Archive this exact managed attachment after integrating and validating its last
required changes; do not prune unrelated worktrees or sibling resources.

SPA source preparation uses the separately owned Git worktree
`C:\repositories\CanDoItAll.SPA-concurrent-mutations`, branch
`codex/concurrent-mutations`, starting at
`5533969fd7ca0807d9438229303a93c738e009a8`. Ownership was recorded before creation
in the main checkout's ignored `p02d-spa-worktree-ownership.json`. The primary SPA
checkout remains unchanged. The app worktree tool selects this chat's engine
repository, so this sibling worktree uses ordinary Git registration. Prepare
source only while the frozen gate runs; no dependency setup, generation, build
or tests until the parent releases validation. Remove this exact registered
worktree after reviewed changes are integrated and its final proof is retained;
do not remove the primary SPA repository or its unrelated worktrees.

After reviewing isolation, the parent permits the SPA's 20 pure Node preparation
tests during the frozen .NET gate. They use no shared .NET outputs, database, server
or dependency install. Their owned `candoitall-spec-preflight-*` and
`candoitall-pending-asset-*` temporary roots are allocated with `mkdtemp` and removed
in finally; setup failures are inside that cleanup boundary. Bounded TAP/provenance
logs go under the main checkout's ignored validation directory. No other future
checkout build/test/database restriction is released by this exception.

The pure Node run passed 20 cases with zero failures/skips, used 0.2001274s command
wall time, and left zero newly owned temporary roots. All 21 hashed source inputs
were unchanged. Its proof directory is
`.artifacts/concurrent-mutations/validation/p02d-spa-preparation/`.
The parent additionally permits isolated SPA dependency setup and the app build,
with ownership recorded before creation. They must use the existing lockfile and
worktree-local outputs, avoid setup hooks changing shared Git configuration, and
perform no backend generation, .NET build, database work or server launch. The
frozen Stable elapsed-time record must acknowledge this separate host workload;
it is not a controlled performance benchmark.

The SPA preparation is committed at
`2fe9e8ee1b6b7ce9e8777320b7daa19f895cfebb`: Node20, app build, direct owned
format/lint, exact asset and non-root URL checks passed. Worktree-local
`node_modules`, `packages/app/dist`, tsbuildinfo and `logs/concurrent-mutations`
remain owned and retained for the next frontend slice. The pnpm exec dependency
check unexpectedly reinstalled and invoked `prepare: vp config`; the exact
introduced hook setting was removed and the shared config's complete original
hash restored. Lock/root manifest/workspace/tracked hook were unchanged.

Cleanup blocker: automatic approval review rejected guarded removal of the exact
generated inactive directory
`C:/repositories/CanDoItAll.SPA-concurrent-mutations/.vite-hooks/_` (16 files,
created 2026-10-16T20:12:27Z) with `blocked by policy`. Do not retry its removal
through another command or remove the worktree to bypass that rejection. Retain
the recorded evidence and report this item until resolved. The original tracked
`.vite-hooks/pre-commit`, primary SPA and shared caches are not cleanup targets.
Subsequent package-manager entry uses process-scoped
`pnpm_config_verify_deps_before_run=error`; direct final checks avoided pnpm.
See [SPA proof](validation/P02d-spa-preparation.md) and the SPA validation record.

The isolated SPA adapter validation reused those dependencies and refreshed only
owned app outputs. Commit `0411cd3e65d50d43961a97480ac120d7d1552d30` records the
four reviewed source inputs. Its 18 controlled-module Node cases, direct lint/format,
typecheck and app build passed; exact proof is retained under the existing ignored
`logs/concurrent-mutations/pending-adapter/`. No new server, browser, database or
installation was created. This exception does not release the frozen .NET graph.
The inactive hook directory and its cleanup blocker remain unchanged.

The frozen Stable checkpoint ended and all original source/binary/runner identities
were verified at 2026-10-16T21:53:30Z. The parent may integrate the reviewed managed
candidate into the main checkout and resume serialized standard Release validation.
The managed checkout becomes read-only after the recorded transfer manifest.

One interrupted Integration fixture remains: database
`cditall_primary_67c47aa1d495479495c55deaa4fdeedf` on the existing owned test server,
and temporary root
`C:/Users/lucys/AppData/Local/Temp/integration-agentframework-structured-output-continuation-failure/3bdf069eb3754e4894a4b9e77a186edd`.
An independent audit matched its activation profile to the deterministic exact
database endpoint identity and test. Original runner/testhost processes are absent;
the database had zero active clients. Automatic approval review rejected the
guarded cleanup command before execution with `blocked by policy`. Neither resource
was removed. Do not bypass the rejection through a different removal mechanism.
The ignored `integration-residue-cleanup-blocked.json` retains the exact ownership
and rejected action. Reuse the current server; do not provision a replacement.

## Current receipt diagnostics

The parent closed the UTF-8 comparison with all 84 executions, exact source/binary
identities and owned host/fixture cleanup verified. A read-only database inventory
after the 15 compatibility Integration cases found only the prior blocked fixture
with zero clients and the administrative `postgres` connection; no new database residue.

Explicit-session diagnostics use the pre-recorded
`.artifacts/concurrent-mutations/explicit-diagnostic.ownership.json` and fresh
`validation/r1bc-explicit-gc-diagnostic` output. The collector and parser are local
task tooling under `diagnostics`, built with standard Release outputs and existing
installed dependencies. No tool installation, Docker stack or ordinary host is created.
The collector must own only its recorded IPC session and child process; the parent
records and releases the workload process tree separately. Raw traces remain local.
Collection, parser acceptance, refusal arrangements and final output retention are
separate gates; none is inferred from successful compilation.

During this parent validation, managed-checkout agents may author the explicitly
assigned historical-read and source-review successors. They must not build, execute
tests, launch hosts or change databases there. Reviewed transfers remain guarded by
exact predecessor hashes and occur only after the current main validation freeze ends.

The explicit diagnostic freeze closed at 2026-10-17T03:03:35Z. The original launcher
failure, V2 accepted trace, two refused parser interpretations and accepted third
interpretation remain recorded separately. Missing-collector and wrong-identity V2
arrangements and interrupted-collector V3 each verified natural testhost exit and
fixture cleanup. V3 intentionally terminated only its recorded collector after actual
workload-entry evidence; its partial trace is ineligible for parsing. The unused V2
interruption root closed before discovery with no process or fixture created.
See [diagnostic closure](validation/R1bc-controlled-allocation-diagnostic.md).

All diagnostic processes and fixtures are closed. Retained proof, raw traces and
reviewed local tool sources remain under the recorded task-owned `.artifacts` paths;
no global cleanup is authorized. The earlier blocked Integration database/temp root
and inactive SPA hook directory remain unresolved. Main may receive reviewed next
candidate edits; managed authoring still does not run builds, hosts or databases.

The prepared run-read candidate's source freeze ended at
`2026-10-17T03:35:49.8728707Z` after all 8,619 source identities were rechecked.
All 112 correctness cases passed; all 22 performance rows executed with 11 passed
and 11 failed. Every owned host exited and every file fixture cleaned up. These
lanes created no database or container. The original report-only closure failure
and its successful V2 amendment are retained separately; no tests were rerun to
repair reporting. See [candidate proof](validation/P02b-R1bc-run-witness-candidate.md).
The earlier blocked cleanup resources and configured GPG signing wait remain open.

The journal byte-count candidate's source freeze ended at
`2026-10-17T04:02:03.016248+00:00` after all 8,626 source inputs and HEAD matched.
All 135 correctness cases passed; 22 performance rows produced 12 passed and 10
failed. All hosts exited naturally and owned file fixtures were removed; these
lanes created no database or container. See [executed proof](validation/P02b-R1bc-journal-size-candidate.md).
The next wire-comparison evidence root has its own ownership record before creation.
It reuses standard Release outputs and preserves the prior immutable evidence.

The wire-comparison source freeze ended at `2026-10-17T04:19:15.108405+00:00`
after all 8,632 source inputs and HEAD matched. All 161 correctness cases passed;
22 performance rows produced 11 passed and 11 failed. Every host exited naturally
and all owned file fixtures were removed; no database/container was created.
See [executed proof](validation/P02b-R1bc-journal-wire-candidate.md).
The reviewed historical transfer source is preserved in a separately owned ignored
directory: 113 files, 1,583,323 bytes, inventory
`80F0B05601EF13B530005069432598949762D6EAB55A26EC5544F8D015AAE756`.
This prevents later managed authoring from changing the admitted historical candidate.
The exact restart runner source is retained separately; no restart test has run yet.

## 2026-10-19 Agent policy validation

The three policy candidates reuse the registered PostgreSQL18 fixture and standard
Release outputs. Their ownership records precede directory creation. The repaired
candidate passed 63/63; the final visual correction passed all six affected cases,
with source/runtime identity verified and the freezes released. The final observer
covers both commands, with zero remaining observed processes and zero new database
delta; seven unattributed TEMP deltas are preserved. The full record is
[Agent policy access](validation/2026-10-19-agent-policy-access.md).

The parent prematurely interrupted the initial component command and left one
attributed fixture. Automatic approval review rejected its guarded cleanup before
execution with `blocked by policy`; neither deletion ran and no alternate deletion
was attempted. Remaining database: `cditall_primary_e76a5b7c828f4ec7b51c7d88b604524e`.
Remaining directory:
`C:/Users/lucys/AppData/Local/Temp/candoitall-component-tests/e701d324735d408aa351ef96a6011b02`.
Its derived profile is `0a7200a7-d75f-4d95-e4fb-611c843977a8` and activation namespace
is `b2da6e8f-72e4-4bed-aa47-7bd1aabaa150`. Zero clients and zero observed processes
were confirmed before the denied attempt. Later baselines include this residue;
zero later deltas do not close this or any earlier cleanup blocker. The initial
observer omitted this existing component TEMP family; successors include it explicitly.

## 2026-10-19 Native Agent policy recovery

`c4-agent-policy-native`, `c4-agent-policy-native-ready` and
`c4-agent-policy-native-navigation` have ownership records predating creation and reuse
the existing isolated PostgreSQL18 fixture and standard Release outputs. The first
candidate passed 38/39; two test-only repairs led to the passing exact native recovery
case, with unchanged-production bridges. All source/runtime freezes were verified and
released. Complete-command observation shows zero remaining observed processes and
zero new database delta for each candidate. Twelve, six and four unattributed TEMP
entries respectively are preserved. No older blocked cleanup was retried or bypassed.
See [the native recovery record](validation/2026-10-19-agent-policy-native-recovery.md).
