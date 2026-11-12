# Original-child stop for Process refresh

Status: Pass with follow-up. P28 owner checkpoint, Behavioral proof tier. Signed base
`7cd54a27356dfd56851dc21812e4c305a6dc4573` qualifies atomic admission. Its committed P28
design defines this owner boundary before source edits. Full refresh is not activated.

## Owned behavior

The registered Workbench adapter exposes separate cancellation and refresh stop contracts.
Both retain the existing original Agent/source/profile checks and delegate to the same
Core stop protocol. Cancellation still requires the durable root cancellation barrier.
Refresh requires its exact retained admission and current original Reconciling generation,
child, material and optional shared scope. Application defines the authority port;
Persistence implements it. Core and its dispatch lease/journal protocol are unchanged.

Each refresh observation uses three indexed metadata queries: exact original receipt,
validated original child/runtime/accounting, and current head/shared scope. The existing
protocol observes authority before signalling, after lease acquisition and immediately
before the journal write; the adapter also checks before reading the Agent. There is no
SQL transaction held across Core work, history scan, material payload read or provider
call. The original canonical child fingerprint is factored into the existing binding
codec, preserving its bytes and keeping both admission and stop checks consistent.

Stopping rejects only undispatched proposal work. Already accepted effects and closed work
remain intact. A busy lease, caller cancellation, missing journal, open work or unresolved
effect cannot be interpreted as successful accounting. The stop path makes no Process,
budget or material mutation: the original grant remains reserved until a separate owner
settles verified work. It does not make a step Ready, capture data or dispatch a successor.

## Qualification selection

Four changed production projects (Application, Persistence, Workbench and Modules.Processes)
and both test assemblies build directly with standard Release outputs and serialized local
library builds. Discovery confirms exactly 73 cases: 30 Unit admission compatibility,
20 new Integration refresh-stop cases, 14 existing Workbench cancellation cases and nine
existing Core inherited-parent-stop cases. Filters, discovered names and exact commands
are retained in `.artifacts/concurrent-mutations/validation/c4-process-refresh-child-stop`.

All 73 selected cases pass across an explicit test-only repair. The first run passed all
30 Unit and 20 new Integration cases, but one existing cancellation assertion expected a
consumed recovery candidate to prove exhaustion. Since the earlier Workflow recovery
change, exhaustion requires a subsequent empty Agent/Workflow page. The corrected test
asserts both cursor steps. Production and Unit sources/outputs are identical across the
repair; only that test source and the Integration DLL/PDB changed. All 43 Integration
cases were rediscovered and rerun successfully in `c4-process-refresh-child-stop-verified`.

The new cases use actual SQL admission and the registered Workbench/Core owner in both
read modes. They cover proposal-only stop and replay through a fresh scope, missing or
different action identity, changed child/material/grant, root cancellation under the lease
and before publication, changed shared/local authority, accepted-before-fence effects,
held lease/caller cancellation, source/profile changes and an actual lost publication
acknowledgement with an unresolved owner receipt. A substituted absent-journal port proves
that the missing owner cannot release the Process grant. Existing Core cases additionally
cover real interrupted work, lost file acknowledgement and cancellation signalling.

## Architecture and static gate

Snapshots `snap-20261023162526-64aa5311` and `snap-20261023164356-64aa5311` cover Application,
Persistence and Workbench. The final scope has 648 documents, 1,824 types and 16,067 members.
The same two scoped project-reference edges and six pre-existing Workbench module/type
cycles remain; no project cycle or new finding is introduced. No project reference,
business partial, generic workflow framework or service locator is added. The collector
retains 189 WS0002 errors, twelve DI0001 informational diagnostics and four DI0002 warnings,
with `hasBlockingErrors=true`; direct builds provide compilation evidence.

The final full portability scan covers 10,109 files and 47,423 findings. No-write enforcement passes
with all 15,697 reviewed allowances unchanged. Static-tool tests, rendering register,
documentation and whitespace checks pass. No UI or migration changes require new browser
or historical-wire proof; the earlier admission format remains unchanged. No broad Stable
run is justified or claimed for this owner boundary.

## Resources and next dependency

The proof root was recorded before creation. Tests reuse isolated PostgreSQL 18 on 53619
and standard build outputs; ordinary 5032, SPA and installed tooling remain untouched.
The candidate pins 1,256 source files and 44 outputs. SPA stays on `codex/concurrent-mutations`
at signed `1139939` for colleague review, without main integration. Historical denied
cleanup targets remain preserved and are not retried by this checkpoint.

Final source/output verification passes. The initial and final resource observations cover
their three executed commands each; the final observation lasts about 525 seconds. Both
reconciliations find zero owned processes, database-name deltas or new temporary entries.
The four owned bulk JSON/CSV scans were removed after path/hash review, retaining their
summaries and enforcement records. Secret reviews cover 94 initial and 77 final text
artifacts with no sentinel, oversized or unreadable files. Each has three reviewed matches
for private variable references in `Execute.ps1`, not stored credentials.

Reconciliation accounting is next. A proposal-only closure must settle exact original
local/root work once while retaining the blocked/reconciling Process. An effect accepted
before the fence must complete its original result and report refresh overtaken. The P28
plan records both current scope-completion guards that must be addressed explicitly before
that behavior is qualified. Capture, successor dispatch, durable delivery, public manual/
automatic activation, UI and whole-engine E2E remain open.
