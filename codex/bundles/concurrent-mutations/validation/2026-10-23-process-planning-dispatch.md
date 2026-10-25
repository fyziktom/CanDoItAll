# Registered Process planning and terminal shared-scope validation

Status: P21/P22 behavioral qualification passed, 2026-10-23. All 588 current cases pass.
This record does not authorize public policy activation or claim end-to-end engine
completion. The exact frozen checkpoint was subsequently signed as `b149693af`.

Design: [P21 dispatch](../plan/P21-process-planning-dispatch.md) and
[P22 terminal shared scope](../plan/P22-terminal-shared-scope.md).

## Preserved candidates

| Candidate | Discovery / execution | Result and interpretation |
| --- | --- | --- |
| `c4-process-planning-dispatch` | 425 / 425 | 415 passed, 10 failed. Eight new-fixture errors; two real RunSnapshot terminal-scope failures. |
| `c4-process-planning-dispatch-repair` | 57 / 57 | 56 passed, one new assertion failed because the retained claim includes its original admitted contract. Exactly three test sources changed; production remained identical. Three additional real dispatch cases were added. |
| `c4-process-terminal-scope` | 577 / 577 | 564 passed, 13 failed. All failures reached the existing material guard, which prohibited any combined root-scope and local-decision command. Nine serial Release builds passed. |
| `c4-process-terminal-scope-repair` | 588 / 588 | All passed. Exact two-file repair permits terminal-result scope commands with terminal decision closures and adds 11 positive/negative material-guard cases. Seven serial Release rebuilds passed. |

The first two runs verified the same 66 owning/copied binary outputs and all 1,121
source pins at their own boundaries. Each observer covered every test command, with
zero remaining observed processes, database-name delta or unowned temporary entries.
Initial failures, test sources, command transcripts and result files remain preserved.
The P22 candidate expands the source inventory to 1,209 files and adds explicit failure
injection after both SQL flushes, stale-scope and unfinished-root-work coverage.
Its final source/output verification passed; all 17 commands were observed, with zero
remaining processes, database-name delta or unowned temporary entries. The repair pins
1,210 sources and preserves the original failed candidate, rather than replacing its
results. Ordinary capture/reconciliation commands retain their existing restriction.
The final repair verified all 1,210 source pins and 66 owning/copied outputs again after
execution. Its observer covered all 18 commands; reconciliation found zero remaining
observed processes, database-name delta or unowned temporary entries.

## Behavior and exact proof

The owned user requirement is safe concurrent user/Agent/Process work with deliberate
snapshot semantics and no accidental cross-module dependency or provider-held lock.
The shipped slice routes an already admitted exact task-planning contract through the
registered Process adapter and Workbench owner, retaining original input, material,
claim and measured accounting. Pending preparation cannot release another caller's claim
or invent a child. A final governed root result closes shared material in the same SQL
transaction as the runtime, child and budget; descendants cannot close the root scope.

The shallow-pass trap is a directly invoked test planner, a new child on replay, or a
terminal runtime with a still-active shared scope. `ProjectTaskPlanDispatchTests` executes
the real registered adapter/strategy, holds provider and material preparation while a
second caller races, changes the mutable assignment prompt, and rejects missing original
journals and unsupported obligations. Manager approval publishes once; rejection publishes
nothing. Both retain the exact original accounting/scope receipt on replay.

`ProjectTaskPlanStartupFailureTests` injects exceptions after each real SQL flush, checks
Active then Cancelled scope state inside the transaction, and reads unchanged runtime,
decision, child, budgets, scope, idempotency rows and event count from fresh contexts after
rollback. The same original action then commits once and replays without live startup
evidence. Stale scope and an extra open root reservation reject without partial settlement.
The 11 material-guard cases allow only terminal closures, refuse payload capture and every
ordinary scope command/local decision combination, and retain command-shape validation.

