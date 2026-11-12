# Retention implementation candidate

The continuation worktree contains the approved bounded maintenance change. This is a
source review and pre-execution test expectation, not a passing validation record. The
main checkout at `702d0a93b` and all sibling repositories remain outside this edit scope.
An independent read-only review by the runtime agent found no blocking lock-order,
commit-knowledge, eligibility-observation or test-seam defect in this candidate. That
review includes the final disposal-based fault/cancellation seams; it ran no validation.
The UI agent's independent review identified two false health-recovery paths: positive
confirmed progress followed by failure, and no retired receipts observed while retirement
was Busy. Both are repaired in the candidate: recovery now requires a successful Empty
cycle with non-Busy retirement. The same four Unit health Facts include both sequences;
discovery remains 12 Unit rows. This refinement does not change SQL or confirmed totals.

## Owner and transaction changes

Only the Projects owner changes production behavior. No project reference, migration,
writer transaction, receipt codec, browser contract or persisted recovery promise changes.
`ProjectDefinitionOperationOptions` now accepts an interval from 5 through 60 seconds;
the default remains 60 seconds. Warning escalation defaults to five minutes and accepts
twice the interval through one hour. Existing startup option validation covers both.
The two-second cycle budget, 64 batches, 128 rows and 16 MiB per batch remain unchanged.

`ProjectOperationEpochService.AdvanceRetirementAsync` performs a bounded READ COMMITTED
metadata transaction. It checks exact activation, locks the current namespace state
with `FOR UPDATE SKIP LOCKED`, reads at most 128 ordered historical epochs, and advances
only the contiguous Closed prefix past both persisted horizons. The UPDATE repeats the
exact binding and monotonic expected watermark. An unchanged watermark does not issue
an UPDATE. Rotation now acquires lifecycle SHARE, then state UPDATE, then current epoch
UPDATE, and no longer independently rewrites the retirement watermark. Standalone
advancement acquires neither lifecycle SHARE nor an epoch row lock.

Purge reports confirmed row and payload-byte totals from a bounded DELETE RETURNING
aggregate. The eligibility observation is a separate SQL statement after deletion and
metadata cleanup, before commit; it observes the transaction's own deletes and repeats
the exact binding. No eligible receipt, observed eligible receipt and unknown evidence
are distinct. Locked eligible receipts produce contention, not empty work. Eligibility
age uses the observed server timestamp and the selected epoch's two-horizon maximum;
it is not an assertion about the globally oldest receipt.

Results become confirmed only when CommitAsync returns. A narrowly typed purge cleanup
exception carries a confirmed batch if later disposal fails. The concrete cycle keeps
prior committed totals across subsequent failure, cancellation and enclosing service
scope cleanup. An unacknowledged commit is excluded from those totals; they are a lower
bound in that case. No automatic effect replay, new receipt framework or generic job
scheduler was introduced.

The existing background service remains the scheduling owner. One cycle token covers
activation capture, service scope, rotation, retirement and purge. The small shared
cycle implementation also drives the controlled performance harness. Metrics use
constant instruments and finite stop/eligibility tags, without profile, namespace,
operation, caller or project labels. Failure logs contain type/SQLSTATE and confirmed
counts, never the raw exception or payload. Bounded warning state retains only the
current profile/binding and emits a pressure transition and observed recovery. Unknown
evidence cannot clear a warning; capped progress alone is not recovery.

## Expected focused validation

Production project: `src/Modules/CanDoItAll.Modules.Projects/CanDoItAll.Modules.Projects.csproj`.

| Owning assembly/filter | Source-derived rows |
| --- | ---: |
| Unit: `FullyQualifiedName~CanDoItAll.Tests.Unit.ProjectOperationMaintenanceTests` | 12 (8 inline option rows + 4 Facts) |
| Integration: `FullyQualifiedName~CanDoItAll.Tests.Integration.ProjectOperationMaintenanceTests` | 10 (6 Facts + 2 two-row theories) |
| Integration: `FullyQualifiedName~CanDoItAll.Tests.Integration.ProjectOperationEpochTests` | 12 retained rows |
| Opt-in Integration: `FullyQualifiedName~CanDoItAll.Tests.Integration.ProjectStructure.ProjectsProtectedMutationPerformanceTests` | 2 retained LongRunning Facts |

The new database cases cover bounded advancement with a held live epoch SHARE, explicit
state-lock Busy, both retirement/rotation orders, a binding switch between precheck and
mutation for both paths, rollback after receipt deletion, two contending workers,
confirmed totals after budget cancellation and sanitized failure after prior progress.
They reuse the existing epoch fixture, canonical migrations and PostgreSQL lease. No
fixture/server or application instance was launched during preparation.

The retained two-horizon test now exercises standalone advancement. The first new case
asserts the coherent receipt observation contains the committed retirement watermark
while the old bytes remain, the actual receipt reader returns Expired without payload
authorization, and owner admission refuses the original address. The query-failure row
also finishes its remaining receipt through an acknowledged commit followed by an
injected connection-disposal failure, requiring confirmed totals to survive and checking
the actual database through a separate context.

The harness keeps its foreground workload and explicitly labels the revised cleanup
measurement `bounded-observable-retirement-v2`. It records actual configured cadence,
typed cycle stop, confirmed row/byte totals and eligibility. Prior one-minute numbers
remain historical; this source change is not a new capacity measurement.

## Still open

Parent-owned production builds, exact discovery, focused execution and portability
enforcement remain required. No validation was run from this worktree. The finite
cleanup comparison must be repeated. The amendment originally proposed a 10-second
experiment; the parent's subsequent decision specifies **60-second default versus
5-second candidate**, without changing the default. The proposed
[sustained validation card](../plan/P02b-sustained-maintenance-validation.md) fixes
arrival profiles, one/two actual worker processes, unexhausted-backlog evidence,
physical accounting and a separate quiescent vacuum/reuse phase. That harness is
source prepared, unbuilt and unmeasured. Eligible-retention health also does not establish
admission readiness during an expiring epoch.
The supported arrival/storage envelope, connected operational alerting, runtime owner
coverage and final cutover gates remain open. The change supplies observability and
bounded cleanup; it does not impose an exact disk-pressure admission cap or establish
sustainable throughput.
