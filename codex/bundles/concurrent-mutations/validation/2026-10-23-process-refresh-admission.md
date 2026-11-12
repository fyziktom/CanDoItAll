# Atomic Process refresh admission

Status: Pass with historical cleanup follow-up. P28 admission dependency checkpoint, Behavioral
proof tier. Design checkpoint `40cde8844` precedes production edits; the P27 implementation
base is `8b11d7d1c`. SPA remains clean at signed `1139939` on `codex/concurrent-mutations`.

## Implemented boundary

An explicit user/manager action can atomically freeze the current single-activity waiting
decision. RunSnapshot also freezes its original shared scope in the same root-fenced SQL
transaction. Identity derives from root, run and action; changed body or actor under that
identity conflicts. Replay reads and validates the original receipt before reading later
business/runtime state. It reports historical acceptance rather than current progress.
AutomaticReplan and ParentRefresh are rejected as admission requests, even from System.

Runtime owns the typed request, immutable intent and pure transition. Application owns
selection/preparation and admission orchestration. Persistence rechecks the original
accepted input and selected child under the existing root fence. Admission changes only
timestamp and decision/scope state: no provider call, business write, budget settlement,
replacement material, dispatch or public capability. The original child remains reserved.
Generic advance commands must retain the admitted pointer and refresh kind; the successor
clears the pointer while retired/cancelled history retains it.

The generation has one nullable command-reference column. The generated migration adds
its check constraint without rewriting old payloads and uses a five-second DDL lock bound.
Downgrade refuses either retained pointers or version-7 receipts. Receipt versions 1-6
retain their canonical representation; version 7 carries the explicit primitive wire
shape for the new typed intent. Exact command reads project only the two receipt columns;
current discovery joins the indexed head/generation/command keys without loading runtime
history or material payloads. New admission uses existing bounded child, membership and
allocation queries; there is no background scan, new worker, lock family or project edge.

## Validation and corrections

Five affected production projects (Runtime, Application, Persistence, Modules.Processes
and PostgreSQL migrations), plus Unit and Integration, built directly with standard
Release outputs, local libraries and serialized builds. Web is refreshed transitively.
The EF pending-model check reports no changes. Builds and failed intermediate candidates
are retained rather than represented as one clean first attempt.

The initial 17 SQL cases passed seven and failed ten. The shared material command guard
still rejected the newly admitted exact local/shared pair; it now permits only that pair.
The stale-runtime assertions now require the existing optimistic-concurrency exception.
Moving advance validation into the cohesive refresh guard removes the new decision-store
file-size warning. All affected binaries were rebuilt, so the full 343-case selection was
rediscovered and rerun instead of borrowing the initial Unit results.

That rebuilt candidate passed all 236 Unit cases and 14 of 17 admission SQL cases. Two
rollback checks constructed the scope reader without its mandatory canonical profile.
The publication race also incorrectly required a post-commit acknowledgement after
refresh could revoke the old review's read authority. Its corrected proof awaits both
owners, permits only successful acknowledgement or the exact access refusal, verifies the
accepted Project task/receipt and requires unchanged reserved budgets. This proves the
publication/admission ordering; reconciliation of that accepted effect is a downstream
requirement, not a claim of completed refresh.

This final repair changes one test source and only the Integration DLL/PDB. The exact
comparison proves production and Unit outputs unchanged, retaining the 236 passing Unit
cases. All 107 Integration cases are rediscovered and rerun on the final assembly.

All 343 distinct selected cases pass, with zero skipped or unexecuted selected cases.
Eight actual historical/current reader checks pass for new and legacy receipts in both
read modes. Migration tests preserve original row/receipt bytes and prove both pointer
and receipt-only downgrade refusal. The final full portability scan covers 10,104 files
and 47,422 findings. Six reviewed additions are generated migration mappings already
present in the model; there are no stale allowances. The baseline diff contains only
those six and its timestamp. Final enforcement passes without writing the baseline,
with 15,697 reviewed allowances. Rendering-register, static-tool and documentation checks
also pass. No broad Stable or whole-engine result is claimed.

