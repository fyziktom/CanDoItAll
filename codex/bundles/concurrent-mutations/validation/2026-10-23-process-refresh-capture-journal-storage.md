# P28: durable refresh capture journal

Status: Pass with follow-up. All 124 distinct selected cases, final portability enforcement,
source/output checks and new resource closure pass. This record does not close public
refresh, Replan or engine E2E.
Behavioral proof tier, following the [recovery design](../plan/P28-process-refresh-recovery.md)
and signed pure protocol `e46625b5af9677ede3bb9a0f93c0139b0822dff9`.

## Owned behavior and boundaries

The runtime reservation and its journal row now commit with the same original budget
allocation. The journal retains the original admission, command, decision generation,
scope, accepted-input fingerprint, grant and exact invocation token. A separate temporary
table retains returned content. The migration adds these two tables; existing runtime,
generation and receipt columns keep their format. The optional typed UOW command is
omitted when null, preserving legacy request serialization.

Only the definitive winner of Begin can invoke the content owner. Reserved work can be
claimed after interruption before Begin. A lost Begin acknowledgement leaves Invoking
unknown: another caller cannot invoke, guess a zero charge or refund its grant. Returned
work is immutable and can be settled after restart without another content call. Root
capture, step capture and local shared-material inheritance use the same protocol.

Cancellation before invocation closes NeverInvoked at zero work. Cancellation after Begin
waits for the original return. Known returned work drains through the existing budget/UOW
owner, including when source authority is subsequently unavailable. Successful publication
still checks current eligibility, material age, funding and scope. An aborted captured
return retains its original work and outcome. Settlement closes the journal, releases the
original grant and removes the temporary payload in the same transaction. A current writer
cannot settle a journal allocation using an old-shaped command that omits that handoff.

Runtime owns pure transitions. Application owns capture orchestration and a separate,
internal known-work settlement collaborator. Its store interface is a real persistence
boundary. Persistence implements narrow journal reads/writes and participates in the
existing runtime UOW; Workbench remains the content owner. Composition adds the store
registration. No new assembly reference, business partial or service locator is needed.

## Concurrency and cost

Begin and Return acquire the existing local root sequence lock and PostgreSQL advisory
lock. The shared key helper preserves the previous byte calculation used by the runtime,
publication, native execution and Workflow dispatch fences. Content capture runs outside
SQL. Begin rechecks the original open allocation, decision generation, quiescence and
run/step eligibility under the fence, so a stale preflight cannot invoke after cancellation.

Journal reads have a five-second operation bound. Writes use the same whole-operation
bound and PostgreSQL transaction/statement/lock limits of 5s/2s/1s. Metadata reads join the
selected row to immutable scope and budget ownership without loading runtime histories or
payloads. Returned material is selected with its metadata in one SQL statement, avoiding
a read/delete race. Payloads are bounded to one MiB of UTF-8; exact UTC ticks preserve
material identity. Foreign keys and phase/outcome checks reject invalid retained shapes,
including PostgreSQL nullable-check pitfalls.

The original UOW still loads/replaces runtime collections for reservation and settlement.
Routing Begin/Return through it would add two more history-dependent rewrites; the narrow
store avoids those. Existing budget commands incur an indexed allocation lookup to reject
journal bypass. This checkpoint proves bounded SQL shape, not sustained throughput or
maximum-history performance. Those capacity gates remain open.

## Candidate and qualification

The initial builds passed, and EF reported no pending model changes. The first PostgreSQL
migration lane passed empty-journal downgrade/upgrade but exposed two failures: EF could
not translate the returned-payload join after a record constructor projection; a test's
raw SQL literal also contained unescaped format braces. The query now performs its joins
before the final projection, retaining one coherent statement. The test literal was fixed.
No database constraint or runtime invariant was weakened.

The initial attempt is retained at
`.artifacts/concurrent-mutations/validation/c4-process-refresh-capture-journal-storage`.
Its three executed lanes were observed; reconciliation found zero remaining observed
processes, zero database-name delta and zero retained temporary entries. The corrected
candidate used `c4-process-refresh-capture-journal-storage-final`, the same isolated PG18
server, and standard Release outputs. All three migration cases and 15 of 16 persistence
cases passed. The remaining root bypass test omitted the existing required scope abort,
so the earlier guard correctly refused it before the new journal boundary. The test now
includes that original scope abort while still omitting journal settlement. No production
code changed in this repair. The final attempt uses `c4-process-refresh-capture-journal-storage-verified`.
Both earlier observers closed with no database, process or temporary-path delta. No extra
database stack was created.

