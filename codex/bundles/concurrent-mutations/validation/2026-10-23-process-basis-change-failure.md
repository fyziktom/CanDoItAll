# Process basis-change failure qualification

Status: Pass with cleanup follow-up. P27, Behavioral proof tier. Backend base
`6e3cb4d14ad333f1511e053de86f37d2036a90c8`; SPA base
`c611d356ad9b486bc8f8c1e95f5376d6404d6fee`. SPA remains on
`codex/concurrent-mutations`, without a merge or push to main.

## Implemented contract

The public single-activity Project planner advertises Fail and PauseForReview for both
snapshot modes. New Fail preparations save a version-6 step admission with a typed child
response grant. The original dispatch authority carries that grant into the Agent origin;
Process background-source and Workbench receipt/read authority require exact equality.
The existing Core publication owner closes a changed-basis Fail child, and existing
Process completion settles its original allocation and root. Shared material closes with
the existing Cancelled scope phase for a failed root; the Agent/Process outcome is Failed.

Legacy admissions and origins omit the new grant and retain their pause interpretation.
The actual pre-change Runtime reader accepts legacy versions 4/5 and rejects new grants
in both read modes. The current reader accepts both formats: eight binary-reader checks.
Malformed, stripped, downgraded and mismatched grants fail validation; persisted origins
cannot acquire or remove the grant later. There is no database migration or new project
reference, service, provider, worker, lock, query fan-out or refresh loop. The scalar
protocol checks add bounded work; publication and accounting reuse their existing owners.

The SPA exposes advertised change responses, validates combinations, and preserves the
override through the existing bounded pending record, recovery and acceptance retry.
Legacy read-mode-only records remain readable; Fail's numeric zero is retained. Existing
public request/response contracts already contain the override, and the persisted types
are absent from the P26 OpenAPI schema; no generated SDK source was changed.

## Validation and test repair

All changed production projects built in Release with local libraries and serialized
standard outputs: Agent Models, Process Runtime/Application/Persistence, Modules.Processes
and Workbench. Unit and Integration builds also pass; Web is refreshed transitively.
Commands use `dotnet build <project> --configuration Release --no-restore
-p:UseLocalCanDoItAllLibraries=true /m:1 /nodeReuse:false`.

Discovery uses `dotnet test <owning project> --configuration Release --no-build
--no-restore -p:UseLocalCanDoItAllLibraries=true /m:1 --filter <exact class prefix>
--list-tests`; execution adds the TRX logger and an owned results directory. Exact
prefixes, source methods and discovered case names are retained in the discovery records.
An initial namespace error discovered zero Unit Process cases and was corrected before
execution. No zero-case result is treated as proof.

| Topic | Expected/discovered | Result |
| --- | ---: | --- |
| Unit AgentInheritedPlanningResponseTests | 14 | Pass |
| Unit AgentInheritedPlanningJournalTests | 33 | Pass |
| Unit ProcessLaunchDecisionTests | 37 | Pass |
| Unit ProcessLaunchDecisionDiscoveryTests | 15 | Pass |
| Unit ProcessDecisionDataPolicyAdmissionTests | 13 | Pass |
| Unit ProcessDecisionChildResponseAdmissionTests | 13 | Pass |
| Unit ProcessInheritedSourceBindingTests | 18 | Pass |
| Integration ProcessPlanningLaunchApiTests | 17 | Pass |
| Integration ProcessLaunchDecisionDiscoveryApiTests | 3 | Pass |
| Integration AgentInheritedPlanningReviewTests | 26 | Pass |
| Integration AgentInheritedPlanningRunAdmissionTests | 35 | Pass |
| Integration ProjectTaskPlanInitialBindingTests | 23 | Pass |

The first public-launch run exposed an incorrect new test assertion: root consumption
includes material capture as well as child work. It then exceeded the existing 16 MiB SQL
log bound and was terminated with verified process-tree exit. Its partial integration
output is not a passing result. The corrected test compares the pre-publication balance,
exact child settlement, root allocation sums and unchanged replay balances. Only that
test and its HTTP fixture changed; the fixture suppresses informational EF command logs
while preserving warnings/errors. Exact source/output comparison proves all production
and Unit outputs unchanged, so the 143 passing Unit cases remain valid. The 104 Integration
cases are rerun from the rebuilt assembly with fresh discovery. No output limit, assertion
of an owned allocation or production behavior was weakened.

The next complete public-launch run passed 15 of 17 cases. Both failures compared the
paused Process with an earlier transient Active observation; manager-waiting execution
correctly becomes Blocked. The assertion now requires Blocked, while the shared material
scope remains Active. This one test-source repair changes only the Integration DLL/PDB;
production and Unit outputs remain unchanged. All 104 final integration cases pass.
The qualification summary verifies all individual TRXs and both exact repair boundaries:
247 distinct backend cases pass, with no skipped or unexecuted selected case.

