# Resource baseline capacity harness: authored source

Parent released this source-only harness after the corrected ordinary candidate passed
346/346 with source `862EB393…`, runtime `E9D51402…` and accepted resource closure
`E9FB5746062FF1BCC5A950B90DC5D75B86A28A6DF345C7BC67EEE5D44CC056F6`.
Browser proof remains separate. No capacity execution or shared Resource lock change
is claimed here; P03 remains held.

This implements the finite measurement entry in [2FEC](../plan/P05-R1-stage3-entry-and-probe-readiness.md)
and [02185](../plan/P05-R1-resource-capacity-proof.md). Those design records are retained
unchanged. The only executable source addition is
`tests/Integration/CanDoItAll.Tests.Integration/ResourceMetadataOwnerTests.Performance.cs`.
The existing two partials, private fixture and production owner remain byte-identical.

## Selection and exact workload

The new partial adds one constructor taking xUnit's existing `ITestOutputHelper` and
one Fact with method-level `Category=LongRunning`. There is no class-level exclusion,
skip, dynamic data or new fixture framework. The ordinary class remains 12+6 =18 Facts;
the new method is one separate opt-in row, not an ordinary suite expansion.

```text
FullyQualifiedName=CanDoItAll.Tests.Integration.ResourceMetadataOwnerTests.Measure_bounded_resource_metadata_owner_capacity&Category=LongRunning&Category!=Playwright&Category!=LiveProcess&Category!=Quarantined&Category!=UnixRuntimePortability&RequiresHostDocker!=true
```

The cells are one shared one-writer cell, then independent Projects, same Project with
distinct Resources, and same original Resource/object at8 and32 writers. Three operations
(replacement Save, Delete, complete Promotion), two legal shapes and three samples give
`7 × 3 × 2 × 3 =126` batches. Per operation/shape/sample the cell widths total
`1 +8 +8 +8 +32 +32 +32 =121`; `121 ×3 ×2 ×3 =2,178` calls. No warmup is discarded.

The separately labelled controls make17 instrumented calls: calibration1, explicit-absence
race8, held-Project versus independent Project2, cancellation while held2, three transaction/
cleanup controls3, oversized header-only deletion1. These are excluded from matrix counts.
Original-resource seeding, Project creation, source opening, verification and cleanup are
also outside the dispatch measurements. In a complete run the matrix seeds996 original
Resources (498 for Save and498 for Delete); the oversized control seeds one more. These
setup writes are not hidden as measured calls.

All scopes share one existing `Fixture.CreateAsync` application/database lease. Thirty-two
worker scopes,32 Projects and32 task-owned files are reused. Promotion invokes the actual
`ResourceStorageObjectPromotionService`, browse item activation, storage authorization,
real SQL writer, catalog publication and grant revocation. Its forwarding writer records
the inner owner duration without changing arguments or outcomes. There is no per-cell
container, database lease, host, provider substitute or production strategy flag.

The escaped Webhook shape uses200 name characters,64KiB description, a1000-character
absolute synthetic endpoint and a bounded binary search for the largest accepted escaped
health-path length. Calibration runs the actual preparation and immutable snapshot
constructor, reserves six nonzero fractional digits for both microsecond-precision stored
timestamps, checks the next character refuses, and records the resulting payload sizes
after accepted operations. It does not equate separate field maxima with a jointly valid
1MiB snapshot. Promotion uses actual small local files and a legal200-character escaped
name; it is intentionally smaller. No capability or SQL deadline is enlarged.

## Records and interpretation

Only bounded primitive records go to xUnit output: environment, payload calibration,
labelled controls,126 batch records, post-batch verified-storage records and terminal
counts/cleanup. No raw SQL, parameters, payload, locator, secret reference, connection
string or exception message is emitted by the measurement writer. Parent retains the
ordinary testhost log/TRX under its existing output bound and extracts these JSON records.

Each batch records operation/shape/topology/sample, typed outcome counts, process allocation
and collection-count deltas, private-byte boundary samples, total call distributions,
forwarded Promotion owner durations, per-domain advisory command durations, completion
order, maximum overlap of client-observed post-Project intervals and per-worker counters.
Process allocation includes instrumentation and concurrent process activity. It is not
per-worker attribution or retained memory; bytes/attempt is only a batch quotient. No
forced GC, heap setting, packet collector or diagnostic session is introduced.

Preparation is outside the dispatch window and includes original-resource seed writes
and immutable command construction. Acknowledgement encoding, exact fresh Resource/Search
verification, storage-size reads and cohort deletion run after the dispatch allocation/timer
window. Provider spans and EF events are reported separately and must never be added as
network round trips. Lock durations include wait and command/reader work. Acquisition to
commit acknowledgement or owned connection close is a client interval, not exact server
lock hold; overlapping acknowledgement tails do not disprove exclusive SQL ownership.

