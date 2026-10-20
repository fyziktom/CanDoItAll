# Resource ownership and cleanup

## Process capture ownership checkpoint (2026-10-20)

The initial and repaired `c4-process-capture-ownership` candidates released test
execution with complete source/runtime verification and whole-command observation.
Initial 104/127 and final 29/29 results are retained, with an exact test-only comparison
preserving 98 passing regressions and 1725 production outputs. Each run leaves zero
observed processes and database-name delta; fourteen initial and eight final TEMP
entries without proven ownership remain untouched.

The directories contain small validation inputs/logs and reusable helpers. Retain
their identity manifests for the proof comparison and following producer integration.
No new server or compiled output root was created. The shared PG18 fixture remains
needed, and ordinary 5032 and unrelated resources remain outside ownership. Earlier
automatic-review cleanup denials remain blocked and were not retried.

## Process result/accounting checkpoint (2026-10-20)

The initial, repaired and optional-join `c4-process-result-accounting` candidates have
released test execution with complete source/runtime verification and whole-command
observation. Each leaves zero observed processes or database-name delta. Their exact
failure/success evidence is retained in three secret-reviewed proof files; the final
candidate passes 111/111, with 258 unaffected passes under the exact repair comparison.
Eleven final TEMP entries without proven ownership remain untouched.

These owned candidate directories contain retained logs, source manifests and reusable
validation helpers, with no new compiled output trees or server. Keep the identity
manifests required by the repair bridge and the helpers used by the next producer proof.
The existing isolated PG18 fixture on 53619 remains in use. Ordinary 5032, its databases,
installed MCP tools and unrelated resources remain outside ownership. Historical
cleanup denials listed below remain blocked and were not retried.

## Shared allocation boundary checkpoint (2026-10-19)

Owned `c4-shared-decision-budget` passed the same 28 arithmetic cases after their move
to SharedKernel. Its source/runtime inputs and exact naming-only source transformation
were verified; the freeze was released. The observer covered the entire command and
reconciliation found zero remaining observed processes and zero database delta. Four
unattributed TEMP entries were preserved. No new database fixture or application host
was required; existing cleanup blockers remain unchanged. See
[the validation record](validation/2026-10-19-shared-decision-budget.md).

## Process decision arithmetic checkpoint (2026-10-19)

Owned `c4-process-decision-budget` built standard Release outputs and passed all 28 pure
Unit cases. No test database or application host was required. Its complete command was
observed; source/runtime inventories were verified and the freeze released. Reconciliation
found zero remaining observed processes and zero database delta, preserving three
unattributed TEMP entries. Existing shared fixture/cleanup blockers remain unchanged.
See [the validation record](validation/2026-10-19-process-decision-budget.md).

## Process policy authority checkpoint (2026-10-19)

Owned `c4-process-policy-authority` and `c4-process-policy-authority-ready` reused the
existing isolated PostgreSQL 18 fixture and standard Release outputs. Ownership records
precede creation. The first retains 47/52 passes and five fixture failures; the successor
passes all 27 repaired/related cases with an exact unchanged-production bridge retaining
31 earlier cases. Both source/runtime inventories were verified and freezes released.
Observers cover all six complete execution commands. Reconciliation found zero remaining
observed processes and zero new database delta; 12/10 unattributed TEMP entries were
preserved. Prior cleanup blockers were not retried. See
[the validation record](validation/2026-10-19-process-policy-authority.md).

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

## 2026-10-19 Process budget persistence

`c4-process-budget-persistence` has a pre-creation ownership record and reuses the
existing isolated PostgreSQL18 fixture and standard Release outputs. All 41 selected
cases pass. Complete source/runtime identity was verified before freeze release;
observation covers all three entire test commands. Reconciliation found zero remaining
observed processes and zero database-name delta. Twelve unattributed TEMP entries remain
preserved. No earlier blocked cleanup was retried or bypassed. See the
[accounting validation record](validation/2026-10-19-process-budget-persistence.md).

## 2026-10-19 Deleted-Agent policy recovery and actual host restart

