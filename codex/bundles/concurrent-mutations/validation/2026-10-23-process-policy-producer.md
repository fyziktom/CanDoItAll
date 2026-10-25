# Public Process policy producer validation

Status: qualified, 2026-10-23.
All 251 distinct backend cases pass across the verified test-source repairs below.
The 12 public producer cases include queued MAF execution, HTTP approval, one publication,
replay and terminal accounting in both snapshot modes. SPA generation, 62 tests and its
typecheck/build pass on its separate signed branch. Whole-engine completion remains open.

Design: [P23](../plan/P23-process-policy-producer.md). Operator/API contract:
[Process planning launch policy](../../../../docs/process-planning-launch-policy.md).

## Owned behavior and proof boundary

The user requires deliberate snapshot/change behavior for concurrent users, Agents and
Processes without mixing module responsibilities or introducing provider-held locks.
This slice admits one explicitly selected Project task-planning activity through the
normal Process HTTP producer. RunSnapshot and StepSnapshot retain original settings
provenance, bounded root funding and exact assignment coverage. Publication requires
current facts and manager review. Fail/Replan, shared refresh, multi-activity lifecycle,
Workflow activation and native/SPA selection remain open.

Application owns typed selection and immutable admission checks. Workbench owns the
registered planning provider, coherent Workspace read and selected Agent/Project checks.
The existing preparation store and initial runtime transaction retain the original policy.
No new project reference, business partial or schema migration is introduced.

The shallow-pass trap is seeded consumer state or a manually dispatched test that never
proves public delivery. The final positive must publish a real definition, use public
check/accept, let the ordinary queue dispatch a real MAF proposal, review it through HTTP,
and observe one task publication, replay and terminal budget/shared-scope accounting.
Negative cases must prove their specific refusal, no preparation/runtime/funding and no
provider work. Fixture transport and executor selection are controlled; execution,
admission, publication, database and journal owners remain real. The diagnostic decorator
forwards all workspace calls and rethrows startup exceptions unchanged.

## Preserved candidates

Proof roots below are under `.artifacts/concurrent-mutations/validation/`.

| Candidate | Executed / passed / failed | Finding |
| --- | --- | --- |
| `c4-process-policy-producer` | 47 / 42 / 5 | The standard compiler retains its managed host requirement; an empty-host consumer assumption rejected the real producer. The missing-policy fixture also attempted an invalid V2 null write. |
| `c4-process-policy-producer-host-contract` | 48 / 46 / 2 | The API already queues execution; an additional manual dispatch raced capture/claim ownership. |
| `c4-process-policy-producer-queue` | 48 / 46 / 2 | Actual queued execution exposed the executor's remaining empty-host request guard. |
| `c4-process-policy-producer-executor` | 48 / 46 / 2 | Startup reached the original task-publication permission fence. A test-only collection construction compile error was repaired before execution and preserved. |
| `c4-process-policy-producer-startup` | 2 / 0 / 2 | Narrow diagnosis identifies missing declared `ExecuteExternalAction`; no production behavior was replaced. |
| `c4-process-policy-producer-operation` | 50 / 48 / 2 | The new early operation refusal passes. The correctly authored positive reaches a saved child, whose failed outcome still needs diagnosis. |
| `c4-process-policy-producer-runtime` | 2 / 0 / 2 | Saved child reports `InvalidProviderOutput` after one actual provider request. |
| `c4-process-policy-producer-response` | 2 / 0 / 2 | The expected fixture draft prepares against the saved decision; the owner codec is not rejecting it. |
| `c4-process-policy-producer-wire` | 2 / 0 / 2 | The retained envelope proves Responses parsing of a Chat Completions fixture response. The second host normalizes the managed provider identity. |
| `c4-process-policy-producer-provider` | 2 / 0 / 2 | A shadow-catalog clone is absent from the primary database provider registry; setup refuses before launch. |
| `c4-process-policy-producer-registry` | 2 / 0 / 2 | Registry-owned provider reaches successful approval/publication. Reusing the one-use activity ID correctly refuses the replay attempt. |
| `c4-process-policy-producer-final` | 121 / 119 / 2 | All 12 public cases pass. Two old transfer assertions incorrectly equate cancelled runtime status with finished cancellation recovery. |
| `c4-process-policy-producer-transfer` | 49 / 48 / 1 | All 44 persistence cases pass. A legacy producer fixture still calls the retired unconditional Project deletion method. |
| `c4-process-policy-producer-lifecycle` | 130 / 130 / 0 | Both retirement fixtures use the existing governed lifecycle driver. Producer/definition races, dispatch, review and host-contract regressions pass. |

