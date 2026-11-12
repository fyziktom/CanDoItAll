# Original Process refresh successor

Status: Pass with follow-up for the original successor boundary. All 129 selected cases
pass. P28 Behavioral proof tier. Signed prerequisite
`8acae9662a6106a8161e3e34f04f3b04c338787f` qualifies original reconciliation accounting
and records the successor/capture decisions before this implementation. Public refresh
and automatic Replan remain unadvertised.

## Owned behavior

This checkpoint continues the user's request for safe concurrent edits by users, agents
and workflows, with explicit snapshot/reload behavior. It owns the successor transaction
after original NoEffect accounting. It does not close the complete refresh producer.

Runtime prepares one deterministic successor command from the original refresh action.
Only the selected waiting step becomes Ready, its nonterminal run becomes Active and its
normalized timestamp advances. The old attempt, completed claims, waiting results and
other runtime fields remain unchanged. Existing rework/reactivation/readiness events use
the original admission actor and exact RuntimeProjection messages. Generic rework, which
resets a blocked attempt, is not used: the existing scheduler must increment the retained
attempt when it enrolls the next child.

Persistence checks the retained admission and deterministic reconciliation receipt under
the existing root fence. Only the original NoEffect closure and settlement may resume
the unchanged reconciled generation. Its capture allocation must be distinct, current
and unsettled, with no other local work open. Shared inheritance also requires no other
open root work. Existing material, complete-child and scope owners retain validation of
the captured generation, original scope and membership. Accepted completion and root
cancellation cannot acquire a successor.

The shallow implementation would turn a Reconciling head Ready, reset its attempt, or
settle any allocation while ignoring unfinished work. Pure forged mutation cases and SQL
substituted accounting/allocation cases must reject those paths. Real material capture
uses the registered Workbench owner outside the Process transaction. Integration fixtures
orchestrate its reservation and completion explicitly; this is consumer qualification,
not evidence of a durable refresh capture coordinator or restart delivery worker.

## Selection and invalidation

Final selection: 129 distinct cases, no unfiltered gate. The initial 127-case selection
passed before two root-quiescence cases were added following the contention review.
The expanded 19-case successor class was rediscovered and rerun; the 110 unaffected Unit,
accounting and admission cases retain identical test sources and production dependencies.

| Project | Exact class topic | Expected cases | Reason |
| --- | --- | ---: | --- |
| Unit | ProcessDecisionRefreshSuccessorTests | 34 | Pure transition, authority and mutation refusal |
| Unit | ProcessDecisionRefreshAccountingTests | 33 | Original accounting prerequisite and unchanged mutation contract |
| Integration | ProcessDecisionRefreshSuccessorTests | 19 | Both modes, real capture, scheduler enrollment, replay, races, rollback and root quiescence |
| Integration | ProcessDecisionRefreshAccountingTests | 26 | Accounting remains valid through the existing commit owner |
| Integration | ProcessDecisionRefreshPersistenceTests | 17 | Original admission and legacy refresh persistence regression |

Each filter is `FullyQualifiedName~<declared namespace>.<class>.`; manifests retain the
literal filters, source-derived methods, expected counts and actual discovery. Runtime
and Persistence build directly in Release, followed by both test projects with local
libraries. Source, selected tests, transitive owner outputs and dependency mode invalidate
reuse. No schema, dependency, public wire contract or composition change triggers another
broad stable run. The historical stable result is not reclassified as passing.

The first build caught a missing Core namespace import. The first test run passed 31/34
new Unit cases; three shared-material fixtures used an event digest where the material
contract requires an uppercase 64-character SHA256 fingerprint. Production rejected the
invalid stamp correctly. The corrected test-only candidate changes that source and the
Unit DLL/PDB; all production and Integration outputs remain identical. The original
failed run is retained and is not counted as a passing run.

The verified candidate passes all 67 Unit cases. Its first SQL run passes 13/17 new cases;
four assertions incorrectly expect the raw commit owner to replace the stored Applied
outcome with Duplicate. That owner preserves the original outcome and marks replay with
`CommittedNow=false`. The final assertions require successful original-outcome replay,
no fresh commit and unchanged retained state. The scheduler/enrollment case uses the real
clock so freshly captured database timestamps are not compared against a frozen past.
This second repair changes only the Integration test source and DLL/PDB. All production
and Unit source/output hashes remain identical; the 67 passing Unit cases are reusable.

