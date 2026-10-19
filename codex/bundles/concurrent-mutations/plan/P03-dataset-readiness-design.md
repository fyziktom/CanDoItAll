# P03 dataset readiness and interrupted reactivation

Implementation decision, 2026-10-18. This is a planned prerequisite for legacy
installation, not a runtime acceptance claim. It refines
[stopped-host recovery](P03-stopped-host-release-installation.md).

## Ownership and invariants

`DatasetActivationCoordinator` currently validates finite-operation pools. Rotation must
also refuse Awaiting deletion gates and Released gates with unfinished participants.
Refusal after Prepared publication needs a usable repair: both current cleanup and the
offline release command require the previous Active selection.

Projects owns gate interpretation and native retirement. Agent Framework and Workbench
own pending-record interpretation and claim state. Infrastructure owns lifecycle locks
and activation-file publication. Composition constructs explicit-profile adapters and
orders the transaction; it must not query module tables or decode their payloads. Web
parses closed commands and presents safe outcomes. No project-reference edge is added.

CodeAnalytics snapshot `snap-20261018231106-f7be53c3` loaded six projects. Its 189 workspace
errors report existing NuGet vulnerability diagnostics; this is degraded analysis, not a
clean gate. Direct XML inspection found 248 projects, 996 distinct internal edges and no
project-reference cycle. Existing module/type cycles remain separately recorded.

## Exact previous-selection restoration

Introduce an offline `dataset-activation restore-previous` action with the exact profile,
Prepared reactivation plan ID, expected previous namespace and stopped-writer attestation.
It uses an existing-profile read-only composition session, shared with deletion recovery.
The session owns bounded profile/key reads and terminal cleanup; it does not resolve the
ordinary runtime, repair files, migrate, create a database or run a host.

Activation record version 2 retains an optional restoration descriptor containing the
complete abandoned Reactivate plan. Its replacement Plan is an Attach plan with a fresh
plan ID and the abandoned plan's exact previous binding. It preserves namespace,
maintenance identity and protocol. The descriptor is valid only with that attachment,
must have a distinct plan ID, and is rejected on version-1 records. Continue reading
valid version-1 records; new publications use version 2. Older readers must refuse the
new record instead of interpreting restoration as ordinary reactivation.

Under existing activation-file coordination and SQL lifecycle exclusion:

1. Require the exact Prepared Reactivate plan and full Active SQL previous binding.
   Validate installed finite-operation pools; do not assert participant readiness.
2. Persist the version-2 replacement as Prepared, retaining the complete abandoned plan.
   This publication changes no SQL business rows, epoch, namespace or cleanup permission.
3. Terminally release the read transaction, then mark that same attachment Active while
   retaining its restoration descriptor and exact plan identity. All writers remain
   stopped through terminal command cleanup.

On retry, a Prepared or Active replacement is recognized only through its retained
abandoned-plan ID, original expected namespace and exact still-active SQL binding. Resume
the same replacement; never generate another ID. Unknown plans, a different Prepared kind,
changed profile or advanced SQL refuse without altering the current file. Publication
uncertainty remains inspectable and retryable. Ordinary later activation may replace the
restoration record; a stale restore request then refuses.

Restoring local selection does not assert runtime readiness. Unbound legacy work still
prevents normal startup. Offline installation/reconciliation can now operate on the exact
Active selection, followed by ordinary authorized cleanup and a separately requested
reactivation. No callback runs during restoration or release reconciliation.

## Bounded pending inventories

Add a Projects-owned inventory contract implemented by the two native participant owners.
It exposes an unfinished-existence check and keyset pages of exact original preparation,
participant and recovery-ID bindings. It grants no authority and exposes no callback.
Owner adapters reject malformed/oversized payloads, invalid statuses and partial bindings;
completed historical notices are excluded from executable work.

Read pages in the entered explicit-profile transaction, at most 128 recovery-ID headers.
Workbench measures raw JSON sizes in SQL before materialization: at most 2 MiB per record
and 8 MiB total payload per page. Use a bounded materialized header CTE and cumulative byte
budget. An invalid or oversized first row must produce a refusal marker, never a skipped
record or false end-of-data. Deserialize sequentially and retain only exact bindings.

Reuse the Projects completion-row interpreter through a bounded multi-ID read. Do not add
a new interpreter or a query for every pending row. Every unfinished binding must match
the current profile/namespace, supported gate, native retirement or reserved cancellation,
and exact obligation. A missing completion refuses. Valid Awaiting gates permit ordinary
readiness for inspection; existing callback gates still prohibit their effects.

Add owner-owned partial indexes for unfinished Agent work, unfinished Workbench DeleteProject
work and Awaiting Projects gates. Terminal history must not be scanned on a hot mutation
path. Page limits bound transport/allocation, not total history; prove query plans and
capacity with many terminal rows, many small pending records and maximum-size payloads.

## Rotation and proof order

Preflight unfinished work before Prepared publication for diagnostics, then repeat under
the exclusive SQL lifecycle fence before closing the namespace. Awaiting gates or any
unfinished obligation block rotation, including Released-but-unfinished and unknown or
foreign ownership. Terminal Released gates can remain inert in old namespaces. The guard
reads only SQL while holding lifecycle exclusion; it does not acquire source file locks,
call providers or invoke completion, preserving source-before-SQL mutation ordering.