The existing options for Resources, Projects and enlisted Search receive the same private
EF observers. Npgsql Activities cover raw lifecycle commands that bypass EF interception.
The first control requires lifecycle, owner transaction, P/R, Resource-write and Search-write
signals; missing observations and the128-record-per-stream bound refuse measurement.
The current installed MAIN assets resolve Npgsql10.0.3. Its
[activity source](https://github.com/npgsql/npgsql/blob/v10.0.3/src/Npgsql/NpgsqlActivitySource.cs)
defines the source/tag shape; actual callback coverage still requires runtime calibration.
The listener reads the exact `db.query.text` tag. Query-less `CONNECT ` activities are
reported separately as physical-open spans, not SQL commands; any other query-less
provider activity refuses measurement. There is no older-tag fallback.
Its [transaction implementation](https://github.com/npgsql/npgsql/blob/v10.0.3/src/Npgsql/NpgsqlTransaction.cs)
also explains why logical/EF transaction events do not establish packet counts.

Fault controls use real unpooled Resources contexts only to force an actual physical
connection disposal callback after the real transaction. They are explicitly labelled;
the measured matrix keeps its registered factory and Npgsql pool settings. Lost
acknowledgement plus cleanup must remain Unconfirmed with one committed effect; acknowledged
cleanup stays Accepted/CleanupPending. A current read is verification of state, never
proof that an earlier caller received acknowledgement. No retry is dispatched.

## P/G/R protocol decision and comparison gate

Parent pinned the future G domain as `resource-promotion:v1:` plus exactly64 uppercase
hexadecimal SHA-256 characters (`Convert.ToHexString` convention). The observer validates
the complete grammar and refuses unknown advisory domains. Version1 denotes lock encoding,
not `ConfigSchemaVersion` or Resource Revision. Future production must use one private
named prefix and the reviewed bounded, unambiguous profile/namespace/Project/lifetime/
plugin/exact canonical-config preimage and fixed GUID encoding. No G production code is
part of this source.

To preserve the exact same harness for the later candidate, G counts are explicit output:
non-promotion calls require zero and promotion observations allow at most one. The parent
**baseline acceptance must require zero G everywhere**. Later candidate acceptance must
require the reviewed exact G count on applicable admitted/accepted Promotion calls, with
pre-G refusals distinguished, and bind it to the actual shared-P source. The broad0..1
observation bound is not itself proof of either protocol. Existing P/R names are unchanged.

Unexpected deadlock/timeout remains a real capacity failure, not an accepted original-state
conflict. A batch may continue after a definite TransactionConflict only after every worker
is terminal and exact retained Resource/Search state is checked. Unexpected refusal,
exception, duplicate effect, unconfirmed result or cleanup diagnostic stops the matrix.
All capacity failures remain in terminal evidence; final success requires126/2,178 and
all cell-specific outcomes. Partial execution is not capacity qualification.

## Cleanup and remaining proof

The dispatch gate and actual task array remain owned through terminal drain, including a
failure before barrier release. Wait controls own their holder disposal exactly once,
cancel/release in finally and await each underlying task before the enclosing contexts
dispose, including failure while creating or opening either context. Completed worker
failures are retained before evidence construction/output, so output failure cannot hide
them. Errors are retained with cleanup failures. There is no waiter-only cleanup
timeout or detached continuation. The15-minute token is cooperative; parent must use an
owned external testhost watchdog and treat forced termination as unproved fixture cleanup.

Files are selected only from the reused fixture's owned bootstrap storage, recorded before
creation and removed individually. Worker scopes, listener and fixture dispose at the
end. Parent independently reconciles the application/database/temp/process resources;
the test's disposal record is not exhaustive cleanup proof.

This checkpoint is authored source, not compilation, discovery, calibration or measurement.
The independent UI architecture peer reviewed the complete initial source and the narrow
successor at `8AA4FCF2ED476D3D3C463134C5485A16B67735763545EE19573C851C93832400`
and reported no remaining concrete source blocker. Source parsing found no syntax
diagnostics; that does not establish reference/API compilation or instrumentation coverage.
Parent still owns standard Release build, full portability-static review/enforcement,
exact1 discovery, immutable source/DLL/PDB provenance, runtime, output extraction and
resource reconciliation. Baseline preservation precedes shared-P implementation. Finite
bursts establish neither sustained throughput nor starvation freedom, production-sized
promotion residual-scan cost, physical I/O or a new operational latency SLA.
