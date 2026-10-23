# C# architecture entry review

## C# Architecture Gate Result: registered refresh capture producer (2026-10-23)

Status: Pass with follow-up for [the capture producer](../validation/2026-10-23-process-refresh-capture-producer.md).

### Findings

| Severity | Finding | Evidence | Required action |
| --- | --- | --- | --- |
| Reviewed | Three member-count findings on the attempt, coordinator and refusal enum | `snap-20261023201727-17b44d93`; 18/11/9 source members, one responsibility each | Retain cohesive original-attempt data and one capture lifecycle; no partial splitting |
| Existing | 189 WS0002 collector diagnostics | Unchanged before/after; direct Runtime/Application/Persistence/module builds pass | Preserve the scoped-analysis limitation |
| Follow-up | Unknown invocation and explicit retry are not qualified | Retained grants stay pending; no public action or worker added | Follow the invocation/returned-work design before recovery or Replan activation |

### Dependency direction

Application reserves, invokes its existing content interface and settles known work through
existing query/UOW boundaries. Persistence owns coherent metadata reads and mutation guards;
Workbench remains the content owner. The same three project edges and zero scoped cycles
remain from `snap-20261023192129-17b44d93`. No project reference, schema, service locator or
provider callback inside a Process SQL transaction was added. Composition registers one
scoped coordinator at the existing module boundary.

### Partial-class policy

No business partial was added. The immutable attempt owns original identities/grant and
preflight, the receipt validator owns stage evidence, and the coordinator owns one lifecycle.
Typed UnclosedWork distinguishes retryable quiescence refusal from corrupt funding; existing
domain accounting and successor owners still enforce the actual changes.

### Testability proof

All 65 distinct selected cases pass: 3 unchanged pure identity cases plus 62 Integration
cases on the corrected candidate. Production/Unit hashes are identical across the test-only
interleaving repair. Real registered capture and scheduler enrollment cover both modes.
Competing callers, lost acknowledgements, expiry/overrun, exception/cancellation, restart,
capacity/receipt boundaries and real read/write interleaving have positive and negative
proof. The nineteen successor regressions supply the dependent-flow gate. Full portability,
source/output verification and new resource reconciliation pass.

### Closure decision

Known capture production may support the next recovery boundary. Completed root inheritance
is a qualified restart path; a retained reservation alone does not prove invocation or
closed work. The [next design](../plan/P28-process-refresh-recovery.md) separates first
invocation, returned outcome, unknown work and explicit retry. Public refresh, automatic
replan, durable delivery and whole-engine E2E remain open. SPA stays on its colleague-review
branch; historical cleanup denials remain unchanged.

## C# Architecture Gate Result: refresh capture queries (2026-10-23)

Status: Pass with follow-up for [capture preparation/progress](../validation/2026-10-23-process-refresh-capture-query.md).

### Findings

| Severity | Finding | Evidence | Required action |
| --- | --- | --- | --- |
| Reviewed | Two member-count findings on immutable identity/selection records | `snap-20261023192129-17b44d93`; each has one cohesive responsibility | Retain typed stage identities and the original funding selection |
| Existing | 189 WS0002 collector diagnostics | Unchanged before/after; direct production/test builds pass | Preserve the scoped-analysis limitation |
| Follow-up | Queries do not invoke capture or establish durable recovery | P28 producer entry and failure decisions | Qualify interleaved reads, actual capture and closed/unknown-work recovery before activation |

### Dependency direction

Application owns one genuine read boundary; Persistence supplies bounded metadata and
reuses existing original-input, child, root and reconciliation owners. The Process module
registers the query. The same three project edges and zero scoped cycles remain between
`snap-20261023182501-fb03b32e` and `snap-20261023192129-17b44d93`. No project reference,
schema, service locator or independent write transaction was added.

### Partial-class policy

No business partial or broad manager was introduced. The identity type owns one stable
identity domain, the query owns reads, and the shared internal reconciliation reader removes
the successor's duplicate receipt verification. Existing Runtime behavior remains unchanged.

### Testability proof