`c4-agent-policy-deleted-recovery`, `c4-agent-policy-deleted-recovery-ready` and
`c4-agent-policy-deleted-recovery-owner` have ownership recorded before creation and
reuse the existing isolated PostgreSQL18 fixture and standard Release outputs. The
native restart replaces only the fixture's own process, retaining its profile, database,
origin and browser storage; original and successor IDs/start times are recorded.
All freezes were verified before release and every execution command was fully observed.
Each candidate reconciled to zero remaining observed processes and zero database delta.
Fifteen, 21 and nine unattributed TEMP entries respectively remain preserved. Initial
failures, exact repairs, 58 latest passing identities and reviewed screenshots are retained
in [the validation record](validation/2026-10-19-agent-policy-deleted-recovery.md). Two
read-only Components MCP query children also exited; no installation was changed.
No prior cleanup-review denial was retried or bypassed. The shared fixture remains needed
for continuing Process/Workflow work; this is not complete bundle cleanup.

## 2026-10-19 Process generation lifecycle

`c4-process-decision-generations` recorded ownership before creation and reused standard
Release outputs. Its 29 isolated cases passed with full-command observation. Complete
source/runtime identities were verified before freeze release. Reconciliation found no
remaining observed process or new database delta; three unattributed TEMP entries were
preserved. Earlier cleanup denials were not retried. The existing isolated fixture remains
needed for durable generation integration. See the
[validation record](validation/2026-10-19-process-decision-generations.md).

## 2026-10-19 Atomic Process generation persistence

`c4-process-generation-persistence` recorded ownership before creation and reused the
same isolated PostgreSQL18 fixture and standard Release outputs. The restart fixture's
exact TEMP family and the earlier budget restart family are now explicitly inventoried.
All 101 selected cases passed, with complete-command observation. Complete source/runtime
identities were reverified before release; reconciliation found no remaining observed
process or database-name delta. Seventeen unattributed TEMP entries remain preserved.
No earlier cleanup-review denial was retried or bypassed. See the
[storage validation record](validation/2026-10-19-process-generation-persistence.md).

## 2026-10-19 Process child publication condition

`c4-process-child-publication` recorded ownership before creation and reused the isolated
PostgreSQL18 fixture and standard Release outputs. All 108 selected cases passed with
complete-command observation. The actual restart TEMP family is explicitly inventoried.
Complete source/runtime identities were verified before releasing the freeze. Resource
reconciliation found zero remaining observed processes and zero database-name delta;
16 unattributed TEMP entries remain preserved. No earlier denied cleanup was retried.
See the [publication validation record](validation/2026-10-19-process-child-publication.md).

## 2026-10-19 Versioned Process execution admission

`c4-process-execution-contract` and its exact test-only successor recorded ownership
before creation and reused standard Release outputs plus the existing PostgreSQL18
fixture. Complete-command observation and full source/runtime verification passed for
both candidates before freeze release. The first has 112/114 passing cases; the repair
has 60/60 Unit passes and an exact unchanged-production/runtime bridge to all 54 database
passes. No observed process or database-name delta remained. Eleven unattributed TEMP
entries from the first candidate were preserved; the repair observed none. Earlier
cleanup denials remain unchanged. See the
[validation record](validation/2026-10-19-process-execution-contract.md).

## 2026-10-19 Original material value

`c4-process-inherited-material-value` and its test-repair successor reused standard Release
outputs and the existing fixture. Initial proof retained 41/44 passes and three test-only
repairs. The final 44/44 pass had complete observation and source/runtime verification.
A prior unobserved pass remains explicit: the full shared TEMP baseline exceeded 16,384
entries. Read-only measurement justified a finite 32,768-entry baseline capacity, retaining
the separate 16,384 new-delta bound. No TEMP entries were deleted or excluded. Final
reconciliation found no observed process, database-name delta or new TEMP entry; three
unattributed entries from the first candidate were preserved. Earlier denied cleanup
remains unchanged. See the [material proof](validation/2026-10-19-process-inherited-material-value.md).

## 2026-10-19 Canonical material storage

`c4-process-material-persistence` and its test-repair successor recorded ownership before
creation, reused standard Release outputs and the existing isolated PostgreSQL18 fixture.
The initial 237-case run passed 234; three test-only SQL execution/expected-code repairs
were retained explicitly. The successor passed all 44 affected database cases and an
exact source/runtime bridge retains the other 193 passes. Both freezes were verified and
released after complete-command observation. Neither candidate left an observed process
or database-name delta. Fifteen and eight unattributed TEMP entries were preserved without
claiming ownership. No new resource family or stack was introduced. The reviewed 32,768
TEMP baseline bound and separate 16,384 delta bound remain unchanged. Earlier denied
cleanup remains explicit; the fixture remains needed for continuing implementation.
See the [validation record](validation/2026-10-19-process-material-persistence.md).