Implement and validate the usable restoration path before enabling this guard. Test exact
retry and interruptions before/after replacement publication and Active publication; a
competing plan; SQL already advanced; unchanged profile/key/business bytes; and readiness
remaining closed for legacy work. Then test inventories across pages and byte bounds,
missing/foreign binding, terminal history, and rotation races. Finally prove the complete
sequence: refused reactivation, exact previous-selection restoration, supported offline
installation/release, ordinary cleanup, then new reactivation.

No expiry, imported history, missing row, current project read or optional no-op adapter
substitutes for original evidence. Standard Release builds, final portability enforcement,
frozen runtime identities and owned-resource reconciliation remain mandatory proof gates.

## Restoration implementation boundaries

Use a separate existing-file restoration lease. Ordinary activation acquisition creates
directories and hardens permissions, so reusing it would silently repair a missing or
invalid offline selection. The restoration lease acquires existing coordination, reads
the bounded record without repair, and compares its exact digest before each replacement.
The pure record selector is independently testable. A narrowly scoped Infrastructure
coordinator checks SQL lifecycle and installed pools, terminally releases the SQL read
transaction, and publishes Active while retaining file coordination. It does not depend
on product modules. A shared explicit-profile composition session owns only key/profile
construction and disposal; both offline commands use it. The Web command branches before
ordinary host construction and migrations. No new project reference or production partial
class is needed.

Restoration integration tests use a separate test class and the extracted existing
activation fixture. They exercise the Infrastructure coordinator directly without the
ordinary activation coordinator or full host on the behavior under test. Existing
activation tests reuse that same fixture; their old scenarios remain intact.

Refusal before publication and uncertainty after publication are distinct outcomes.
An acknowledged Active publication remains restored if terminal cleanup fails, with that
failure reported explicitly. Retry checks the retained original plan and previous full
binding; it never guesses an old namespace from the current SQL row. A bounded operation
deadline and existing connection/command timeouts prevent an offline command from waiting
indefinitely on another writer. Stopped-writer attestation remains required until terminal
cleanup because file publication follows SQL exclusion release.

## Readiness implementation ordering

The restoration foundation passed its focused runtime proof; see
[the architecture gate](../validation/2026-10-18-dataset-restoration-architecture.md).
Construct the Projects dataset guard through a cohesive explicit-profile composition
factory, using the existing transaction and native inventory adapters. Composition must
not decode payloads or query module tables. A registration test must keep the compiled
native participant catalog and inventories complete; a future owner also requires the
corresponding installed-protocol/migration compatibility change.

Preserve the existing activation advisory-lock-before-lifecycle-row order. Do not add a
row-first precheck ahead of `StageActivationAsync`, which would invert that order across
hosts. Infrastructure should expose the guarded activation observation and reuse it in
staging; Composition can inspect the locked current binding, run the SQL-only pending
guard and then stage. Redundant metadata reads on this offline path are preferable to
an inverted lock order or product callbacks in Infrastructure. A resumed plan whose SQL
binding already changed must validate that exact current binding instead of trying to
lock the retired previous selection.

Workbench pages may contain fewer than 128 items because of the byte bound. Continue
from the last returned recovery ID until an empty page; a short page is not end-of-data.
The first invalid/oversized header must be returned as refusal evidence. Completion-row
batch reads must also bound their combined reference/recovery allocation (recovery alone
is at most 65,536 bytes per record), reuse the existing interpreter and detect every
missing requested ID. Keep these costs off the ordinary mutation path.

## Selection before readiness for legacy repair

The 102-case inventory/rotation candidate passes. A separate upgrade review identified
an initial-selection trap: validating native pending work before Bootstrap commits rolls
back the first SQL identity; validating it before Attach publishes Active leaves a
Prepared attachment. Neither state permits the existing offline installer/reconciler to
observe an Active selection. Previous-selection restoration only covers Reactivate.

Keep selection and runtime readiness separate, as restoration already does. Within
`ApplyAsync`, retain the complete finite-pool audit and all locked Reactivate pending
checks. Commit the exact SQL selection and publish its Active file, then run the existing
readiness check before returning. Bootstrap/Attach can therefore establish a repairable
selection while ordinary startup still refuses unbound work. Retry retains the same
plan/namespace. No new recovery command, boolean bypass, publication lease or host graph
is needed. Unknown finite history and incompatible installed protocols still refuse
before committing selection. Test fresh and interrupted Bootstrap and Attach with
unchanged legacy native rows and continued readiness refusal.

The next performance proof captures actual adapter commands with their parameters for
both first and subsequent pages, rather than relying solely on equivalent existence and
header probes. Use explicit SQL `Id > cursor` when a cursor exists, with a separate
first-page form. This avoids a comparison expression or nullable-parameter branch hiding
the range predicate from a generic PostgreSQL plan. Assert the partial index and range
condition with substantial terminal history. Payload fetch must remain bounded to the
selected Workbench headers; inspect the actual join plan as well.

The expanded plan proof found that PostgreSQL chooses a sequential scan for plain
`EXISTS` when 131 records are unfinished among 10,000 terminal records. Its measured
execution was fast because the first matching row was at the front; that does not
provide a history-independent bound. Replace each existence probe with a count over
an ordered subquery limited to one indexed identity. Count never examines more than
that one result. Keep the partial predicate and order key together. The next proof
places unfinished rows after terminal history and captures all seven actual commands:
two native existence probes, four first/subsequent pages and the Projects Awaiting
probe. Do not disable sequential scans or change planner settings to make proof pass.