All 33 selected cases pass: 3 pure identity, 11 actual PostgreSQL query and 19 successor
regressions. Tests reject missing accounting and changed identities/ceilings, preserve both
funding owners, observe outstanding work and verify one progress SQL statement against only
the idempotency table. The successor dependency still proves capture-backed advance, replay,
rollback, cancellation and next-attempt enrollment for both modes. Full portability,
source/output verification, secret coverage and new resource reconciliation pass.

### Closure decision

The capture producer may use the preparation query and stable stage identities. The read
contract is advisory until mutation expectations are checked under the root fence; progress
does not authorize recapture. Public refresh, automatic replan, durable interruption recovery
and whole-engine E2E remain open. SPA remains on the colleague-review branch; previous cleanup
denials are unchanged.

## C# Architecture Gate Result: original refresh successor (2026-10-23)

Status: Pass with follow-up for [the successor boundary](../validation/2026-10-23-process-refresh-successor.md).

### Findings

| Severity | Finding | Evidence | Required action |
| --- | --- | --- | --- |
| Existing | Collector retains 189 WS0002 errors | Unchanged before/after diagnostics; direct Release builds pass | Preserve this scoped-analysis limitation |
| Follow-up | Durable capture orchestration and interrupted-work recovery are not activated | P28 capture preparation/failure decisions | Qualify the result owner, recovery and delivery before public refresh/Replan |

### Dependency direction

Runtime owns deterministic successor preparation and exact mutation validation. Persistence
checks original receipts, current allocation and scope evidence under the existing root
transaction. Application and Workbench keep their existing contracts. Snapshots
`snap-20261023173045-65134217` and `snap-20261023182501-fb03b32e` retain the three intended
project edges, zero scoped cycles and the same 236 findings. No project reference, schema,
new interface, service locator or external callback inside the Process transaction was added.

### Partial-class policy

No business partial was added. The UOW delegates the new guard to one cohesive persistence
owner. The pure Runtime record does not require the runtime engine, database or provider.

### Testability proof

All 129 selected cases pass across verified test-only repairs/additions: 67 Unit and 62
SQL cases. Actual material capture and scheduler enrollment cover both snapshot modes.
Replay, concurrent commits, rollback, cancellation, restart after lost acknowledgement,
forged mutation/allocation and root-quiescence cases exercise the real commit owner.
The shared-root negative and independent-step positive distinguish a necessary shared
barrier from unnecessary blocking of independent snapshots. No throughput claim is made.
Final portability enforcement, source/output identity, secret coverage and new resource
reconciliation pass; reviewed allowances are unchanged.

### Closure decision

The production capture owner may depend on this guarded successor. The test helper's
capture orchestration is supporting consumer evidence, not a shipped durable producer.
P28 delivery/recovery and whole-engine E2E remain open; SPA remains on its colleague-review
branch and historical cleanup denials remain recorded.

## C# Architecture Gate Result: original refresh accounting (2026-10-23)

Status: Pass with follow-up for [the accounting boundary](../validation/2026-10-23-process-refresh-accounting.md).

### Findings

| Severity | Finding | Evidence | Required action |
| --- | --- | --- | --- |
| Existing | Collector retains 189 WS0002 diagnostics | Before/after snapshots; direct Release builds pass | Keep the collector limitation explicit |
| Follow-up | Capture, successor and delivery are not activated | P28 successor/capture decisions | Qualify those owners before public refresh/Replan |

### Dependency direction

Runtime owns exact mutation rules; Application owns preparation, orchestration and receipt
interpretation; Persistence rechecks the original evidence under its existing root fence.
No Agent callback runs inside SQL. Snapshots `snap-20261023170919-65134217` and
`snap-20261023173045-65134217` retain three intended project edges, zero scoped cycles and
no new finding. No project reference, service locator or extra transaction owner was added.

### Partial-class policy

No business partial was added. The small accounting preparation, receipt and persistence
guard each own a separate contract; the existing terminal-only coordinator stays unchanged.

### Testability proof

All 275 selected cases pass: 210 unchanged Unit cases and 65 final SQL cases after a verified
test-only cost assertion repair. Registered Workbench/Core owners exercise both snapshot
modes, actual accepted/unknown effects, competing continuations, rollback, cancellation,
lost SQL acknowledgement and full host restart. Pure negative tests reject substituted
input, history, claims, result, actor and projection. Final full portability passes with
15,697 reviewed allowances unchanged; source/output and new resource checks pass.