## 2026-10-19 Shared material content

`c4-process-shared-content` recorded ownership before creation and reused the existing
Release outputs and isolated PostgreSQL18 fixture. All 265 selected cases passed;
complete source/runtime verification and 536.0032092 seconds of whole-command observation
preceded freeze release. No observed process or database-name delta remains. Fourteen
unattributed TEMP entries were preserved. The initial startup-file check typo and actual
prior V2 startup are retained in proof. No new family or stack was added; earlier denied
cleanup remains unchanged. See the [validation record](validation/2026-10-19-process-shared-content.md).

## 2026-10-19 Root funding

`c4-process-run-budget-funding` retained 327 passes and 34 failures. The corrected
`c4-process-run-funding-publication-repair` repeated all 361 cases and passed. Both used
the existing standard Release outputs, isolated PostgreSQL fixture and 114 resource
families. No new stack or resource family was introduced. Whole-command observation and
unchanged source/runtime checks preceded both freeze releases. No observed process or
database-name delta remained; 32 and 20 unattributed TEMP entries were preserved.

The prepared repair directory was renamed with ownership recorded before the move. The
observer initially rejected its ownership state before tests began; the successor checks
both exact ownership records, source absence, common intended parent and ordered timestamps.
The failed startup and correction are retained. Earlier denied cleanup remains unchanged;
the shared fixture is still needed for active-scope and runtime integration. See the
[validation record](validation/2026-10-19-process-run-budget-funding.md).

The stalled Agent recovery commit and seven retained Process checkpoints completed with
valid signatures: `5656cae65`, `cb42edb57`, `1229f6986`, `4f3b07476`, `621611e79`,
`ecbb119d4`, `8bb22f773`, `41a4df8d5`. Each Process checkpoint used its saved reviewed blobs;
later root-funding changes were not accidentally staged into those commits.

## 2026-10-19 Active shared run scope

The first `c4-process-shared-run-scope` candidate retained 397 passes and 17 failures.
`c4-process-shared-run-scope-repair` passes all 68 focused cases covering every failed
behavior and the affected wire/query contracts. Both roots recorded ownership before
creation, reused standard Release outputs and the same isolated PostgreSQL fixture, and
introduced no new resource family or stack. Complete source/runtime verification and
whole-command observation preceded freeze release.

No observed process or database-name delta remained. The first run's 28 and repair's
12 unattributed TEMP entries remain preserved. Earlier denied cleanup is unchanged.
The shared fixture remains needed for inherited Agent/Workflow execution proof.
See the [scope validation record](validation/2026-10-19-process-shared-run-scope.md).

## 2026-10-19 Inherited Agent journal

`c4-inherited-agent-journal` retained 238 passes and four test-setup/assertion failures
from 242 cases. Its repair repeats all 21 consumers of the corrected fixture and passes;
the exact production bridge supports the other 221 passing cases. Both owned roots reuse
standard Release outputs, the existing isolated PostgreSQL fixture and existing resource
families. No stack or provider endpoint was created. Observers covered all test commands
for 450.60 and 54.18 seconds, followed by unchanged source/runtime verification.

No observed process or database-name delta remained. Respectively 25 and 14 unattributed
TEMP entries were preserved. Earlier denied cleanup remains unchanged. The shared fixture
remains needed for actual Process/Workflow integration. See the
[Agent validation record](validation/2026-10-19-inherited-agent-planning.md).

## Exact inherited admission candidate ownership

The next candidate owns only `.artifacts/concurrent-mutations/validation/c4-exact-inherited-admission`
and its bounded command, identity, test and observation evidence. It reuses the isolated
PG18 fixture on loopback 53619 and the existing standard Release outputs. No new host,
container, volume or database server is provisioned. Tests use the existing tool-admission
fixture family and dispose their own temporary directories; observed unrelated TEMP
deltas are not deletion authority. Historical cleanup denials remain unresolved.

The original candidate retained 71/73 passes; two test-only successors retained 27/28
and 28/28. Exact source bridges preserve the original 45 regression passes. All three
observations covered their complete test commands, with no remaining observed process
or database-name delta. Respectively 12, 4 and 4 unattributed TEMP entries were preserved.
The final complete static gate passes. See the
[admission validation record](validation/2026-10-19-exact-inherited-admission.md).

