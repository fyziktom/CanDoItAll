# P28: registered refresh capture producer

Status: Pass for all 65 distinct selected cases across the verified test-only repair.
Behavioral proof tier. This checkpoint owns known capture work
and successor production. Durable unknown-work recovery, public refresh, automatic replan
and recurring delivery remain separate gates.

## Ownership and behavior

The Process Application coordinator reads original progress before source selection. Only
a caller whose reservation committed now may invoke the registered content capturer.
StepSnapshot captures material for the original step. RunSnapshot captures once at the root,
then inherits that retained material through a separate measured local grant. The existing
successor transaction makes the original waiting step Ready and preserves its history and
attempt number. The scheduler, rather than capture, enrolls the next child attempt.

The immutable attempt retains original identities, policy, grant and expectations. Its
capacity checks leave allocation slots for capture, inheritance where needed, and the next
child. Manual refresh spends no automatic replan. The receipt validator checks exact stage,
allocation, grant, settlement, funding and shared-material relationships before accepting
replay. These are Application contracts; no Workbench implementation reference, service
locator or business partial was introduced. Composition registers the coordinator at the
existing Process module boundary.

Known refusal, expired material, exceeded grant, caller cancellation and capturer exception
settle measured closed work without publishing material. Overruns remain overruns rather
than being clamped to the grant. Cancellation draining uses its own bounded five-second
accounting token. Original exceptions and caller cancellation propagate after draining.
Commit acknowledgement loss retries only settlement with the same material and work; it
does not repeat capture. A retained reservation without a known returned result stays
Pending, retaining its grant. Root completion can be inherited after a host restart without
another source capture or root charge.

Other open local work prevents successor advance. Shared inheritance also waits for root
work; unrelated root work does not block an independent StepSnapshot successor. If new work
opens after the local reservation, the producer settles its own closed work through abort
and preserves the competing allocation. The guard reports a typed UnclosedWork reason;
missing original funding and an already closed own allocation remain explicit invalid
evidence. Stale current-head evidence is reported separately from corrupt original receipts.

Preparation now uses a short PostgreSQL REPEATABLE READ, READ ONLY transaction with
transaction/statement/lock limits of 5s/2s/1s. It completes before calling Application or
Workbench and never takes the Process mutation fence. Progress remains one SQL statement.
A coherent read is advisory: the existing UOW rechecks mutation expectations under its
root fence. The limits exclude initial connection establishment. No new schema or project
reference was added. This is bounded-query and contention evidence, not a sustained-load
or whole-history performance claim.

## Selected proof and invalidation

Expected and discovered selection:

| Project | Class | Cases | Purpose |
| --- | --- | ---: | --- |
| Unit | ProcessDecisionRefreshCaptureIdentityTests | 3 | Stable original action/stage identities |
| Integration | ProcessDecisionRefreshCaptureCoordinatorTests | 26 | Actual capture, scheduler enrollment, replay, competing callers, failure/draining, lost acknowledgement and restart |
| Integration | ProcessDecisionRefreshCaptureBoundaryTests | 4 | Capacity preflight and inconsistent replay evidence at the Application query boundary |
| Integration | ProcessDecisionRefreshCaptureConcurrencyTests | 2 | Real writer during read-only snapshot and stale mutation rejection |
| Integration | ProcessDecisionRefreshCaptureQueryTests | 11 | Registered original-source/funding selection and bounded progress |
| Integration | ProcessDecisionRefreshSuccessorTests | 19 | Dependent commit, quiescence, rollback and cancellation |

Each filter is `FullyQualifiedName~CanDoItAll.Tests.<Project>.<Class>.` using the exact row
above. Zero or unexpected discovery is rejected. Boundary tests alter typed query results
derived from the real persisted admission or completed producer; they do not claim to
exercise database corruption. Actual database query and commit tests supply that separate
contract evidence. No capture is permitted during capacity refusal or invalid replay.

Runtime, Application, Persistence and Modules.Processes have direct Release build evidence.
Both test projects refresh their standard Release outputs with local libraries. The first
Application build caught an ambiguous target-typed construction between two receipt
overloads; explicit construction repaired it before testing. The failed build remains in
the proof directory. The frozen candidate has 1,280 source pins and 44 output hashes.