| Exact class topic | Expected/discovered/passed |
| --- | ---: |
| Unit ProcessDecisionRefreshAdmissionTests | 30 |
| Unit ProcessDecisionGenerationTests | 29 |
| Unit ProcessDecisionRunScopeTests | 38 |
| Unit ProcessDecisionRunScopeCompletionTests | 9 |
| Unit ProcessDecisionReviewCompletionTests | 38 |
| Unit ProcessDecisionReviewCoordinatorTests | 35 |
| Unit ProcessDecisionTerminalAccountingTests | 57 |
| Integration ProcessDecisionRefreshPersistenceTests | 17 |
| Integration ProcessDecisionRefreshMigrationTests | 2 |
| Integration ProcessDecisionPersistenceTests | 31 |
| Integration ProcessDecisionRunScopePersistenceTests | 9 |
| Integration ProcessDecisionCompletionPersistenceTests | 38 |
| Integration ProcessDecisionTerminalAccountingIntegrationTests | 10 |

Commands use `dotnet build <project> --configuration Release --no-restore
-p:UseLocalCanDoItAllLibraries=true /m:1 /nodeReuse:false`; discovery/execution use
`dotnet test <project> --configuration Release --no-build --no-restore
-p:UseLocalCanDoItAllLibraries=true /m:1 --filter <recorded exact class prefix>`,
first with `--list-tests`, then with a TRX logger and owned results directory.

## Architecture gate

Scoped snapshots `snap-20261023144945-e8f8071c` and
`snap-20261023160924-65134217` cover Runtime, Application and Persistence. The final snapshot
has 302 documents, 906 types and 7,576 members, the same three intended project-reference
edges and no scoped cycles. No project reference, business partial or service locator was
added. Pure tests use small substituted ports; SQL proof uses the registered producer and
actual Project publication owner. The three new member-count advisories are the explicit
generation wire, refresh-intent wire and typed request. File-size findings remain 34.
The collector still reports 189 WS0002 errors and `hasBlockingErrors=true`; this is not a
clean solution-wide analysis claim. Direct affected builds provide compilation evidence.

## Evidence and progression

Proof directories: `.artifacts/concurrent-mutations/validation/c4-process-refresh-admission`,
`c4-process-refresh-admission-verified`, and `c4-process-refresh-admission-final` under the
same validation parent. They retain discovery, source/output identities, individual TRXs,
command records, compatibility source/results, static summaries and resource observations.
The frozen final candidate pins 1,253 source files and 44 outputs.

The isolated PostgreSQL 18 server on 53619 is reused. Ordinary 5032, SPA, installed tools
and prior denied cleanup targets are preserved. Both completed earlier candidates show
zero new process/database/temporary-directory residue. The final observer initially refused
a mismatched ownership-record schema before any fixture began; the original pre-creation
record is retained, its field names corrected, and observation restarted against the same
baseline. No failed observer run is treated as coverage.

Final observation covers all six Integration commands over 833 seconds. Reconciliation
confirms zero new surviving owned processes, database-name deltas or temporary paths.
Final source/output confirmation is unchanged. Eight exact owned files (six bulk scan
JSON/CSV files and the two older-reader DLLs) were removed after path/hash checks; compact
summaries, failure diagnostics, reader source/results and ownership hashes remain.
Secret review of all three proof roots finds zero credential-sentinel matches and no
oversized/unreadable text exclusions. The three matches per root are reviewed references
to private files/variables in the execution helper, not credential values. Supplemental
checks cover helper source suffixes excluded by the general artifact scanner.

Full P28 remains open. The next owner checkpoint must stop the old child through explicit
refresh authority, preserve unknown or accepted effects, settle exact work and capture one
successor under the original budget. Durable delivery, actual HTTP/queued producers and
UI follow those prerequisites. Broader policies/lifecycles, capacity, remaining owners,
historical Stable failures and final engine E2E remain open. SPA requires colleague review
before any main-branch integration. Historical automatic-review cleanup denials remain.