## Shared inherited planning coordination

`c4-inherited-planning-coordinator` and `c4-inherited-planning-coordinator-final` recorded
ownership before creation and reused standard Release outputs, the existing tool-admission
family and isolated PostgreSQL on 53619. Initial 169/170 and final 170/170 results, command
coverage and source/runtime identities are retained in the coordinator validation record.
The 298.8655941- and 289.5316435-second observers covered both complete commands in each
candidate. Reconciliation found zero surviving observed process identities and zero
new database names. Nine initial and eight final unattributed temporary paths were
preserved. Existing cleanup denials and ordinary port 5032 remain unchanged.

## Canonical child/material resolution

The c4-canonical-child-material and c4-canonical-child-material-final roots record ownership
before creation and reuse the existing isolated PostgreSQL fixture and standard Release
outputs. The initial 151/169 result and repaired 18/18 result retain an exact single-test-file
bridge. Their 469.3336321- and 100.0683851-second observers cover all test commands, with
zero remaining observed processes and zero database-name delta. Seven and five unattributed
TEMP entries respectively were preserved. Exact identity checks pass before release.
See the [validation record](validation/2026-10-19-canonical-child-material.md); historical
cleanup denials remain unresolved.


## Inherited workspace lifecycle checkpoint

The c4-inherited-workspace-lifecycle and c4-inherited-workspace-lifecycle-final roots
record ownership before creation, reuse existing task-plan/tool-admission resource
families and PostgreSQL 18 on 53619, and use standard Release outputs. Initial 182/185
and final 185/185 results are retained; all cases repeat after the production repair.
Observers cover complete commands for 391.0883235 and 393.2450934 seconds. Both source/runtime
identities verify before freeze release. No observed process or database-name delta
remains; eight initial and nine final unattributed TEMP entries are preserved. The shared
fixture remains needed by later lanes. Historical cleanup denials remain open. See the
[validation record](validation/2026-10-19-inherited-workspace-lifecycle.md).


## Publication claim-lifetime checkpoint

The c4-publication-claim-lifetime root records ownership before creation and reuses
PostgreSQL 18 on 53619, existing Process families and standard Release outputs. All
106 selected cases pass. The 422.7272699-second observer covers the complete execution;
source/runtime identity verifies before freeze release. No observed process or
database-name delta remains. Four unattributed TEMP entries are preserved. Historical
cleanup denials remain open; the fixture is still needed. See the
[validation record](validation/2026-10-19-publication-claim-lifetime.md).

## Asynchronous binding checkpoint

The c4-asynchronous-child-binding root records ownership before creation and reuses the existing isolated PostgreSQL 18 endpoint, standard Release outputs and 119 resource families. All 192 cases pass under full observation. Source/runtime identity is unchanged; no observed process or database-name delta remains. Twenty unattributed TEMP entries are preserved. The freeze is released and the fixture remains required. Details and proof identities are in the [validation record](validation/2026-10-19-asynchronous-child-binding.md). Historical cleanup denials remain open.

## Selected Process dispatch checkpoint

The c4-selected-child-dispatch root records ownership before creation and reuses the isolated PostgreSQL 18 fixture, standard Release outputs and 119 resource families. All 86 cases pass under complete observation; source/runtime is unchanged. No observed process or database-name delta remains; three unattributed TEMP entries are preserved. The freeze is released. The [validation record](validation/2026-10-19-selected-child-dispatch.md) retains exact identities. The shared fixture and historical cleanup denials remain open.

## Inherited publication snapshot checkpoint

The initial and corrected c4-inherited-publication-snapshot roots record ownership before allocation and reuse standard Release, the existing isolated PostgreSQL fixture and 119 resource families. The initial run retains 235/239 passes, three fixture failures and the coarse SQL-wait negative comparison; the corrected run passes all 238 positive cases. Observers cover both complete runs, source/runtime identity verifies before release, and no observed process or database-name delta remains. All three owned measurement-row cleanups pass. Seven unattributed TEMP entries per run are preserved. Historical cleanup denials and the still-required shared fixture remain open. Exact identities and proof hashes are in the [validation record](validation/2026-10-20-inherited-publication-snapshot.md).

## Process Project/root ordering checkpoint