### Closure decision

The successor/capture checkpoint may depend on original accounting. These tests do not
qualify public refresh, automatic delivery or whole-engine E2E. Historical cleanup denials
remain recorded, and SPA remains on its dedicated branch for colleague review.

## C# Architecture Gate Result: original-child refresh stop (2026-10-23)

Status: Pass with follow-up for [the qualified stop boundary](../validation/2026-10-23-process-refresh-child-stop.md).
Application declares two genuine owner/IO ports. Persistence checks exact durable refresh
authority with three indexed metadata queries per observation. Workbench selects the
correct cancellation or refresh authority before sharing existing mechanics; Core retains
its original dispatch lease and journal protocol. No SQL transaction spans child work.

Snapshots `snap-20261023162526-64aa5311` and `snap-20261023164356-64aa5311` retain the same
two scoped project edges and six existing Workbench module/type cycles, with no project
cycle or new finding. Collector limitations remain 189 WS0002 errors, twelve DI0001
messages and four DI0002 warnings. Direct affected Release builds pass. No project
reference, business partial, service locator or generic orchestration layer was added.

All 73 selected cases pass across the documented test-only repair. SQL and registered
Workbench/Core proof cover both read modes, rejected/changed authority, original-source
and profile races, actual leases, root cancellation, accepted and uncertain effects,
missing journals and unchanged reserved grants. Final portability, candidate identity and
new runtime resource reconciliation pass. Accounting may now implement the concrete P28
contract; capture, delivery, public activation and final engine E2E remain open. Historical
cleanup denials remain reported, and SPA remains on its dedicated branch.

## C# Architecture Gate Result: Process refresh admission (2026-10-23)

Status: Pass with follow-up for [atomic admission](../validation/2026-10-23-process-refresh-admission.md).

### Findings

| Severity | Finding | Evidence | Required action |
| --- | --- | --- | --- |
| Info | Three explicit receipt/request records cross the member threshold | Final snapshot `snap-20261023160924-65134217` | Accepted cohesive wire contracts; no extra owner/layer |
| Existing | Collector has 189 WS0002 errors | Both scoped snapshots; direct affected builds pass | Retain the limitation; no clean solution-wide claim |
| Follow-up | Admission is not complete refresh | P28 owner/accounting/capture/delivery plan | Qualify each dependency before exposing manual/automatic refresh |

### Dependency direction

Runtime stays independent; Application owns preparation/replay, and Persistence implements
the bounded ports and transaction checks. The three existing scoped project-reference
edges and zero scoped cycles remain unchanged. No project-file change or service locator.

### Partial-class policy

No business partial was added. EF-generated migration/designer partials retain their
normal generated role. The decision-store growth finding was removed by moving advance
validation into the dedicated persistence refresh guard.

### Testability proof

All 343 selected backend cases and eight actual older/current reader checks pass. Pure
admission uses substituted ports; SQL tests exercise registered preparation and real
Project publication, rollback, immutable replay and cancellation. New work adds no runtime
history scan or provider call. Final portability, source/output and new resource checks pass.

### Closure decision

The admitted intent may support original-child stop under the explicit P28 authority
design. Full refresh/replan, public activation and final E2E remain open. SPA remains on
its dedicated branch for colleague review; historical cleanup denials remain reported.

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

## Guarded run publication

Status: Pass with follow-up. The [56-case guard qualification](../validation/2026-10-21-manager-run-authority.md)
adds one explicit mutation capability to the existing file owner. It shares preparation
and revalidates the original witness; external authority I/O does not hold the workspace
lock. No new type, dependency edge, project reference or cycle appears. File store
members increase from 155 to 157, the mutation interface from two to three; other
reviewed owners are unchanged. All 277 findings and 189 collector diagnostics are
pre-existing. Durable intent, manager integration and public transport remain required.

## Durable manager review owner