The first execution passed all three Unit cases and 25 of 26 coordinator cases. The failing
test tried to admit root work while the shared scope was Capturing; the existing guard
correctly refused it. The test now opens that work immediately after root completion,
before local inheritance, and reads the current budget before settling the competing work.
Production behavior is unchanged. Source/output verification preceded this test-only repair.
Binary comparison confirms that only this test source and the Integration DLL/PDB changed;
production and Unit outputs are identical. The three Unit cases are reused. All 62
Integration cases were rediscovered and passed on the corrected candidate, with zero
skipped or unexecuted selected cases. The coordinator's 26 cases took 368 seconds; the
remaining classes took 74/48/156/264 seconds respectively. All 1,280 source pins and 44
output hashes remained unchanged through corrected execution.

Changed producer/receipt/query/guard/enum/composition source or selected fixture inputs
invalidate this selection. No schema, public transport, global composition, build policy
or unrelated owner changed; no unfiltered suite is selected. Earlier stable-suite failures
and full-engine E2E remain open.

## Architecture review

Before: `snap-20261023192129-17b44d93`; after: `snap-20261023201727-17b44d93`.
The same three intended project edges and zero scoped cycles remain. The collector retains
189 WS0002 diagnostics; successful direct builds are the compilation evidence. The after
scope has 318 documents, 933 types and 7,707 members. No new method-complexity finding was
introduced. Three member-count findings concern the cohesive immutable attempt (18 source
members), capture coordinator (11) and refusal enum (9). The coordinator owns one lifecycle:
reserve, invoke, and settle or abort. Persistence owns storage and Workbench owns content.
Moving methods into partial files or unrelated helpers would not improve those boundaries.

## Proof resources and closure

Ignored proof root:
`.artifacts/concurrent-mutations/validation/c4-process-refresh-capture-producer`.
Its sibling ownership record preceded directory creation. Exact commands, discovery names,
source/output hashes, architecture summary and runtime observation are retained there.
The test repair uses its `-verified` sibling, also declared before creation. The first
observer covered both test commands for 377 seconds and reconciled zero new runtime
processes, databases or temporary directories. Its full no-write portability gate passed.
The isolated PostgreSQL 18 fixture on port 53619 is reused. The ordinary 5032 instance,
installed tools and colleague-owned SPA main are preserved. SPA remains at signed
`11399398e4b836116a6a9b06291f2619368fd4db` on `codex/concurrent-mutations`.

Final no-write portability enforcement passes: 10,137 scanned files, 47,426 findings and
15,697 unchanged reviewed executable allowances. The seven new C# files have no scanner
findings. Tool tests (6/4/7), rendering registration, documentation and whitespace pass.
The corrected observer covered all five commands for 926 seconds and reconciled zero new
processes, databases and temporary directories. Four owned bulk JSON/CSV scans were removed
across both proof roots after retaining summaries and hashes. The first secret review
covered 99 scanner text files and six supplemental text files, with no actual credential
sentinel; three private-variable references were reviewed. Final corrected secret coverage
passed for 97 scanner text files and six supplemental text files, again with only those
three reviewed variable references and no actual sentinel. The shared isolated PostgreSQL fixture remains available
for the authorized recovery work.

Historical automatic-approval cleanup denials remain separate, including
`C:/Users/lucys/AppData/Local/Temp/candoitall-api-tests/118b26c298ad4d6d9fea358f28134973`,
whose removal was denied with “blocked by policy.” This checkpoint does not retry it.

## Progression decision

The producer and its dependent successor may support the next qualified recovery boundary.
No public activation follows merely from producer success. Reservation without durable
returned-work evidence cannot prove whether capture began or stopped. Before recovery can
retry it, the durable owner must distinguish reservation, invocation, returned outcome and
unknown work. Lease expiry cannot manufacture measured cost or authorize recapture. Known
completed root material is the narrower restart case qualified here. Durable result
handoff, explicit unknown-work resolution/retry and delivery remain next in the
[recovery design](../plan/P28-process-refresh-recovery.md). Whole-engine E2E is not complete.