SPA: all 152 tests pass (25 generator, 22 SDK, 105 application), including 42 launch-session
cases. Full app typecheck/build and owned formatting/lint pass; the existing bundle-size
warning remains. Sixteen compiled-browser states at 1920x1080 pass with four check writes,
two identical acceptance/continuation writes, twelve explicit reads and no page errors or
horizontal overflow. Selection, open response overlay, saved Fail review and restored
intent screenshots were inspected. Primary action and effective policy fit the first
viewport; the overlay is readable and contained. Existing page scrolling and components
are retained. Browser responses are controlled; SQL/real queued MAF proof is separate.

## Architecture and boundaries

Scoped snapshots `snap-20261023134255-6f4f0f5b` and
`snap-20261023140815-6f4f0f5b` cover Models, Core and Workbench: 814 documents,
three unchanged project edges and the same nine existing module/type cycles. No project
cycle was introduced. Direct project-file review shows no project-reference changes in
the expanded Process layers. Contracts stay in Models/Runtime/Application, persistence
projects the saved grant, and product binding remains in Workbench/Modules.Processes.
No runtime partial or service locator was added. Existing collector limitations remain:
189 WS0002 errors, twelve DI0001 informational findings, four DI0002 warnings;
`hasBlockingErrors=true`. These snapshots are not a clean solution-wide analysis claim.

The final full portability scan covered 10,092 files and 47,415 findings; enforcement
passed with all 15,691 allowances unchanged, without writing the baseline. Static-tool
tests, rendering register, documentation and whitespace checks also pass. No broad Stable
result is claimed: the changed consumers are covered by the
named topics; whole-engine closure and its historical failures remain open.

## Evidence and resources

Original proof: `.artifacts/concurrent-mutations/validation/c4-process-basis-change-failure`.
Confirmation: `.artifacts/concurrent-mutations/validation/c4-process-basis-change-failure-verified`.
Final: `.artifacts/concurrent-mutations/validation/c4-process-basis-change-failure-final`.
These contain command records, exact discovery, TRXs, source/output identities, the
test-only repair comparison, binary-reader results, scoped architecture and resource
observations. The final candidate's 1,242 source files and 40 outputs remained unchanged
through validation. Browser images:
`output/playwright/concurrent-mutations-spa-p27-20261023`.

SPA signed checkpoint `11399398e4b836116a6a9b06291f2619368fd4db` has signature status G,
tree `be26d936ba5f10f33ff3c9f90fe271f58e9b0966`, and parent `c611d35`. Its six changed
source/document files and 127 compiled files are pinned; its worktree is clean.

The shared isolated PostgreSQL 18 server on 53619 is reused; ordinary 5032 is untouched.
The browser context and preview host closed. The first interrupted run left one new
database and one linked test directory. Its exact database
`cditall_primary_da5d4c5501be45928b04f197f919ad5f` was removed and absence verified.
Automatic approval review blocked deletion of
`C:/Users/lucys/AppData/Local/Temp/candoitall-api-tests/118b26c298ad4d6d9fea358f28134973`
with reason **blocked by policy**. It remains and was not retried. Prior historical denied
cleanup also remains. Final confirmation must report new deltas separately from these
preserved blockers; neither this slice nor the bundle may claim complete cleanup.

The final observer covered all five integration commands over 709.20 seconds and closed
without fault. Reconciliation confirms zero remaining owned processes, database-name
deltas or new temporary entries. The preceding complete failed candidate also has zero
new residue. The interrupted candidate separately confirms its database and processes are
gone, with only the denied directory above preserved.

The six owned bulk scan JSON/CSV files and the historical Runtime probe binary were
removed separately after passing proof and exact-path/hash review. Scan summaries,
enforcement records, binary-reader source/results and hashes remain. The interrupted
16 MiB SQL log was reduced to its 98 failure-diagnostic lines plus command/hash record.
All three proof directories have zero secret-sentinel findings, no oversized or
unreadable text exclusions, and three reviewed matches for private-file/variable references
in each execution helper; the credential value is absent. The final scans cover 210, 74
and 93 text files respectively. Additional helper-source sentinel checks are retained
separately because the scanner excludes Python, JavaScript and C# source suffixes.

## Progression

The Fail/Pause producer and client are qualified for downstream work, with the exact
historical cleanup blocker retained. Automatic parent replanning, shared refresh
quiescence, multi-activity lifecycle, authored defaults,
native/planning-publication UI, remaining owner waves, capacity and final engine E2E remain
open. Fail at publication does not imply live data subscriptions or permission to publish
from stale material.
