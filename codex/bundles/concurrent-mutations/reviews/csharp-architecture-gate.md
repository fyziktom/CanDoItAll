# C# architecture entry review

## Original requirements and execution clock (2026-10-21)

The [qualified scope](../validation/2026-10-21-original-step-requirements.md) preserves
the six Process project references and zero scoped cycles. Application owns typed
requirements, Persistence owns indexed capture/versioning, and the additive migration
widens only its payload-version constraint. The generated designer and immutable
contract member counts account for the three new advisories.

The workspace factory forwards its registered TimeProvider; the existing execution
owner uses it consistently with the journal. Core's before/after snapshots add only
one field, no owner, partial, dependency or finding. All 29 affected scenarios pass.
Existing collector diagnostics remain disclosed; direct affected Release builds pass.

## Core startup accounting review (2026-10-20)

The [189-case accepted scope](../validation/2026-10-20-inherited-startup-accounting.md)
keeps initial admission, live child work and workspace preparation with their existing
Core owners. There is no new project edge or runtime partial. Snapshot
`snap-20261021003627-0b4ceac4` has one Core-to-Models edge, zero project cycles and
the same three type cycles. The small coordinator's member-count advisory is accepted.
The registered workspace is exercised; its fixture now explicitly replaces its test
participant instead of registering a duplicate owner. A remaining-time setup/cancellation
boundary found during review must close before activation; its accepted correction
uses the existing timer and settlement path and adds deterministic regression coverage.

## Selected step source and producer closure (2026-10-20)

Status: PASS for selected preparation/enrollment, with [187 distinct passing behaviors](../validation/2026-10-20-process-selected-step-producer.md).
Application owns one capture/enrollment lifecycle; Persistence owns bounded original
selection and the assignment/claim transaction fence; Workbench owns coherent material
capture. Root and step share one genuine business capture port. No new project
reference, runtime partial or service locator. No Process transaction spans capture.

Snapshot `snap-20261020235359-77361fd7` retains seven project edges, zero project cycles
and the same six existing Workbench module/type cycles. Four informational member-count
findings are accepted for the immutable selection, deterministic identities and typed
enum, and cohesive lifecycle coordinator. The exact test repair changes no production
source or output. Initial Core accounting, actual business binding, current manager
authority and allocation-capacity qualification remain required before activation.

## Initial root material producer closure (2026-10-20)

Status: PASS for initial shared capture, with [192 distinct passing behaviors](../validation/2026-10-20-process-root-material-producer.md).
Application owns capture identity/lifecycle through real persistence and business ports;
Persistence owns the bounded original header and validated scope; Workbench owns coherent
Project reads and source leases. The dispatcher delegates before claim admission.
No project reference, runtime partial or service locator was added. No Process lock spans
the business read. Current snapshot readiness does not replace child/publication authority.

Before/after scoped reviews preserve seven project edges and six existing Workbench
module/type cycles, with no project cycle. Two new informational member-count findings
are accepted for cohesive attempt/receipt and capture-lifecycle responsibilities.
Registered composition and actual source/capture tests validate wiring that the static
DI interpreter does not fully recognize. Two test classes were corrected/extended without
production changes; 2,324 production outputs are identical across that repair.
Actual child binding, manager continuation and post-capture revocation remain activation gates.

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

## Closed startup failure checkpoint

Status: Pass with follow-up; executor activation remains gated.

Core owns completion/cleanup measurement and sealed live evidence. Workbench matches the
original product identities; Process Application carries opaque typed owner evidence,
and Persistence owns exact receipt replay and atomic settlement. No runtime partial,
project reference, table, migration or successful-path write was added.
Snapshots `snap-20261021045212-77e0a53d` and `snap-20261021052732-61ea7d49`
retain nine project edges and nine existing cycles. Two informational member-count
findings cover the immutable attempt evidence and cohesive admission owner; existing
analyzer diagnostics preclude a completely healthy graph claim.

The [validation record](../validation/2026-10-21-closed-startup-failure.md) qualifies
202 behaviors with direct Release builds, static enforcement, unchanged-production
test corrections and runtime/resource proof. Pure outcome matching remains a direct
static seam; add isolated negative unit cases before executor activation. Existing
integration negatives additionally test real capability issuance and persistence.
Capacity implementation may proceed independently within Persistence. This review does
not approve the remaining adapter, manager or Workflow activation.

The [capacity checkpoint](../validation/2026-10-21-process-capacity-bounds.md) subsequently
passes all 15 isolated startup-outcome tests, closing that testability follow-up. Its
Persistence-only byte bounds add no references, cycles or analyzer findings. All 179
selected cases pass. Finite root completion passes, while serialization allocation cost
requires the documented buffer correction. Ordinary authority also needs the original
policy predicate correction found during adapter review. Executor activation stays gated.

## Pooled preparation and original-policy correction

Status: Pass with follow-up. The [validation record](../validation/2026-10-21-process-capacity-repair.md)
qualifies 183 affected behaviors with retained initial failures and an exact nine-case
test-only repair. Production identities remain unchanged across the correction.
Snapshots `snap-20261021062953-77361fd7` and `snap-20261021070241-77361fd7`
retain seven project edges and six existing cycles. The sole new informational finding
counts required private Stream members; it is not a new domain responsibility.
Persistence retains serialization and source-authority ownership, with no new query,
schema, reference or runtime partial. Warm allocation improves; cold cost and concurrent
working-set capacity remain explicit. Manager/adapter and broader activation are not closed.

## Manager review admission

Status: Pass with follow-up. The [74-case qualified checkpoint](../validation/2026-10-21-manager-review-admission.md)
keeps exact review selection and live-work authority in Core, with enlisted Project
publication in Workbench/Process Persistence. Scoped before/after snapshots retain the
same five project edges and nine existing cycles. The added informational member-count
finding belongs to the 67-line continuation contract; it does not introduce another
runtime responsibility. No new reference, interface, schema or partial is added.
Pure tests exercise capability identity without the runtime, and actual MAF/PostgreSQL
tests prove publication after claim closure. Test-only corrections preserve production
identity. Waiting completion, current manager authorization and executor activation
remain explicit follow-ups.

## Waiting-result completion and publication context

Status: Pass with follow-up. The [final 331-case candidate](../validation/2026-10-21-process-review-completion.md)
proves actual inherited Project publication, exact waiting-result completion, root
fences, atomic accounting, rollback, restart and ordinary-result regressions.
Runtime owns the independent terminal transition; Application reads actual child
outcomes and derives replay identity; Persistence enforces the existing root transaction.
Shared event and terminal-state behavior is removed from the Runtime Engine helpers.
No new Runtime partial or project reference is introduced.

Process snapshots `snap-20261021090234-c953f4c6` and `snap-20261021112602-b5477474`
retain six scoped project edges, zero scoped cycles and 295 findings. Core snapshots
`snap-20261021103047-8e94da1d` and `snap-20261021123318-8e94da1d` retain all
784 types, 5,823 members and 225 findings. The publication correction removes an
invalid catalog/context comparison while preserving exact original metadata.
The 189 existing collector diagnostics remain a limitation; direct builds pass.

Pure Runtime/Application tests exercise the new owners independently of a running
Agent. Real MAF/PostgreSQL cases exercise produced children and their original
receipts. All earlier fixture, publication and observer failures remain retained.
Current manager authority, durable choice intent, parent reconciliation and actual
executor activation remain required under the [manager design](../plan/P07-current-manager-actions.md).
