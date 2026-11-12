# Owner design cross-review and first implementation gate

Reviewed 2026-10-16 against architecture README, owner design 01, performance/risk
plan 04, and current persistence/deletion source. This review permits the narrow
foundation cleanup slice below; it does not certify the full concurrent-mutation
design or any implementation as tested.

## Decision: proceed with bounded cleanup slice

The first source slice is restricted to
`src/Foundation/CanDoItAll.Infrastructure/Persistence/SerializableMutationScope.cs`:

1. In relational `BeginAsync` failure cleanup, release every acquired process
   semaphore in a `finally`, even if transaction disposal throws.
2. In `DisposeAsync`, release the scope's process semaphores in a `finally`, even
   if transaction disposal throws. Keep the existing once-only disposal behavior.

Do not change isolation level, process stripe count/hash/key mapping, advisory SQL,
scope-key ordering, authorized-source branching, commit behavior, retry behavior,
or production service registrations. Do not add a global coordinator, interface,
constructor overload, reflection seam, or production-only-for-tests callback.
This is an independently justified liveness defect; it does not need the Projects
receipt or aggregate-stamp design to be implemented first.

The existing transaction is still disposed before releasing local gates. The
fix makes release unconditional; it does not claim the database resource was
successfully released when disposal failed. Callers still receive that failure,
and the owning DbContext/connection remains subject to its normal cleanup.

### Exception semantics

Preserve the original exception unchanged when `BeginAsync` fails and cleanup
succeeds. If initialization and transaction cleanup both fail, preserve both
using a standard `AggregateException`, with the initialization failure first and
cleanup failure second. This retains original exception objects and stacks without
string-keyed exception Data, hidden logging-only evidence, or a new exception
abstraction. Use a safe fixed message that includes no connection string or keys.

For ordinary `DisposeAsync`, propagate the transaction-disposal exception after
finally release. A repeated dispose must return without another release or another
transaction disposal. Do not catch and silently discard cleanup failure.

This slice cannot preserve an arbitrary caller-body exception that is subsequently
masked by C# `await using` disposal: `DisposeAsync` has no access to that exception.
Do not claim it solves all commit knowledge. Owner-level outcome handling and
`ProjectSourceMutationScope` postcommit cleanup remain a later dedicated slice.

The aggregate case is not a new automatic-retry signal. Existing conflict helpers
can find the first inner exception; callers must not infer that cleanup completed
from a conflict classification. No retry policy changes belong in this slice.

## Direct fault-injection seam

Prefer a test-only EF service replacement of the public
`IRelationalTransactionFactory`, using
`DbContextOptionsBuilder.ReplaceService<IRelationalTransactionFactory, ...>()`.
The replacement derives from `RelationalTransactionFactory` and creates a small
test transaction derived from public `RelationalTransaction`. Override
`DisposeAsync` to await `base.DisposeAsync()` and then throw a dedicated sentinel
exception once. Forward the connection, DbTransaction, transaction ID, logger,
ownership flag, and `Dependencies.SqlGenerationHelper` from `Create`.

These are the installed EF 10.0.4 public provider-extension seams, verified in
the local package XML. Keep the factory stateless/thread-safe: EF documents its
lifetime as singleton. Keep throw-once state on each created transaction, not in
mutable static test configuration. Use distinct test factory types/options if
cases need different behaviors.

`DbTransactionInterceptor` is useful for transaction start/failure observations,
but it has no disposal interception callback in this installed version.
`RelationalEventId.TransactionDisposed` is a diagnostic event, not an override
on that interceptor. Do not implement a nonexistent `TransactionDisposedAsync`.
Wrapping `DbTransaction` in a generic decorator is also less suitable because
provider command code may require its native transaction type. Replacing the EF
wrapper preserves the underlying Npgsql transaction.