Status: Pass with follow-up. The [147-case owner qualification](../validation/2026-10-21-manager-review-owner.md)
keeps intent and measured work in Core and current Project authority in Workbench.
No new project reference or cycle appears. Shared publication completion and pure
cancellation move out of the existing coordinator and journal. The intent validator's
single informational member-count finding is reviewed as cohesive identity validation.
The lifecycle callback runs after the Agent dispatch lease closes, allowing Process
accounting without a Core dependency on Processes. Current permission still gates
final disclosure. Existing interactive behavior, real SQL rollback, exact replay,
open-work recovery and cancellation are covered. Workspace/Process/API integration,
parent reconciliation and executor activation remain required.

## Native cancellation and Workflow coverage

Status: Pass with follow-up. [P17/P18 validation](../validation/2026-10-23-native-workflow-cancellation.md) separates Web identity, native host lifetime, Process recovery coordination and native Workflow persistence. No project-reference change, new business partial or dependency cycle is introduced. The two pre-existing AgentFramework module scoped cycles are unchanged; generated EF migration partials retain the existing convention. No added CodeAnalytics findings are present. Pure adapter/component cases and real SQL/HTTP/browser cases prove the owner boundaries, negative authority/metadata paths, original-request recovery and bounded queries.

The final snapshots include `snap-20261023030956-f3b31dd5` (AgentFramework), `snap-20261023032730-6a886db9` (Process persistence), and `snap-20261023032812-721f5c0b` (Processes module); the compact proof includes the earlier UI/Web/Application/Workflow snapshots and diagnostics. Browser evidence corrected a stale parent projection. Legacy root transport, manager-chat session concurrency and sustained/separate-process capacity remain follow-ups before activation and full closure.

## Legacy cancellation and original provider scope

Status: Pass with follow-up. [P19 validation](../validation/2026-10-23-legacy-cancellation-cutover.md)
removes the Application root cascade and hierarchy dependency; Web maps a typed refusal.
The real MAF producer exposed missing scope evidence. Models now owns its typed immutable
checkpoint contract, MAF classifies and enforces it before the wire, and the existing Core
stopper interprets it under the original lease and current authority. No new project,
reference, interface or partial file is added. The existing journal methods gain only the
new contract parameter and immutable field check; the broad execution service gains nothing.

Final snapshots are `snap-20261023043334-538a35d8` (Models),
`snap-20261023043420-3274eeb3` (Core) and `snap-20261023043501-df461bfc` (MAF).
No findings or project references are added. Three existing Core type cycles and one MAF
type cycle remain unchanged. Application removes one large-file finding; its and Web's
earlier P19 comparisons remain in the proof. Independent model/stopper tests and actual
MAF/HTTP/worker tests prove the boundary, including hosted uncertainty and historical
refusal. Manager-chat concurrency, sustained capacity and remaining owners remain next.


## Process planning dispatch and terminal shared scope

Status: Pass with follow-up. [P21/P22 validation](../validation/2026-10-23-process-planning-dispatch.md)
passes all 588 current cases and retains the original failures and exact repairs. The Process module routes a typed
execution contract to the Workbench-owned executor without a reverse module reference.
Runtime owns terminal scope eligibility; Persistence finishes the scope and its receipt
after child/accounting flush in the same root transaction. No provider work enters the
transaction. Existing cancellation and result-acceptance guards remain enforced.

The comparable P21 snapshots retain the same project references and existing cycles;
its sole new finding is the cohesive executor's informational member count. P22 starts
from `snap-20261023072639-65134217`, with no scoped cycle and three intended references.
Final snapshot `snap-20261023081255-fb03b32e` retains those edges and zero scoped cycles,
with no new findings. Collector workspace errors remain explicit limitations. Actual SQL
rollback at both flushes, stale/open-work refusal, real dispatch/review, all affected
Release builds, full portability and final source/output reconciliation pass. No new
project reference, business partial or provider wait in the transaction is introduced.
Public policy producers and broader lifecycle activation remain open; the P23 design
must preserve these owners and qualify its actual producer before UI activation.

## Selected chat title and publication boundary

