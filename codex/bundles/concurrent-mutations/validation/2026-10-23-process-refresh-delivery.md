# Process refresh continuation and cancellation handoff

Status: all 36 new continuation cases pass across the verified test-only repair below;
74 existing capture/accounting regressions are still running. Final portability passes
without a baseline change. Behavioral proof tier; no background worker, durable queue,
public refresh/Replan activation or capacity qualification.

## Entry and owned requirement

Continue the user's requirement that users, agents and workflows can work concurrently
without incorrect module dependencies, unbounded work or lost updates. This slice owns
the original admitted refresh's stop/account/capture continuation and its cancellation
handoff. It consumes signed `41955b020` compatibility proof and the journal implemented
at `e906b22ad`; ten actual older-producer cases preserve all 36 Process tables. No schema
or protocol change here invalidates those saved-state compatibility conclusions.

Entry gate: Pass for this bounded continuation. The earlier junction cleanup blocker is
independent of its source, fixture and proof. The user authorized the exact non-recursive
`C:\repositories\jrc` retry; automatic review again rejected it before execution as
**blocked by policy**. That alias and its managed checkout remain, recorded in the prior
proof root's `alias-cleanup-authorized-retry.json`. Other historical denials are unchanged.

## Ownership and concrete behavior

`ProcessDecisionRefreshDeliveryCoordinator` is a scoped Application use case registered
by Modules.Processes. It receives only original run/admission identity and uses the
retained admission's actor. It checks successor/abort/accounting receipts before calling
the original child stopper, accounting owner or capture coordinator. Results distinguish
pending stop/outcome work, unknown invocation, legacy unknown reservation, contention,
source projection, review, completed original effects, advanced successor and stopped
refresh. Stopped means this refresh has no remaining capture funding; cancellation still
owns the Process's original child and finalization.

The accounting owner shares its original-input validation with a completed-receipt read.
This eliminates a redundant full receipt read in delivery. It does not remove retained
runtime JSON or the runtime UOW's history-dependent cost. No extra model call or capture
is authorized by delivery, lease expiry or loss of an acknowledgement.

Capture resume now bypasses live reconciliation after the runtime is stopped. Without
that change, cancellation can finalize the local decision while root capture work is
still reserved, making the live query refuse the very work that must drain. Reserved
work closes as NeverInvoked; Returned work settles its exact retained cost; Invoking and
legacy work stay pending with funding held. Original receipt/journal/budget guards remain.

No new project references, generic recovery framework, partial-class expansion, Workbench
dependency, migration, default or endpoint. The next durable queue must atomically retain
delivery during admission, supply bounded leases/concurrency and checkpoint original
receipts. Operator resolution, source revocation across actual owner profiles, capacity
and public activation remain separate gates.

## Selected proof

Build Application, Modules.Processes and Integration directly in standard Release with
local family libraries, serialized output writes, `/m:1 /nodeReuse:false` and
`UseSharedCompilation=false`. Final confirmation follows the accounting-owner refinement.

All filters are `FullyQualifiedName~CanDoItAll.Tests.Integration.<class>.` in
`tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj`:

| Class | Expected discovery | Reason |
| --- | ---: | --- |
| ProcessDecisionRefreshDeliveryTests | 14 | Registered producer, original accepted effect, pending stops, cancellation race, wrong action |
| ProcessDecisionRefreshDeliveryRecoveryTests | 22 | Lost acknowledgements, cancellation ordering, exact drain, unknown/legacy retention |
| ProcessDecisionRefreshCaptureCoordinatorTests | 26 | Changed capture resume owner and real next scheduler attempt |
| ProcessDecisionRefreshCaptureBoundaryTests | 4 | Source/funding boundaries |
| ProcessDecisionRefreshCaptureConcurrencyTests | 2 | Competing producer admission |
| ProcessDecisionRefreshCaptureJournalPersistenceTests | 16 | Journal persistence, retained work and root fence |
| ProcessDecisionRefreshAccountingTests | 26 | Shared original-input validation and completed receipt replay |

Expected total: 110. Discovery must match before execution. The actual registered
Workbench/Process/PostgreSQL owners produce receipts, budgets and effects; only explicit
failure/acknowledgement boundaries are substituted. A shallow implementation that simply
restarts capture would fail returned/unknown/legacy cases. One that abandons cancellation
would fail final root/budget closure. One that uses current caller identity would fail
the original-actor event assertion. Counts alone are not acceptance evidence.

The new scoped registration is covered by its real DI/consumer tests; it changes no
global composition. The accounting refactor invalidates its owning 26 cases. No unfiltered
Stable trigger is introduced. Historical Stable failures and other wider gates remain.

Proof root: `.artifacts/concurrent-mutations/validation/c4-process-refresh-delivery`.
Ownership was recorded before creation. Reuse the existing isolated PostgreSQL 18 fixture
on 53619 through bounded database leases; preserve the ordinary application on 5032.
Final portability must include untracked protected sources and enforce without writing;
review any baseline delta. Require documentation, source/output identity, architecture,
secret review and owned process/database/directory reconciliation before closure.

## Checkpoint evidence and test repair

The first 14-case delivery run passed ten cases and failed four new test assertions.
`PublishAsync` intentionally advances its deterministic clock, so the two accepted-effect
cases must use the fixture's frozen clock. The two normal advance cases incorrectly asked
an idempotency replay receipt for event bodies; those receipts intentionally omit events.
They now assert the exact actor against persisted successor event rows at the committed
runtime timestamp, with the expected two/three event count. Production code is unchanged.

The verified proof root is
`.artifacts/concurrent-mutations/validation/c4-process-refresh-delivery-verified`.
`reuse-review.json` verifies 1,298 unchanged source inputs and all 30 production/supporting
outputs. Only the two corrected methods and Integration DLL/PDB changed. The three
unchanged methods retain the original ten passing cases. The four repaired cases and all
22 new recovery cases pass, yielding 36 distinct passing new cases. The remaining 74 cases
are recorded separately and must pass before behavioral closure. The first run's four
failures remain preserved; they are not relabelled as successful execution.

Application and Modules.Processes were built directly, then the standard Integration
output was refreshed. The final production confirmations took 3.251, 27.277 and 36.045
seconds respectively. The test-only repair rebuilt Integration in 58.441 seconds with
identical production output bytes. All discovery counts matched (110 initially; 100 in
the verified run plus ten eligible reused cases).

The final complete portability scan includes untracked protected files: 10,161 files,
47,442 findings, no truncation. Enforcement passes with all 15,703 reviewed allowances
unchanged and no baseline edit. Six portability-tool, four secret-tool and seven rendering
tool tests pass, as do the rendering register and documentation checks. The earlier run's
observer closes with zero remaining processes, database-name deltas or temporary paths.
Final verified-run resource and secret reconciliation remain due after its last test.

Architecture snapshot `snap-20261023234316-59d5ae0f` covers Application, Runtime, Persistence
and Modules.Processes: 521 documents, 1,340 types, 10,827 members, six scoped project edges,
zero detected cycles. No project reference changed. The delivery owner has eight members;
it and the accounting owner have no reported findings. The 388 existing scoped findings
remain visible (66 COMPLEXITY-001, 322 COMPLEXITY-002). The collector reports 123 WS0002
NuGet audit errors and one existing DI0002 warning at registration line 42; its one captured
service registration is not proof of the new scoped registration. Direct builds and the
actual registered continuation tests establish that registration. `architecture-review.json`
retains the bounded result; this is not a clean collector/compiler claim.