Trigger the `BeginAsync` primary failure after its local transaction variable has
been assigned: a test `DbCommandInterceptor.NonQueryExecutingAsync` throws an
initialization sentinel when the expected `pg_advisory_xact_lock` statement is
issued. Failing `TransactionStartedAsync` itself can throw before the scope gets
the returned transaction, exercising a different ownership path.

Use the existing explicitly isolated PostgreSQL 18 fixture and an ordinary
DbContext with Npgsql options plus the test service replacement. No product rows
are required for this lock/disposal regression. Interceptors/factory replacements
must be limited to these test options and never registered in the product host.

### Meaningful assertions

Required cases:

| Case | Assertion |
| --- | --- |
| Successful Begin then failing Dispose | Same disposal sentinel escapes; all scope keys are acquirable by a subsequent contender |
| Failing advisory acquisition and successful Dispose | Exact primary sentinel escapes unchanged; all acquired process stripes release |
| Failing advisory acquisition and failing Dispose | Aggregate contains original primary and cleanup sentinel objects in order; process stripes release |
| Dispose twice after a cleanup failure | Second disposal does not dispose/release twice; while a successor owns the same key, another contender remains blocked |

For a direct test of the process semaphore, use an explicit InMemory contender
with the same scope key after the relational failure. The static stripes are
shared across the current helper's provider branches, so this tests the actual
observable lock contract without reflection. It also prevents an unrelated SQL
lock/connection failure from hiding whether the process stripe was released.
The throwing wrapper should still dispose the real transaction first so fixture
cleanup does not intentionally leak PostgreSQL locks.

Use at least two selected scope keys when checking multi-lock release, and obtain
them through the public API. Do not inspect the private stripe array or manufacture
reflection-based semaphore counts. The test can deterministically choose keys
whose documented current hash calculation yields different stripes if needed;
that is test setup for the existing mechanism, not a new production key contract.

Bound contention awaits with cancellation/deadlines and release every successfully
acquired test scope in finally. Avoid timing-only claims that a waiter was active:
hold the successor scope explicitly, start the contender, and require its bounded
acquisition to cancel while the holder remains alive. Then release and require
successful acquisition. Record exact discovery counts before test execution.

Build Infrastructure, refresh the owning test assembly, run the narrow new
regression filter plus the existing InMemory multi-project scope case, and run
the mandatory portability-static review/enforcement. Broader transaction/source
tests belong to the frozen shared-persistence checkpoint or a named invalidation
trigger; no broad repeated suite is required for each edit.

## Delete precondition decision

The architecture currently defines updates more completely than destructive
deletion. Current `ProjectsService.DeleteAsync` accepts optional lifecycle
admission; the HTTP endpoint supplies none. Its participants can prepare/delete
current dependent records without proving what the caller reviewed. A definition
stamp alone does not cover Workbench/CRM/resources written after a deletion
preview.

Decision for the protected Projects perimeter:

- Ordinary interactive/API/agent delete requires the original project admission
  and definition expectation. No nullable existing-ID bypass remains enabled.
- Destructive confirmation binds an explicit owner-generated deletion plan or
  witness covering the affected owner closure and incident relationships that
  the UI/tool presented. Revalidate it under the existing complete deletion key
  set and enlisted participants before any effect. New material dependent rows,
  changed owned rows, or changed incident links invalidate that plan.
- Each participant owns its witness semantics. Append-only history that is
  deliberately retained may be excluded; do not make every log entry invalidate
  deletion. Exclusion is explicit in the participant policy and user-facing
  deletion scope.
- Reconfirmation after a conflict obtains a new plan and operation identity.
  A new snapshot is not silently substituted into the existing confirmation.
- An idempotent retry of a committed deletion uses its original receipt/recovery
  identity, even if the public ID was recreated. Cleanup retries address exact
  existing participant recovery IDs, never a newly selected project lifetime.
- Existing creation compensation remains a distinct narrow inverse with its
  exact lifetime/reservation/fingerprint and participant evidence; it does not
  bypass ordinary delete expectations.