Status: Pass with follow-up. [P20 validation](../validation/2026-10-23-chat-session-publication.md)
places the pure title condition in Core and current selected-key publication in Persistence.
It removes the unused whole-session save helper and keeps UI/API orchestration at existing
boundaries. No new project, reference, interface or business partial is introduced.
Three Core type cycles, one Persistence type cycle and the existing AgentFramework module
namespace/nested-builder cycles remain. The only new finding in the comparable owner
snapshots is the title-change value's informational member count; its five immutable fields
and three cohesive operations do not justify another abstraction.

Final Core/Persistence/Components snapshots are `snap-20261023051630-3274eeb3`,
`snap-20261023051708-601c88d2` and `snap-20261023051758-c6457ab2`. Web and product modules were
also inspected as `snap-20261023061102-3eb59b32` and `snap-20261023061307-3039be43`; their
broader solution/generated-source scope prevents interpreting older raw totals as deltas.
The two AgentFramework cycles involve Hosting and the existing nested image-generation
builder, which this phase does not change. Actual project files are unchanged. Collector
WS0002 and duplicate embedded-attribute warnings remain explicit limitations.
Independent title tests, real selected-key races, HTTP conditions, actual Editable callbacks,
and the native browser scenarios prove this seam. SPA generation preserves the same wire
condition and has separate runtime serializer and bounded-transport proof. Sustained
capacity, activation, remaining policy/owner integrations and whole-engine closure remain.

## Explicit public Process planning policy

Status: Pass with follow-up. [P23 validation](../validation/2026-10-23-process-policy-producer.md)
qualifies 251 distinct backend cases across recorded test-source repairs. Application
owns typed selection, immutable identity validation and the narrow admission-provider
boundary. Workbench implements planning eligibility, coherent settings capture and
current Agent/Project checks. Runtime owns supported host semantics; Persistence binds
the original selection and admissions to the durable preparation. The existing launch
partial only delegates at its preparation boundary. There is no new business partial,
project reference, schema migration or service locator.

The provider closes the Workspace read before acquiring the Agent catalog lease. Neither
lease spans provider work. Replay restores saved policy and funding instead of reading
new defaults. The coordinator checks exact assignment coverage and original root/step
bindings; adding a later owner uses the same registered seam. Unknown or unsupported
contracts and policies fail explicitly. Selection never grants a missing activity operation.

Final snapshot `snap-20261023100837-da94960e` retains four intended project references,
six existing Workbench cycles and no new findings. Its 189 WS0002 errors, 12 DI0001
informational diagnostics and four DI0002 warnings remain collector limitations; direct
affected Release builds pass. This scoped comparison is not global solution health.
Pure coordinator/fingerprint tests, real HTTP/SQL/queued MAF review and rollback/replay
regressions exercise the new owner without using the launch monolith for pure tests.
The generated SPA client preserves selected/effective semantics and bounded retry behavior.
Broader lifecycle, refresh/replan, definition defaults and UI selection require their
own qualification before they may borrow this producer's trust.

## Workspace V2 acceptance and SPA policy editor

Status: Pass with follow-up. [P24 validation](../validation/2026-10-23-workspace-policy-client.md)
repairs an OpenAPI name collision in the existing Web schema owner and registration.
Only the three nested V2 Workspace outcome identities change; V1 and unrelated framework
naming retain their existing contract. There is no project-reference, schema migration,
runtime owner, business partial or DI lifetime change. Actual generated schema and SDK
tests prevent a cast or client-side fallback from hiding lost policy fields.

SPA reuses its existing session, bounded transport and shared UI controls. Its new pure
policy codec has no I/O or state ownership and preserves exact integer ticks. It does not
add polling, a dependency, global state or a backend policy resolver. The schema export
preserves all 317 routes, while real SQL/HTTP, state/codec and browser checks prove distinct
owner responsibilities. Broader launch UI and actor-bound durable draft recovery remain
separate obligations; this scoped pass does not close engine-wide dependency or runtime gates.

## Process planning contract discovery

Status: Pass with follow-up. [P25 validation](../validation/2026-10-23-process-policy-discovery.md)
qualifies metadata on the existing Application admission-provider seam. Workbench owns its
execution identity and supported policy combinations; it validates resolved policy against
that same descriptor. Application snapshots metadata without invoking preparation. UI can
discover the contract without referencing Workbench or moving Runtime types into Projections.
The Web-only item-schema correction reuses the complete policy schema and preserves every
existing public schema. There is no new project reference, runtime service lifetime,
business partial, service locator or storage change.