## Architecture and proof records

Runtime owns the pure successor rule; Persistence owns its bounded SQL evidence checks.
Application and Workbench retain their existing input, accounting and capture contracts.
No project reference, service locator, business partial, schema or extra transaction
owner was added. No provider or capturer executes inside the Process root fence. Queries
target the original action, run, step, child and budget; open-allocation predicates use
the existing budget keys. This explicit continuation adds no polling or worker and is
not sustained capacity evidence.

Scoped snapshots `snap-20261023173045-65134217` and
`snap-20261023182501-fb03b32e` retain three intended project edges, zero scoped cycles and
the same 236 findings. The collector retains 189 WS0002 errors; direct Release builds
are separate evidence and this is not a clean solution-wide graph.

Local proof roots under `.artifacts/concurrent-mutations/validation/`:

- `c4-process-refresh-successor`: first build/discovery, failed 31/34 Unit result, unchanged
  source/output verification and zero new resource residue. An initial observer launch
  failed before execution because the ownership record used a different filename; the
  preserved pre-creation record was adapted to the observer schema before tests ran.
- `c4-process-refresh-successor-verified`: repaired test source, build, 1,267 source pins,
  44 output hashes, exact 127-case discovery, 67 passing Unit cases and the preserved
  13/17 SQL result. Full portability passes with 10,121 files, 47,423 findings and 15,697
  reviewed allowances unchanged. All three executed commands have observer coverage;
  no new process, database or temporary-directory residue remains. Its two bulk scan
  files were removed after preserving their hashes and compact review. Secret review
  covers 82 scanner text files and six supplemental text files with no credential match.
- `c4-process-refresh-successor-final`: Integration-only repair, 60-case rediscovery and
  passing SQL/static qualification. All 60 cases pass, including next-attempt enrollment
  and restart replay in both modes. Its 926.922-second observer covers all three commands
  with zero new process/database/temp residue. Secret coverage includes 77 scanner text
  files and six supplemental files. `test-only-repair-comparison.json` records the exact
  source/output invalidation boundary.
- `c4-process-refresh-successor-quiescence`: two added root-work cases, current Integration
  build, exact 19-case discovery and 19 passing cases. An unrelated root allocation blocks
  shared inheritance, while StepSnapshot advances and preserves that other reservation.
  `test-addition-comparison.json` verifies that only the successor test source and Integration
  DLL/PDB changed. `qualification-combined-summary.json` verifies all 129 distinct cases,
  with no skipped or unexecuted selected cases. Its 286.280-second observer covers the
  full command with no new resource residue. All 1,267 source pins and 44 outputs remain
  unchanged during this final run.

Final full portability enforcement passes without rewriting the 15,697 reviewed allowances.
Tool tests, rendering register, documentation and diff checks pass. Compact manifests retain
scan counts and hashes before bulk JSON/CSV cleanup. No production placeholder, fixture
branch, service locator, new project reference or business partial was added.

## Closure decision

The capture producer may rely on this guarded successor transaction. The positive path
uses real admission, child stopping, accounting, material capture, scheduling, claiming and
enrollment owners; capture-stage orchestration itself is still a test helper. Public refresh,
automatic Replan, durable capture-result recovery and recurring delivery remain open in P28.
The new preparation/failure decisions distinguish known closed capture work from unknown
interrupted work; these tests do not resolve that remaining crash window or sustained capacity.

The isolated PostgreSQL server is retained for the next authorized lane. Six owned bulk scan
files across the successful proof roots are removed after preserving compact evidence.
Historical denied cleanup targets remain outside new-run cleanup claims, including the
previously reported `candoitall-api-tests/118b26c298ad4d6d9fea358f28134973` directory blocked
by automatic approval review. SPA stays clean at `11399398e4b836116a6a9b06291f2619368fd4db`
on `codex/concurrent-mutations`; no merge or push to main was performed.
