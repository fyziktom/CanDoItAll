# Terminal-child Process accounting

Status: qualified for P11 A. All 178 selected cases pass, including 61 new cases.
This is the accounting prerequisite for parent cancellation/recovery. It does not
activate Agent stopping, a recovery worker, finalization fencing, refresh or producers.
SPA remains clean on `codex/concurrent-mutations` at signed `70c65dd`.

## Behavior and ownership

The [design](../plan/P11-terminal-child-accounting.md) adds one typed request and one
Application coordinator, registered in the existing Process module. It verifies the
original accepted input, child, execution contract, displayed decision/budget revisions,
terminal step and identified actor. Completed, Failed and Cancelled steps are supported;
Skipped steps are not. A live selected claim or unfinished/uncertain child refuses.

Exact child closure and measured work settle through the existing root-fenced unit of
work. The canonical concurrency timestamp advances; all business state, claims, results,
events and outbox remain unchanged. An accepted effect stays accepted even when the
Process step is cancelled. Shared scope reconciliation permits local accounting without
claiming that the scope is complete. Other local work prevents final decision closure.
Already settled work cannot be charged again.

The retained action identity includes original input/binding and actor. Its exact
receipt recovers before current-phase admission, without another Agent observation.
A different actor/action cannot adopt it. Corrupted original material, work, child,
status or completion fails explicitly. This lower-level receipt is historical; a
future product boundary must reload current runtime separately.

## Validation

| Lane | Discovered and passed | Execution seconds |
| --- | ---: | ---: |
| New isolated terminal accounting | 53 | 4.42 |
| Existing review coordinator | 35 | 3.90 |
| Existing review completion | 38 | 3.92 |
| Existing result coordinator | 30 | 4.10 |
| New real SQL/file terminal accounting | 8 | 223.83 |
| Existing product accounting recovery | 14 | 389.73 |
| Total | 178 | |

All six exact class filters use Release, `--no-build --no-restore`, local library
references and `/m:1`; source method inventories match built discovery before execution.
There are no failed or skipped cases in this candidate.

The new SQL/file cases cover RunSnapshot and StepSnapshot, approved and rejected
children after parent stop, shared reconciliation, exact root/local budget charge,
unchanged sibling/business state, two competing actions, lost acknowledgement and
rollback after SQL flush. Recovery uses a fresh DI scope. This scope does not claim a
new full host-restart test. Agent files and original provider/Project effect counts
remain unchanged. The 14 existing product recovery cases provide the dependent flow.

Five serial standard Release builds pass: Application 20.18 s, Process module 97.71 s,
Web 61.94 s, Unit 31.07 s and Integration 33.82 s. Pre-build and frozen source hashes
are identical. Final verification confirms 9,913 source inputs and 7,740 runtime files
unchanged throughout execution. Ten standard output roots include the direct Process
module in addition to the previous nine-root selection.
The post-proof documentation bridge checks exactly six documentation changes against
the frozen inventory; compiled source and the complete file set otherwise stay fixed.

Full portability scan and final enforcement pass with 15,665 reviewed executable-source
findings unchanged; no baseline rewrite was needed. Rendering register passes for 1,212
inputs. Documentation and whitespace checks pass. Exact commands and discovery names
are retained in the proof. Final integrated Stable remains an open E2E gate; this new
owner does not alter an existing schema or shared runtime contract.

## C# architecture gate

Status: Pass for P11 A.

Application snapshots `snap-20261021233051-8c37828d` and
`snap-20261021235110-bef0246a` show 130 to 132 documents, 369 to 371 types,
4,026 to 4,044 members and 459 to 467 edges, with zero cycles and zero diagnostics.
Actual Application/Process module references remain eight/26. There is no new partial,
provider dependency, service locator, schema or reverse implementation reference.

The one new informational finding counts 11 coordinator members, including four
diagnostic constants and private input/identity/receipt checks. Its 128-line source
range has one responsibility: exact terminal child accounting. Splitting those checks
into unrelated wrappers would not improve the boundary. Independent tests use narrow
existing ports; actual registered SQL/file tests exercise the real owner and transaction.

The continuation audit found a separate prerequisite: rework of a previously Failed
step can change a CancelRequested root back to Active. Repair runtime admission and
the persisted barrier before using root cancellation as a permanent Agent-stop
capability. This does not invalidate P11 A's same-transaction runtime CAS and unchanged
terminal step. No unsupported stop/finalization behavior is enabled here.

## Evidence and resources

Retained proof: `proof/2026-10-19-planning-authority/c4-terminal-child-accounting.json`,
566,858 bytes, SHA-256
`3909728F8D4ABF10961FA1B4A7903451D57D874AA689FF59EBC1C78A9FC7152E`.
Secret scan reports zero findings. Source manifest SHA-256 is
`0361BC68D21FFD7942D698F38C77C0669FE9F1367769E33AE467F12B91B62D2F`;
runtime manifest is `2F31A4C563A6BAFBF20FD8A6F413A4FAEB1CB4F2D134F4A82475706095F0DFF7`.

The observer covered all six test commands for 654.68 s. No new surviving observed
process, database-name or temporary delta remains. The CPU sampler closed normally
with 198 visible-process samples: median 7.22%, maximum 14.82%. These are context for
this correctness run, not a capacity qualification. CodeAnalytics preparation for the
next phase ran during these correctness tests; no latency gate is inferred.

Ownership preceded validation-directory creation. Standard output trees and the existing
isolated PostgreSQL server were reused. Ordinary port 5032, SPA main and historical
cleanup-denied resources remain untouched. Prior automatic-review cleanup denials are
still listed in [resources](../resources.md); zero new residue is not complete historical
cleanup. Retain only this reviewed proof, manifests and reusable helpers for continuation.

## Progression

P11 A passes its affected-scope closure gate. Next repair cancellation-barrier
monotonicity, then qualify selected Agent stopping, durable quiescence/recovery and
snapshot refresh. Continue measured Agent/read and database-connection diagnosis;
finite latency and sustained capacity are still open. Producer, Workflow/settings/SPA,
remaining writer waves and complete E2E closure remain required.