| Owning test topic | Discovered / passed |
| --- | --- |
| Unit `ProcessDecisionMaterialCommandTests` | 11 / 11 |
| Unit `ProcessDecisionRunScopeCompletionTests` | 9 / 9 |
| Unit `ProcessDecisionRunScopeTests` | 38 / 38 |
| Integration `ProjectTaskPlanStartupFailureTests` | 23 / 23 |
| Integration `ProjectTaskPlanProcessReviewTests` | 14 / 14 |
| Integration `ProjectTaskPlanDispatchTests` | 15 / 15 |
| Unit `ProcessDecisionReviewCoordinatorTests` | 35 / 35 |
| Integration `ProcessDecisionCompletionPersistenceTests` | 38 / 38 |
| Integration `ProcessDecisionRunScopePersistenceTests` | 9 / 9 |
| Integration `ProcessDecisionRunScopeRecoveryTests` | 12 / 12 |
| Integration `ProcessRootCancellationAdmissionPersistenceTests` | 2 / 2 |
| Unit `ProcessRuntimeDispatchApplicationServiceTests` | 42 / 42 |
| Unit `ProcessRuntimeIntegrationAdapterTests` | 263 / 263 |
| Unit `ProcessDecisionSingleActivityAdmissionTests` | 23 / 23 |
| Unit `ProcessDecisionResultCoordinatorTests` | 30 / 30 |
| Integration `ProcessStepMaterialProducerTests` | 13 / 13 |
| Integration `ProjectTaskPlanReviewCompletionTests` | 9 / 9 |
| Integration `ProcessInheritedPublicationCancellationRaceTests` | 2 / 2 |

Commands and exact filters are retained in the final artifact root
`.artifacts/concurrent-mutations/validation/c4-process-terminal-scope-repair`:
`Build.ps1 -Attempt 1 -StartAt CanDoItAll.Processes.Persistence`,
`Discover.ps1 -Attempt 1`, `Confirm-Candidate.ps1`, `Start-Observer.ps1 -Attempt 1`,
`Execute.ps1`, `Confirm-Candidate.ps1 -Verify`, `Reconcile.ps1` and `Summarize.py`.
Each test uses its owning Unit/Integration project, Release/local-library outputs,
`--no-build --no-restore`, and the manifest's exact namespace/class prefix. Discovery
validates both the count and source method names. All commands record their exit code,
elapsed time, output bound and process identity; the TRX files retain every case.

The initial nine direct builds cover Runtime, Application, Persistence, Workbench,
Processes module, Composition, Web and both test assemblies. The repair rebuilds
Persistence and all six affected downstream outputs. No custom configuration/output
tree or replacement PostgreSQL stack was created. No new UI was changed in this slice.

`Static-Final.ps1 -Attempt 1` passes the full portability tool/secret tests, rendering
tool tests, rendering register (1,212 inputs), scan, no-write enforcement, documentation
and whitespace checks. The untracked-inclusive scan covers 10,066 files, is not truncated,
and leaves all 15,688 reviewed executable-source findings unchanged. No baseline refresh
is needed. The two bulk scan files (50,590,549 bytes) were individually identity-checked,
removed and verified after retaining their hashes, scan metadata, Markdown summary and
command results in `bulk-scan-cleanup.json`. Earlier denied cleanup is still separate.

The new production owners contain no fixture branching, placeholder implementation or
provider simulation. Pure Runtime policy/transition tests are isolated; SQL/file/MAF
integration tests exercise the actual owners. Public launch admission is still absent,
so these cases do not substitute for P23's real operator producer proof. Broader Stable
remains a final frozen-checkpoint gate; this focused run is not an unfiltered-suite claim.

## Architecture evidence

P21 before/after snapshots: `snap-20261023063751-745762b0` and
`snap-20261023065848-745762b0`. Both have the same two scoped project references and six
existing module/type cycles. The new executor's informational member-count finding is
reviewed as one cohesive business adapter; it delegates capture, authority, execution
and accounting to their existing owners. There are no new project references or
business partials. The collector reports 189 WS0002 errors and two DEP0002 warnings;
direct evaluated references and the seven successful Release builds supplement this
limited snapshot. A globally healthy solution graph is not claimed.

P22 before snapshot: `snap-20261023072639-65134217`, scoped to Runtime, Application and
Persistence. It reports three intended project references, no scoped cycle, 883 types,
7,447 members, 233 findings and 189 WS0002 errors. Initial post-change snapshot
`snap-20261023074559-65134217` retains the same three project references, zero scoped
cycles, 233 findings and 189 diagnostics; it has 884 types and 7,457 members. Final repair
snapshot `snap-20261023081255-fb03b32e` has the same counts, project edges, zero scoped
cycles and no new findings. Direct evaluated references and successful builds supplement
the collector, which still reports blocking workspace errors. No globally healthy graph
is claimed.

## Remaining closure

The current slice passes its behavioral and architecture gates. Public Process policy
admission proceeds through the reviewed [P23 design](../plan/P23-process-policy-producer.md),
with explicit contract selection and preserved original intent. Shared refresh/replan,
governed Workflow activation, mixed-contract/failure recovery and sustained capacity
remain separate gates. The pending signed checkpoint and frozen P19/P20 patches remain
unchanged. SPA stays on `codex/concurrent-mutations` for colleague review.

Historical cleanup denials remain unresolved, including the two P20 bulk portability
files. This continuation has not retried those denied deletions or created replacement
database stacks.