Selection: 35 pure journal and three identity Unit cases; three migration and 16 journal
persistence Integration cases; 26 producer, four boundary, two concurrency, 11 preparation
query and 19 successor Integration cases; five existing publication-fence cases. Expected
total: 124, all discovered on the rebuilt final candidate. A checked source reconstruction
proves the four-line test setup is the only source change since the corrected production
candidate. All production and Unit outputs are byte-identical; only the Integration DLL
and PDB changed. The 38 Unit cases, three migration cases and 14 unaffected persistence
cases retain their prior passing evidence. Both bypass cases and the 67 remaining producer/
query/successor/fence cases pass on the final candidate: 69 current cases plus 55 reused.
Total: **124 passed, 0 failed, 0 skipped and 0 unexecuted selected cases** on the qualified
evidence set.
Build/discovery/execution arrays, the exact repair comparison, TRX results and candidate
hashes are retained with proof; the earlier failed cases remain recorded separately.

The new tests exercise actual registered content producers, lost reservation/Begin/Return
acknowledgements, both cancellation orders, restart without recapture, exact immutable
work, root-lock ordering, metadata SQL, atomic reservation rollback, bypass rejection and
migration downgrade/constraint protection. The old-shaped reservation test uses the
current binary; it is explicitly not an actual older-binary retention proof.

Schema, UOW, store, query or producer edits invalidate this selection. Wider shared-owner
regressions must expand the corresponding gate. This is the stated focused selection;
unrelated or historical failing Stable suites are not relabeled as passing.

## Architecture and final closure

CodeAnalytics `snap-20261023220305-17b44d93` reports 327 scoped documents, 959 types,
7,860 members, the same three project edges and zero cycles. Three added member-count
findings describe the cohesive settlement owner, store and typed row; no business partial
or new project dependency was added. The 189 existing WS0002 package-audit collector
diagnostics remain. Corrected Integration and Unit Release builds passed in 46.636 and
34.560 seconds; the final test-only Integration rebuild passed in 37.319 seconds. Existing
package/analyzer warnings remain, with zero build errors. EF reports no pending model
changes. Source/output verification pins 1,295 inputs and 44 outputs and passes before
and after the final tests.

The final complete portability scan includes the untracked C# files: 10,155 scanned files,
47,433 findings, no truncation. The six added executable-source allowances are generated
designer mappings already present in the model: RootPathState, RootPathSyntax, two
ProcessRecovery indexes, RaisesEscalation and EscalationCount. They introduce no platform
path or process-launch behavior. All additions were reviewed; no stale allowance exists.
The baseline diff contains those six and its timestamp/count, now 15,703. Final enforcement
passes without `--write-baseline`. The 6 portability, 4 secret-tool and 7 rendering-tool
tests, rendering register, documentation and whitespace checks pass.

Each proof root has secret coverage plus supplementary UTF-8/hash/sentinel review of
excluded or oversized files. Three scanner findings per root are verified PowerShell
private-file/variable expressions, not credentials; no unreviewed or credential-sentinel
finding remains. The four reviewed bulk scan JSON/CSV files were removed separately after
resolved-path and hash checks; compact findings, enforcement and cleanup evidence remain.

The final observer covers all seven current test commands. All three test attempts show
zero remaining observed processes, database-name deltas or retained temporary paths. The
recorded commands reconcile with no remaining owned command/compiler process or cleanup
fault. Standard outputs and compact proof remain for further work; the ordinary 5032
instance, existing isolated PG18 fixture and installed tooling are preserved.

## Remaining activation gates

Actual older-binary retention, resolution of genuinely unknown work, explicit authorization
for another attempt after a known abort, durable worker delivery, public refresh/Replan,
native UI and engine E2E remain open. No lease expiry, reservation age or retry count
authorizes recapture. The existing Fail/Pause public contract remains in force.
SPA stays on `codex/concurrent-mutations` for colleague review.

Historical cleanup denials remain unchanged. In particular, automatic approval review
rejected deletion of the prior P27 temporary directory
`C:/Users/lucys/AppData/Local/Temp/candoitall-api-tests/118b26c298ad4d6d9fea358f28134973`
with `blocked by policy`; it was not retried or reported as cleaned.