Snapshot `snap-20261023120826-c3ed56ba` covers Application, Workbench and Web before the
schema-only correction. Direct final review covers that helper. Three scoped project
edges retain direction, six known Workbench cycles remain, and existing ProcessesApi
size/member findings are explicit. Collector limitations are 189 WS0002 errors, two
DEP0002 warnings, 14 DI0001 messages and six DI0002 warnings; this is not global health.
Affected builds and independent coordinator tests pass. Real HTTP authorization/schema
and both public producer flows lend trust to the unchanged admission/dispatch boundary.
Catalog construction performs no database/Agent/provider work, and no new polling or
history-sized operation was added. UI lifecycle, durable drafts and broader policy
activation still require their own architecture and behavior proof.

## Project Process launch and intent recovery

Status: Pass with follow-up. [P26 validation](../validation/2026-10-23-project-process-launch-client.md)
qualifies optional project scope through the existing authoring/lifetime owner, and one
read-only indexed lookup through the existing prepared-launch owner. The specific missing
project exception is an Application boundary contract; Web maps only that condition to
404. Both observation routes share mapping and current caller/profile/surface checks.
They perform no preparation, continuation or dispatch. No project reference, runtime
registration/lifetime, migration, business partial or service-locator seam was added.

Snapshot `snap-20261023131243-4560d5cf` covers Application, Modules.Processes and Web:
533 documents, two scoped project-reference edges and no scoped cycle. This excludes
Workbench, so its earlier cycles remain open. The collector reports 189 WS0002 errors,
two DEP0002 warnings, two DI0001 messages and three DI0002 warnings; actual affected
Release builds pass. Existing ProcessesApi complexity findings remain. Its added work
is HTTP observation orchestration using existing owners, not another runtime capability.

SPA owns a typed launch session and a separate bounded tab-recovery codec; its component
renders and invokes those effects. The route keys lifetime by project and aborts/disposes
the previous session. The generated client and existing protected transport retain the
real protocol instead of introducing manual HTTP or client-side policy resolution.
Storage precedes dispatch, observations cannot create preparations, and uncertain
acceptance cannot silently become separate work. No timer, polling, worker, history scan
or new transaction lifetime is introduced. SQL/HTTP, state, generated-client and final
rendered proofs cover these boundaries independently. Native UI, broader lifecycle,
publication review and sustained capacity still require their own qualification.

## Process basis-change failure

Status: Pass with follow-up. [P27 validation](../validation/2026-10-23-process-basis-change-failure.md)
qualifies the optional typed response grant in the existing Agent origin and Process step
admission. Models retain the provider-neutral child contract; Runtime owns the version-6
admission, Application its observed original grant, and Persistence projects that saved
value. Workbench binds the verified grant and both owning modules compare it on original
source/receipt reads. Legacy absent grants retain their original pause semantics. Actual
old-binary proof rejects new admissions before child enrollment. No new project reference,
runtime service, business partial, service locator, migration or settlement path is added.

Scoped snapshots `snap-20261023134255-6f4f0f5b` and `snap-20261023140815-6f4f0f5b`
retain three project edges and nine existing module/type cycles across 814 documents in
Models, Core and Workbench. Direct project-file review covers the added Process layers.
Collector limitations remain 189 WS0002 errors, twelve DI0001 messages and four DI0002
warnings; this is not a clean solution-wide graph. All affected Release builds pass.

Pure codec/policy/identity tests are independent of the runtime monolith. Public HTTP,
queued MAF, SQL publication, replay and exact grant-accounting proof exercise current and
changed material under both modes/responses. Fail settles the original child and root;
Pause retains its grant and blocked Process. No new polling, provider work, query fan-out
or lock lifetime is introduced. SPA extends its existing session/storage boundary and
shared controls. Parent refresh/replan, shared-scope quiescence, broader owner/lifecycle
integration and sustained capacity remain explicit downstream obligations.