The initial and repaired c4-process-project-lock-order roots recorded ownership before creation and reused standard Release outputs, PostgreSQL 18 on 53619 and 119 existing resource families. The initial194 run retains185 passes and9 fixture failures; all22 corrected cases pass through an exact two-test-file production/runtime bridge, giving194 distinct selected passing behaviors. Both complete commands were observed, frozen identities verified and freezes released. No observed process or database-name delta remains. Eight initial and four repair unattributed TEMP entries are preserved; historical cleanup denials and the still-required shared fixture remain open. See the [validation record](validation/2026-10-20-process-project-lock-order.md).

## Selected Process publication checkpoint

The initial and repaired c4-selected-publication-fence roots recorded ownership before creation and reused standard Release, the existing isolated PostgreSQL 18 fixture and 119 resource families. The initial run retains 124/126 passes and two ambient-context fixture failures; all 23 repair cases pass, giving 126 distinct passing cases through a one-test-file production/runtime bridge. Both commands were fully observed, identities verified and freezes released. No observed process or database-name delta remains. Seven initial and four repair unattributed TEMP entries are preserved. The shared fixture and historical cleanup denials remain open. Exact identities are in the [validation record](validation/2026-10-20-selected-publication-fence.md).

The [enlisted task publication checkpoint](validation/2026-10-20-enlisted-task-publication.md) passes all 72 correctness cases, including real Process expiry before staging and after flush. Its one-second selected-guard performance reference failed for two writers (1006.85 and 1066.32 ms); current responsiveness is reopened and is not a green gate. Inherited producer/continuation and wider capacity remain open.

The [workspace path-probe repair](validation/2026-10-20-workspace-path-probe.md) passes all 75 selected cases. Exact internal path comparison removes repeated filesystem case-probe writes while preserving fresh containment/link validation and durable journals. The identical selected SQL-wait reference now has all six writers below one second (162.67–670.72 ms). Earlier negative evidence is retained; sustained capacity and inherited runtime activation remain open.

## Live-parent saved-effect continuation checkpoint

The c4-parent-saved-continuation, c4-parent-saved-continuation-final and
c4-parent-saved-continuation-verified roots are Released. All reused standard Release
outputs and the existing isolated PostgreSQL 18 fixture. Earlier 190/200 and 13/14 results
are retained; the verified candidate repeats and passes all 201 selected cases. Complete
source/runtime identities and observer coverage pass. Every candidate has zero remaining
observed processes and zero database-name delta. Four, three and seven respective
unattributed TEMP deltas are preserved without deletion authority. Shared fixture use and
historical cleanup denials remain open. See the [validation record](validation/2026-10-20-parent-saved-effect-continuation.md) for exact identities and retained proof hashes.

The c4-inherited-task-receipt-identity root is Released after all 75 cases passed.
It reused standard Release and the existing isolated PostgreSQL fixture; source/runtime
identities and full observer coverage passed. The observer first refused a stale baseline
pin, corrected before tests without changing frozen source/runtime. Zero remaining observed
processes and zero database-name delta; six unowned TEMP deltas preserved. Shared fixture
and historical cleanup denials remain open. Exact identities are in the [receipt identity
record](validation/2026-10-20-inherited-task-receipt-identity.md).

The three inherited-task-publication-codec roots are Released. Earlier 89/96 Unit results
remain recorded; the verified candidate repeats and passes all 124 selected cases. Every
freeze and complete observer coverage passes, with zero remaining observed processes or
database-name delta. Two, zero and six respective unowned TEMP entries are preserved.
The existing isolated PostgreSQL fixture and historical cleanup denials remain open.
Exact identities and hashes are in the [codec validation record](validation/2026-10-20-inherited-task-publication-codec.md).

Both selected-process-source-authority roots are Released after the complete repaired
23-case run. Frozen identities and observer coverage pass, with zero remaining observed
processes or database-name delta. Four initial and five final unowned TEMP deltas are
preserved. The shared PostgreSQL fixture and historical cleanup denials remain open.
See the [authority validation record](validation/2026-10-20-selected-process-source-authority.md).

Both inherited-task-publication-authority roots are Released. The final 67-case run and
initial 62/64 attempt retain frozen identities, complete observer coverage and exact
secret-reviewed proof. Each has zero remaining observed processes and database-name delta;
three initial and thirteen final unowned TEMP entries remain untouched. The shared fixture
and historical cleanup denials remain open. See the [publication authority record](validation/2026-10-20-inherited-task-publication-authority.md).

