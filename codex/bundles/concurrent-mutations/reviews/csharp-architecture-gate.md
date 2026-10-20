# C# architecture entry review

## Process capture ownership closure (2026-10-20)

Status: PASS for the commit/claim prerequisite. [Validation](../validation/2026-10-20-process-capture-ownership.md)
has 127 distinct passing behaviors and unchanged production source/outputs across the
test repair. A Runtime contract and typed refusal carry exact identities; the cohesive
Persistence guard validates already-loaded claim state and delegates to the existing
transaction owner. No interface, partial, project reference or DI service was added.
Architecture retains six edges, zero cycles and 352 findings, with no added advisory.
Existing workspace diagnostics remain explicit.

The failed SQL candidate reopened the initial RunSnapshot ordering assumption.
The accepted correction keeps root capture before dispatch claims; step capture uses
the exact claim guard. The positive SQL flows prove both boundaries, and the negative
cases preserve original reservations and roll back flushed material on expiry.
Actual registered producers and manager continuation remain separate open gates.

## Process result/accounting integration closure (2026-10-20)

Status: PASS for this checkpoint only. [Validation](../validation/2026-10-20-process-result-accounting.md)
records 369 distinct passing behaviors under the exact repair comparison, including
actual PostgreSQL compound rollback, competing coordinators and legacy round trips.
Dispatcher and recovery require the independently testable Application coordinator;
Runtime owns pure validation/transitions and Persistence owns bounded projections,
receipt serialization and atomic acceptance. No project reference, runtime partial,
service locator or rendering responsibility was added.

Scoped architecture snapshots retain six project edges and zero cycles. Two added
member-count advisories were reviewed: the immutable selection and the single-purpose
result coordinator. Existing workspace diagnostics remain explicit. Persisted-state
acceptance reuses the transaction's existing reload, and child selection is bounded to
four metadata queries. No model or external-owner await runs under the root transaction.
Producer capture, manager continuation, Workflow, settings and sustained capacity are
separate remaining gates. Historical entry conclusions below retain their original scope.

Result: Pass for architecture direction and P01a only. Protected-owner/runtime rollout
is not yet implementation-ready as a whole; its remaining gates stay in the execution
plan and owner/policy reviews.

Independent persistence, agent/process and UI/validation investigations read exact
current sources. They agree on owner-local conditional commands, separate authority and
data freshness, existing renderer/runtime seams, no global coordinator, and no lock
held across human/model/provider work. The first cleanup repair adds no reference,
partial class, service locator or abstraction. Direct failure and successor-acquisition
tests can prove it without constructing the application runtime.

Current baseline Infrastructure Release build succeeded using normal settings; existing
NU1902/NU1903 dependency warnings remain recorded. CodeAnalytics snapshots loaded scoped
source but have workspace diagnostics, so no complete healthy-graph claim is made.

The larger Projects design needs exact wire/retention/lifecycle decisions and caller
conversion; the planning publication needs its complete material-writer perimeter.
These do not change the correctness of finally-releasing an already-owned semaphore.
P01a may proceed after the design commit. Its closure needs affected build, exact
discovery/results, portability enforcement and independent diff review.

## P01a closure review

Status: Pass. Independent review of the two changed source/test files found no
blocking issues. The implementation retains the existing cohesive Infrastructure
helper with no new references, partials, interfaces or service location. Its six
focused cases passed, including real PostgreSQL transaction-disposal faults and
independent same-key successor acquisition. Infrastructure Release and final
portability enforcement passed. See [validation](../validation/P01a-cleanup.md).
This conclusion does not extend to commit knowledge, owner CAS or durable replay.