Every completed candidate above has verified source/output identity and final process,
database-name and temporary-family reconciliation with zero new residue. The current
selection has 251 distinct cases across twelve exact topics. The transfer repair changes only
`ProcessPreparedLaunchPersistenceTests.cs`. It reruns all 44 cases in that topic plus the
130 previously unexecuted cases; 77 earlier passing cases are explicitly carried across
that verified test-source boundary. This is not one aggregate run. The full selection and
source-change bridge remain in `c4-process-policy-producer-transfer`.
The final repair uses the existing `ProjectLifecycleTestDriver` to capture and submit
original retirement intent. Source review found the same retired call in the unexecuted
definition-lifetime race fixture; both are repaired before the next candidate freezes.
`c4-process-policy-producer-lifecycle` carries the earlier 77 plus 44 passing cases and
runs all five producer API cases and the 125 previously unexecuted cases. All 130 pass.
Production source remains unchanged across these test repairs. The final
`combined-qualification.json` verifies the exact three changed test-source paths,
12 disjoint topics and 251 distinct passing cases. It verifies every contributing
candidate's source/output record and closed resource observer, rather than adding
duplicated pass counts from diagnostic attempts.

The final candidate verifies all 1,224 source pins and 66 owning output identities.
Its observer covers all seven test commands; reconciliation finds zero live owned
processes, database-name changes or new temporary-family residue. Normal Release
outputs were reused and refreshed. No custom configuration or replacement database
stack was created. The isolated PostgreSQL server remains shared with the next lanes;
the ordinary application on 5032 was not used as a fixture.

## Architecture and static status

Final operation-admission snapshot `snap-20261023100837-da94960e` retains four intended
project references, six existing Workbench module/type cycles and no new findings or
references against the preceding scoped snapshots. The collector still reports 189
WS0002 errors, 12 DI0001 informational diagnostics and four DI0002 warnings. Direct builds
pass; this scoped comparison does not assert global solution health.

The operation candidate's full portability scan reports one added executable finding:
`2eaa4f0e231c9187`, the explicit ordinal case-insensitive operation-name comparison in the
new Workbench provider. It matches the existing publication protocol comparison, not
filesystem case behavior. The refreshed final-source scan is nontruncated (10,076 files,
47,390 findings). The reviewed baseline diff adds exactly that allowance, from 15,688
to 15,689; final enforcement without `--write-baseline` passes. Portability tooling
tests (6), secret-scanner tests (4), rendering-tool tests (7), the rendering register
and documentation validation also pass. No executable source changed after that scan.
Twelve obsolete bulk scan JSON/CSV files from five candidates were verified and removed;
compact summaries, hashes, the reviewed finding and enforcement logs remain in their
`bulk-scan-*-cleanup.json` records. Historical denied cleanup was not retried.

Artifact scanning covers 1,282 text files across all 15 producer/schema proof roots,
with no actual database-password sentinel finding or oversized omission. The 42 lexical
findings are the same private file-read and literal PowerShell variable expressions in
14 runners. One 38-byte Windows error log needed an explicit legacy-encoding review;
it contains only the earlier command-path error. The separate review records these
limitations instead of treating report-only exit status as a clean scan.

## Client contract and signed checkpoints

The actual isolated schema export in `c4-process-policy-schema` preserves all 317 routes:
4,526,785 bytes, SHA-256 `918aec9946630060a846bf94c3e353517a1ca221dd32ace33b0119171d685f64`.
It adds selected execution/policy request and effective review contracts. The existing
Release schema probe is reused; the HTTP fixture is disposed with no process, database or
temporary-family delta. Its first observer setup attempt lacked the local deadline helper;
that failed before a host started, and the exact companion was copied before export.

SPA generation, 25 generator tests, eight actual SDK/transport tests, 29 app tests, app
typecheck/build, owned formatting and lint pass. The four new SDK tests cover both snapshot
modes/effective policy, exact intent/preparation preservation, conflict and no retry on 503.
The app's separate zero-discovery Vitest lane adds no coverage. There are no UI rendering
changes or new browser-coverage claims. Signed SPA commit `0fa527a99a85f2e79605c00369bdaf588d4cccfa`
matches the reviewed tree on `codex/concurrent-mutations`; the worktree is clean and no
merge or push was performed. The direct `pnpm.cmd` runner attempt failed before discovery;
the established PowerShell invocation then passed all actual tests.

Normal signed commits now preserve P17/P18 `cf5b3ece6`, P19 `cd588a82b`, P20 `dd539f195`
and P21/P22 `b149693af`. Each frozen patch was applied to the exact predecessor index/tree.
SPA's earlier validated client checkpoint is signed as `97202a7` on
`codex/concurrent-mutations`; it has not been merged or pushed to main.

P23's Behavioral closure gate passes for the explicit public single-activity producer
and generated client contract. The positive uses actual authoring, HTTP check/accept,
the normal queue, real MAF and publication owners, public manager review and terminal
accounting. Negative cases prove specific unsupported-contract/policy/permission refusals,
no preparation/funding/runtime/provider side effect, original-intent replay and changed
selection conflict. Seeded downstream tests are supplementary evidence.

The signed backend identity is recorded separately after this source tree is committed.
Native/SPA policy selection and review rendering, authored definition defaults/versioning, parent Fail/Replan/shared refresh,
mixed or multiple activity lifecycle, Workflow activation, remaining mutable owners,
sustained capacity and final engine E2E remain open. Historical denied cleanup targets
remain recorded; clean new fixtures do not imply whole-bundle cleanup.