This is a separate implementation slice. If the full deletion-plan witness cannot
be completed with definition editing, protected release must explicitly disable
the unconverted destructive route or clearly keep that owner slice incomplete.
Do not add a universal project stamp that every owner must increment to avoid
designing the actual deletion closure.

## Finite receipt horizon decision

Choose a finite, advertised operation-admission/replay contract, not unbounded
receipt growth and not arbitrary deletion after a chosen number of days. The
deployment-specific durations are validated options selected from the maximum
supported disconnected client and retained-checkpoint retry horizon. No numeric
default is asserted by this architecture review.

Use a server-published, durable operation admission epoch/generation separate
from the dataset activation namespace. Its identifier is immutable and retained
in every original command and semantic digest. It is not a permission token.
Normal host restart preserves it. A new epoch can admit new work while an older
epoch permits reconciliation for its already admitted work.

The minimal lifecycle is:

1. Open epoch admits new operation identities up to its explicit deadline.
2. Closed epoch refuses to execute a missing operation identity. Existing receipt
   lookup/replay remains available while its advertised detail/dedupe retention
   applies. No response of "not found" becomes permission to execute again.
3. Detailed acknowledgements expire according to the declared result horizon.
   Compact identity/digest evidence remains until the epoch is durably closed
   and the required outcome retention is complete.
4. After compact evidence is purged, the durable retired-epoch high-water mark
   still rejects old requests. Epoch identifiers are never reused or reopened.

Maintain monotonically retired generations so replay denial does not itself need
one permanent tombstone per operation. Validate epochs through the owning
protocol; a client cannot make an old request new by refreshing only its epoch.
That is a new intention and requires re-admission, new operation identity, current
expectations, and any applicable approval. Paused workflows keep their original
epoch and receive an explicit expired-operation/review outcome after the horizon.
They do not silently extend it on resume.

Existing committed receipt lookup takes precedence over checking whether the
original expectation is now stale. On a missing row, a closed/retired epoch must
fail without applying any effect. Handle an in-flight pre-closure transaction as
pending/unconfirmed; it may still commit its receipt, but a later missing-row
retry cannot start another effect. Keep transaction deadlines bounded and retain
newly committed detail for its full advertised period; garbage collection does
not infer age solely from the epoch's opening time.

Epoch publication/closure and admission must have a proven atomic boundary.
Prefer a small shared/read epoch guard that only epoch maintenance takes
exclusively, or an equivalent explicit database protocol; it permits concurrent
owner writes and must not become a global exclusive write queue. Measure the
extra lookup/guard cost. Do not use a mutable unvalidated cache for closed-state
admission. Immutable epoch metadata may be cached within its proven validity.

This mechanism requires a separate implementation/recovery test and is not part
of foundation cleanup. Until it ships, do not prune committed receipt identities
or advertise finite safe replay retention. A temporary implementation may retain
all receipts while collecting capacity evidence, provided documentation reports
that retention closure is incomplete.

The dataset activation namespace remains a different fence: destructive restore
can roll back the epoch table and retired watermark too. Activation must advance
the namespace outside restored history or perform the reviewed irreversible
retirement procedure before protected writes reopen. Receipt epochs alone do not
solve restore ABA.

## Cross-review disposition and remaining gates

GO for the two-method foundation cleanup with the tests above. No dependency,
schema, isolation, lock ordering, or product-policy change is necessary.

The README/01/04 direction is coherent: narrow owner revisions, explicit command
shape, owner-local receipts, actual SQL enlistment, no locks during thinking, and
measured costs. Keep the scalar adapter data-loss repair and optional blank-row
behavior tests visible alongside CAS; they are independent of token plumbing.

Before protected Projects rollout, resolve and implement deletion witnesses,
receipt epoch lifecycle, trusted principal/operation scope, dataset activation,
request/result limits backed by retained data, and the measured search choice.
Those items do not block the narrow cleanup commit, but they do block broad
claims of complete end-to-end concurrent mutation safety.

No production edits, builds, tests, or commits were performed by this review.