Both inherited-original-source-history roots are Released. The initial100/107 and final30/30
results retain complete frozen/observer proof and an exact test-only bridge preserving77
regressions and1399 production outputs. Each has zero remaining observed processes and
zero database-name delta; four initial and three final unowned TEMP entries are preserved.
Shared fixture use and historical cleanup denials remain open. See the [history record](validation/2026-10-20-inherited-original-source-history.md).

`c4-inherited-task-receipt-history` is Released: 64/64 selected cases, complete 508.435-second observation, unchanged source/runtime, no observed process or database-name delta. Eleven TEMP paths without proven ownership remain untouched. Shared PG18 remains in use. The separately owned `root-sequence-probe` standalone Release experiment remains active for its matched candidate measurement; its ownership sidecar records exact cleanup scope.

`c4-root-sequence-registry` test execution is Released: 26/26 cases, unchanged source/runtime, complete 64.122-second observation, zero remaining observed processes/database-name delta, seven unowned TEMP paths preserved. The standalone `root-sequence-probe` has no active process but remains `CleanupBlocked`: automatic approval review rejected removal of its owned `bin` and `obj` directories with `blocked by policy`. No deletion or workaround occurred. Exact remaining paths and raw measurements/source are retained in its sidecar and reviewed proof. Shared PG18 remains needed.

Both `c4-inherited-initial-source` roots are Released. The initial 38/244 result remains retained; the repaired full repeat passes 247/247. Frozen source/runtime and whole-command observation are verified. Each run leaves zero observed processes/database-name delta and preserves five TEMP paths without proven ownership. Shared PG18 and existing cleanup denials remain open. See the [admission record](validation/2026-10-20-inherited-initial-source.md).

Registered Process source roots c4-inherited-product-source and c4-inherited-product-source-final are Released. Initial 152/153 and final 15/15 plus 138 unchanged regression passes are retained with exact identity bridge. No remaining observed processes or database-name delta; eight initial and three final unowned TEMP entries preserved. Shared PG18 remains needed; existing probe-output cleanup rejection is unchanged.

The c4-accepted-source-scaling root is Released after 15 passing pure measurements, full identity/coverage checks and zero observed process/database delta. Nine unowned TEMP entries are preserved. No new host, project or database; the existing probe-output cleanup denial remains unchanged.

The c4-accepted-source-codec root is Released after 41 passing pure executions, full identity/coverage checks and zero observed process/database delta. Nine unowned TEMP entries are preserved; no new host/database. Existing probe-output cleanup denial remains unchanged.

`c4-accepted-source-persistence` is owned and prepared for migration, runtime-source and SQL scaling proof. Its pre-create sidecar records ownership; standard Release outputs and the existing isolated PG18 fixture are reused. No new server/container is created. Freeze, observer reconciliation and proof retention remain pending. Earlier cleanup denials remain open.

Initial `c4-accepted-source-persistence` is Released with28/31 codec passes and160 unexecuted cases; complete21.477-second observation, no observed process/database-name delta and4 unowned TEMP paths preserved. The failure proof is retained and secret-reviewed. Owned `c4-accepted-source-persistence-final` reuses the same fixture/standard outputs for the corrected complete193-case candidate.

The final accepted-source SQL/history, authority and dispatch/original groups pass 142 cases; the unchanged 51 earlier passes give 193 distinct passing cases. All three final roots are Released after full identity verification, observer coverage, retained proof and secret review, with no new process/database delta. The earlier combined regression command timed out at 900 seconds and remains recorded as such. Its root is ReleasedWithUnattributedDatabase: cditall_primary_3c41089b37074f30b5d6889519d95021 has no connections but lacks exact lease ownership evidence and is preserved. Earlier denied probe/SPA cleanup also remains open.

The original Process outcome reader has 129 distinct passing behaviors across reviewed correction boundaries. All five runs retain exact source/runtime and observer evidence, with zero remaining observed processes or database-name deltas. The initial/repaired/final SQL roots remain in use for the immediately following result integration's helpers and design notes. Automatic approval review rejected recursive removal of the exact owned c4-process-owner-outcome-verified and c4-process-owner-outcome-final directories with `blocked by policy`; both remain CleanupBlocked, with no deletion or retry. Shared PG18 and earlier cleanup denials remain unchanged. Exact hashes, failed attempts and preserved unowned TEMP counts are in the [validation record](validation/2026-10-20-process-owner-outcome.md).
