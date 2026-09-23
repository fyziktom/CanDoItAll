# CRM / HR pre-merge readiness

The current verdict for integrating `components-decoupling` into `development` and then into `main`.
It supersedes the [module-decoupling merge readiness](modules-decoupling/MERGE_READINESS.md), which
is the historical handoff of the branch that came before this one. The CRM / HR module's own
completion and coverage record is [crm-hr-ui-completion.md](crm-hr-ui-completion.md), and the durable
rules the extraction established are in [ui-component-seams.md](ui-component-seams.md).

## Verdicts

| Verdict | Value | Condition behind it |
|---|---|---|
| `CRMHR_FUNCTIONAL_CLOSURE` | `PASS` | All seven areas render from the rendering library behind their view contracts; the two admitted gaps of the module closure (the post-dispatch draft and the committed-with-warning read-back of the four editing hosts) are closed with reproductions; every CRM / HR lane and the browser journeys pass on the closure tree. The residual is a named list of secondary editor fields that no executed test drives through the UI, under [retired browser scripts](crm-hr-ui-completion.md#retired-browser-scripts); their persistence is covered at the owners. |
| `DURABLE_DOCUMENTATION` | `PASS` | The rules are promoted into [UI component seams](ui-component-seams.md) and linked from the instruction files, the architecture overview, the module map and the root README; no maintained document needs a bundle path; the validator passes on a tree that has none. |
| `DEPENDENCY_REPRODUCIBILITY` | `REMOTELY_REPRODUCIBLE` | The sibling dialog repair `ff528974` is the published `development` head of `CanDoItAll.Components` by `git ls-remote`, CI pins it, and the FileTools pin and checkout share one tree. The package-mode fallback is not exercised anywhere and would resolve a package that predates the repair; publishing one is optional and is not a merge prerequisite. |
| `BUNDLE_FREE_SIMULATION` | `PASS` | Every lane of the closure checkpoint ran on the filtered tree. It found two real defects, both repaired: three documents linked into the bundle, and the sandbox's Fast asset mode failed on a clean checkout. |
| `FINAL_GATE` | `PASS` | The broad Stable gate on the closure tree: exit 0, 179.1 minutes, **14 385 executed, 14 385 passed, 0 failed, 0 skipped** (Unit 8841, Components 2303, Integration 3016, Memory 203, AgentFramework.Memory 22). All three results that were non-green at the module closure are inside this filter and passed. |
| `ACTUAL_BUNDLE_REMOVAL` | `OPERATOR_PENDING` | The operator removes the bundle roots and commits that removal; the post-removal checks below run on the resulting commit. |
| `MERGE_TO_DEVELOPMENT` | `READY_FOR_OPERATOR_ACTION` | No unpublished dependency, no unresolved material defect, no missing required proof, no failing static gate and no false live evidence. Conditional on the post-removal checks and on the refs below not having moved. |
| `MERGE_TO_MAIN` | `RECHECK_AFTER_DEVELOPMENT` | The analysis below is against `main` as it stands today. The tree the operator will actually merge into `main` is the `development` result, which does not exist yet; validate that result before integrating it. |
| `MERGE_PERFORMED` | `NO` | |
| `PUSH_PERFORMED` | `NO` | This work pushed nothing. `origin/components-decoupling` does carry these commits: the operator published the branch themselves at 2026-09-18 13:33 local time, while the Stable gate was running. |

## Candidate and targets

Refreshed at the start of this work; refresh again before acting on the sequence below, because a
target that moves invalidates the merge analysis.

| Ref | Commit at inspection |
|---|---|
| `components-decoupling` local head at handoff | `a220d7e682548b842070f8976c33c159a6872808` (entry: `a8384e239e4903e8ca373d9b095a79cd3307e82e`) |
| `origin/components-decoupling` at handoff | `a220d7e682548b842070f8976c33c159a6872808`, published by the operator during this work |
| `origin/development` | `d446dc2bad461c7e753cceb53a7969d6ff6b9cb2` |
| `origin/main` | `10a72521aae7cbcd5d5bc2b7c16366d496ef8285` |
| Sibling `CanDoItAll.Components` | `ff5289746573a6dd6456754a7764844627ddd49f`, the `development` head on GitHub |
| Sibling `CanDoItAll.FileTools` | checkout `7c7453c6583365ae5bd63f8fc6efc4a776e15818`, CI pin `498b36825bd5a5222429972af120b04becf4b3f6` |

## Merge analysis

`git merge-tree --write-tree` against both targets, without moving a ref, creating a merge commit or
touching the working tree:

| Merge | Exit status | Result |
|---|---|---|
| `origin/development` + candidate | 0 | merged tree `b750e79eb0d26147277ff4ef5d5640c21ab0995b` equals the candidate's own tree |
| `origin/main` + candidate | 0 | the same tree |

Re-run at handoff: the candidate is 111 commits ahead of `origin/development` and 117 ahead of
`origin/main`, and 0 behind either.

The merge base of the candidate with `origin/development` is `origin/development` itself, and with
`origin/main` is `origin/main` itself: both targets are ancestors, so each integration is a
fast-forward with no content conflict and no target-only change to reconcile. This is a content
analysis; it says nothing about runtime correctness, which is what the gates below are for. The
operator's own removal of the bundles changes the proposed tree by exactly those paths, so the
post-removal checks below run on the tree that is actually merged.

## Dependencies

The CRM / HR browser journeys depend on a dialog repair in the sibling `CanDoItAll.Components`: a
dialog now completes only its current open request, and a dialog opened inside another re-raises its
descendants after `showModal()`. That repair is commit `ff528974`, it is signed, and
`git ls-remote` shows it as the `development` head of that repository, so a fresh clone can obtain
it.

CI pinned `7b618cda`, the commit **before** that repair, so its stable and container jobs built a
graph the journeys were never proven against. The pin is now `ff528974`. The two commits differ in
exactly five files, all of them the dialog change and its own tests.

FileTools needs no change. The sibling checkout `7c7453c` and the CI pin `498b3682` are different
commits with the **same tree** `6bc360281b6ad13ddec5e813f0a00e26b5bc7d6d`, so the pinned source is
the source in use; commit-derived build provenance is not claimed to be identical.

The package fallback (`UseLocalCanDoItAllLibraries=false`, `CanDoItAllComponentsPackageVersion`
`0.3.0`) is not exercised by CI, by Docker or by the local gates, all of which build the sibling
source. A package-mode build would resolve a published `0.3.0` that predates the dialog repair.
Publishing a package that contains it is an operator decision and is not required for the merge.

## Upgrade from the target schema

The candidate adds eight migrations that `development` does not have. `MoveWorkItemAssignments` is
the one that moves operator data: work-item assignee rows leave the CRM / HR table and become the
Workbench owner's. The migration verifies itself in SQL (row count, a field-by-field `EXCEPT`, and
the exact deletion count, each raising on a mismatch), and
`MergeTargetSchemaUpgradeIntegrationTests` upgrades a database that already holds rows: a work
assignment and a participation are written at the merge target's schema, the chain is applied, and
the assignment is read back from its new owner by its own identifier with its project, party,
opportunity, node key, allocation and primary flag, while the participation stays with the CRM / HR
owner. A fresh empty database is not accepted as upgrade proof anywhere in this record.

## HTTP and tool compatibility

`src/App/CanDoItAll.Web/Api/CrmHrApi.cs` and `CrmHrApiContracts.cs` are byte-identical to the merge
target: the CRM / HR wire contract did not change. The CRM / HR editor models and enumerations moved
into `CanDoItAll.Modules.CrmHr.Contracts` keeping their namespaces and members, which is an assembly
move, not a contract change. The HTTP boundary is exercised on the real host by
`CrmHrApiIntegrationTests`.

No file under the Web host's API surface changed after `160616c8`, the head the family OpenAPI
snapshot was captured from, so that snapshot still describes this candidate. The additive API
comparison recorded for the module-decoupling work therefore stands unchanged; this branch adds no
route, operation, schema, tool name or authorization scope of its own.

## Architectural state of the module

All seven routed CRM / HR areas (Home, Directory, CRM, Workforce, Recruiting, Agents, Assignments)
render from `CanDoItAll.CrmHr.UI`. Each routed page keeps route identity, injected services, reads,
mutations, navigation, notifications, agent context and effect lifetimes, implements its workspace
view contract in a `…Page.View.cs` partial, and composes the library's workspace surface. The
[completion record](crm-hr-ui-completion.md) holds the per-area map, the renderer inventory and the
dead code that was removed with its reference evidence.

Six Razor files stay in the module on purpose, each with a named role: the account summary, the
interaction timeline, the financials panel, the recruiting agent evidence panel, the agent chat
context provider and the secondary tabs. They are effect adapters whose owner is the host, not
renderers; `CrmHrUiModuleBoundaryTests` lists the module's components by role so a new business
renderer cannot quietly appear beside them.

The two editing invariants live in `Components/`: `CrmHrMutationGate` (one admitted write per editor
lifetime) and `CrmHrDraftReconciler` (what a read-back does to the drafts, including the submission
ledger described above). Both are module-owned; the rendering library performs no write.

## Documentation migration

The temporary architecture bundle the operator is removing held the rules several extractions
established. They now live with a maintained owner:

| Temporary source | Maintained owner |
|---|---|
| Component ownership and placement; anti-patterns and decision rules | [UI component seams](ui-component-seams.md), Placement and Anti-patterns |
| State, intent, lifetime and routing readiness | UI component seams, State and intent, Edit and effect lifetime |
| Service, I/O and controller seams; mutation and failure boundaries | UI component seams, The seam, Mutation outcomes, Reconciliation after a commit |
| Sandboxability and project boundaries | UI component seams, Scenario host and production proof; each sandbox README |
| Test and architecture guard hygiene | UI component seams, the proof-layer table; [Testing](../testing.md) |
| Measurement of the development loop | UI component seams, Measurement; the [completion record](crm-hr-ui-completion.md) keeps the CRM / HR numbers with their conditions |
| The bundle's own execution plans, status blocks, approval scaffolding and task prose | nothing; they were coordination state, not guidance |

What points at the new owner: [AGENTS.md](../../AGENTS.md), the
[engineering instructions](../../.github/copilot-instructions.md), the
[architecture overview](overview.md), the [module map](modules.md) and the root
[README](../../README.md). `CLAUDE.md` no longer opens a continuation of a finished task.

No maintained document requires a bundle path any more. The two format rules that mention working
bundles (the documentation validator's sealed-evidence exception and the CI guard that forbids a
bundle path in the workflow) are about the format and the policy, not about a directory that has to
exist; the validator's own tests build a disposable fixture.

## Repairs in this change

| Repair | Reproduction | Proof |
|---|---|---|
| Text typed after a save was dispatched was discarded by the read-back that followed the commit, and a create editor the operator had already started the next record in was cleared by the previous one's completion | The owner call is held open while the test types a second value into the same rendered field | `CrmHrPostDispatchEditTests` (CRM profile and connections, Directory party, Workforce skill), `CrmHrDraftReconcilerTests` |
| The committed-with-warning lifecycle of the four editing hosts had no automated fact | A controlled failure of exactly the owner read each host issues after its commit | `CrmHrCommittedReadBackTests` |
| CI consumed a sibling without the dialog repair the journeys depend on | The pin and the repair commit are different trees | `CrossPlatformCiWorkflowTests`; the journeys run against the pinned source |
| A canvas-preview test stalled on every run of `WorkflowsPageTests` | Each component fixture leased a database and never returned its client pool, so a class of forty-three fixtures exceeded the server's connection limit | The class passes forty-three of forty-three three times over after the lease returns its pool |
| The six quarantined CRM / HR browser scripts executed nothing and hid a field-coverage gap | All six were run and all six failed on replaced selectors and the assignment admission | `CrmHrEditorFieldCoverageTests` covers the fields they uniquely drove; the rest is named in the completion record |
| The opt-in live lane reported a closed gate as a passing test | This runner does not honour dynamic skip for these projects | The evidence manifest records `execution` as `not-run`, `rehearsal` or `live` |

## Closure checkpoint

`CRMHR-PREMERGE-CLOSURE`. One source state: an isolated, task-owned copy of the frozen candidate
`a220d7e682548b842070f8976c33c159a6872808` with the bundle roots omitted, created with
`git worktree add --detach` and then `git rm -r --cached` of the bundle directory with the directory
deleted, so tooling that reads the index sees the post-removal set while the operator's own checkout
stays untouched. 6408 tracked files remain of 6575; the 167 omitted are exactly the tracked bundle
files and nothing else differs from the candidate. Release, `/m:1`, discovery stated before every
run, SDK 10.0.303, Windows 10.0.26200 x64 with 20 logical processors, siblings Components
`ff5289746573a6dd6456754a7764844627ddd49f` and FileTools `7c7453c6583365ae5bd63f8fc6efc4a776e15818`
(tree-identical to the CI pin).

The simulation is evidence for this filtered tree. It is not proof that the branch has had its
bundles removed; that remains the operator's action, and the post-removal checks below are what
confirm the tree that is actually merged.

| Lane | Command / filter | Discovered | Result |
|---|---|---:|---|
| Builds | `CanDoItAll.slnx`; the Stable and Playwright test solutions; the sandbox with `-p:CrmHrAssetMode=Parity` and with `=Fast` | 5 builds | all exit 0. Fast first failed on the clean checkout with the message naming its two npm commands; after `npm ci --prefix Tailwind` and `npm run crmhr:css:build` it builds |
| Documentation | `./tools/Validation/Test-Documentation.ps1`; `./tools/Validation/Test-DocumentationEvidence.ps1` | 228 files; 9 tests | PASS. The first run of this lane is what found the three documents that linked into the bundle |
| Portability / static | `test_enforce_portability_baseline.py`; `test_scan_artifacts_for_secrets.py`; `scan_portability.py --repo-root . --tracked-only`; `enforce_portability_baseline.py` without `--write-baseline` | 6 + 4 self-tests | PASS, `14719 reviewed executable-source findings unchanged`; no baseline entry added or refreshed |
| Unit CRM/HR and agent tools | `FullyQualifiedName~CrmHr\|FullyQualifiedName~CrmAgent\|FullyQualifiedName~HrAgent\|FullyQualifiedName~CrmPlanning\|FullyQualifiedName~ProjectAssignmentGanttProjectionAdapterTests\|FullyQualifiedName~MafAgentRuntimeToolProviderCompositionTests` | 313 | 313 passed, 0 failed, 0 skipped (3 s) |
| Components CRM/HR topic | `FullyQualifiedName~CanDoItAll.Tests.Components.CrmHr.\|FullyQualifiedName~OpportunityBoardTests\|FullyQualifiedName~AssignmentEditorAdmissionTests` | 302 | 302 passed, 0 failed, 0 skipped (3 m 54 s) |
| Components downstream of CRM / HR | every Components class that consumes CRM / HR parties, assignments or the record-browsing family: `FullyQualifiedName~PartyAddressesEditorTests\|~PartyAffiliationsEditorTests\|~PartyContactMethodsEditorTests\|~PartyPickerTests\|~PartyRelationshipsEditorTests\|~ProjectMultiPickerTests\|~ProjectPartyAffiliationPresentationTests\|~ProjectPartyAssignmentRevisionPricingTests\|~ProjectStructureAgentTaskResourceCostStrategyTests\|~ProjectStructureGanttPanelTests\|~ProjectStructureGanttProjectionAdapterTests\|~ProjectStructurePageTaskAssigneeCreationTests\|~ProjectStructurePartyPickerTests\|~ProjectStructureTaskApplicationServiceTests\|~ProjectStructureTaskAssigneeSelectionPolicyTests\|~ProjectStructureTaskCreationServiceTests\|~ProjectStructureTaskDetailsServiceTests\|~ProjectStructureTaskPricingResourcePolicyTests\|~ProjectStructureWorkItemAssigneeServiceTests\|~WorkforceRecordBrowserTests` (each `~` is `FullyQualifiedName~`) | 106 | 106 passed, 0 failed, 0 skipped (2 m 13 s) |
| Integration CRM / HR owners and HTTP | `FullyQualifiedName~CrmHr\|~CrmFinancialSnapshotQueryIntegrationTests\|~CrmInteractionIntegrationTests\|~CrmPartyCommandIntegrationTests\|~CrmPlanningRuntimeIntegrationTests\|~OpportunityConversionIntegrationTests\|~OpportunityIntegrityIntegrationTests\|~PartyDirectoryIntegrityIntegrationTests\|~PartyMergeIntegrationTests\|~PartyOrganizationAffiliationIntegrationTests\|~RecruitmentLifecycleIntegrationTests\|~StaffingAllocationIntegrationTests\|~WorkforceProfileIntegrationTests\|~ProjectPartyAssignmentIntegrationTests\|~ProjectWriteAdmissionIntegrationTests\|~WorkAssignmentAdmissionIntegrationTests\|~RecordQueryIntegrationTests\|~AiAgentDirectoryQueryIntegrationTests\|~AgentRecruitingApiIntegrationTests\|~MafHrResultDisclosure\|~MergeTargetSchemaUpgradeIntegrationTests` | 165 | 165 passed, 0 failed, 0 skipped (8 m 31 s). Executed on `f054617b5`, one commit before the affiliation repair; the Stable gate below re-executes every one of them on the final candidate |
| Integration assignment and project lifetime | `FullyQualifiedName~AgentProjectLifetimePersistenceTests\|~ProjectLifetimeMigrationIntegrationTests\|~ProjectPartyAssignmentMoveReceiptIntegrationTests\|~TaskAssignmentAdmissionHttpTests\|~WorkAssignmentCoherencePersistenceTests\|~WorkAssignmentMigrationIntegrationTests\|~WorkAssignmentOwnerPersistenceTests\|~WorkbenchAssignmentOwnerPersistenceTests\|~WorkspaceAgentRecruitingTargetResolverTests\|~CrmProjectionMigrationIntegrationTests` | 74 | 74 passed, 0 failed, 0 skipped (2 m 53 s) |
| Production Playwright | `(FullyQualifiedName~CrmHr\|FullyQualifiedName~ProjectStructureTaskAssigneeJourneyTests)&Category!=Quarantined&Category!=LiveAgent` on the real Web host and PostgreSQL | 20 | 20 passed, 0 failed, 0 skipped (3 m 28 s). The run before the affiliation repair failed one of them, which is how that defect was found |
| Live model-backed tools | `Category=LiveAgent` with both live variables set | 2 | 2 passed, both manifests `execution=live`: the planner read used 2 model requests and the managed HR create 6, of a bound of 10 each, against the seeded OpenAI profile and model `gpt-5.4-mini` |
| Broad Stable gate | `CanDoItAll.Tests.Stable.slnx` with the CI filter `Category!=Playwright&Category!=LiveProcess&Category!=LongRunning&Category!=Quarantined&Category!=UnixRuntimePortability&RequiresHostDocker!=true` | 14 385 | 14 385 passed, 0 failed, 0 skipped; exit 0 in 179.1 minutes (Unit 8841, Components 2303, Integration 3016, Memory 203, AgentFramework.Memory 22) |

The two Integration filters are disjoint; their counts are two lanes, not one sum. The CRM / HR
lanes above are subsets of the Stable gate and are reported separately because they are the focused
loop, not because they add to it.

### The three results that were not green at the module closure

| Result | Diagnosis | State |
|---|---|---|
| `SecretScanningTests.Repository_contains_no_realistic_provider_keys` | Environment, local only. All ten findings are copies of one integration test file under the git-ignored artifacts tree, which has no tracked file; the tracked version of that file no longer contains the pattern, and the value is a synthetic fixture of a disclosure-redaction test. Verified without printing any candidate value. | Passes on the closure tree, which has no artifacts tree. It still fails in the operator's own working tree, and that remains their decision about their retained drafts. Nothing was deleted, relocated, excluded or weakened. |
| `WorkflowsPageTests` canvas preview | A real resource defect in the test infrastructure, not in the product: every component fixture leased a database and never returned its client pool, so a class of forty-three fixtures held more connections than the server allows and the next fixture waited instead of working. Measured: the class passes at forty fixtures, passes entirely on the merge target which has forty tests in it, and this workstation's server allows a hundred connections. | Repaired at the root; the class passes forty-three of forty-three three times over, and the whole Components assembly passes in the gate. |
| `ProviderHistoryRuntimeIntegrationTests.Scale_capture_and_cleanup_remain_bounded_under_concurrent_search` | Environment-dependent wall-clock bound: a ninety-fifth percentile of twenty samples against a local PostgreSQL, asserted under 25 ms, which lands at 28 to 29 ms on this workstation. Measured on both sides: the candidate failed it 1 of 6 runs and the merge target 1 of 5, so it is not candidate-caused. | Left unchanged. Loosening a product performance oracle, or excluding first-call cost from it, is a decision for its owner; it passed in this gate run. |

### Project Structure, task assignees and governed tools

The two integration requirements of this module are covered inside the lanes above, and are named
here so the claim is checkable rather than implied:

| Requirement | Where it is proven |
|---|---|
| A person and a synchronized agent chosen as a task assignee in the real Project Structure task dialog, saved once, reloaded, replaced, cleared, and read back in Work, Gantt, Assignments and the workforce record | `ProjectStructureTaskAssigneeJourneyTests` in the Playwright lane |
| Stale project or task identity, stale revision, retirement and recreation, changed affiliation, unavailable agent, superseded selection and rejected mutations | `ProjectWriteAdmissionIntegrationTests`, `WorkAssignmentAdmissionIntegrationTests`, `ProjectPartyAssignmentIntegrationTests`, `ProjectLifetimeMigrationIntegrationTests`, `ProjectPartyAssignmentMoveReceiptIntegrationTests` in the two integration lanes |
| Tool composition, availability and allow / deny policy | `MafAgentRuntimeToolProviderCompositionTests`, `CrmPlanningToolAdmissionTests` and the `HrAgent` families in the Unit lane |
| Deterministic governed dispatch through admission, approval, canonical owners, PostgreSQL and result disclosure, with the scripted transport identified as scripted | `CrmPlanningRuntimeIntegrationTests`, `MafHrResultDisclosureIntegrationTests` and its mutation partner, `CrmHrAgentQueryIntegrationTests` in the integration lane |
| A bounded live read from the real Project Structure chat and a managed HR mutation rejected once and approved once in the real approval UI | the live lane above, with its manifests |

Ordinary planning tools and managed HR capabilities stay distinct: no test grants a planner HR
administration, and the grants the live smoke saves are the two CRM planning capabilities plus the
CRM source read, exactly as the module README describes them.

### Lanes that were not executed

- The Linux and macOS jobs of CI, the container job, the headless publish lane and the runtime
  portability browser lanes: this workstation is Windows and runs no Docker host here. They are
  unexecuted, not inferred from the Windows result.
- `Category=LiveProcess`, `Category=LongRunning`, `Category=UnixRuntimePortability` and
  `RequiresHostDocker=true`: excluded by the same filter CI uses.
- The package-mode build (`UseLocalCanDoItAllLibraries=false`): no lane exercises it here, as
  described under Dependencies.

## Risks the operator should weigh

| Risk | Evidence | Blocks the merge? |
|---|---|---|
| The CI stable job's budget | That job has `timeout-minutes: 180`. The gate took **179.1 minutes** here, and an earlier run on this workstation took 198. A runner slower than the budget fails the job without a test failing. | No, but it is the likeliest reason for a red CI run that is not a defect. Measure the first CI run rather than pre-emptively raising the number. |
| The machine-dependent performance bound above | Fails roughly one run in five or six on this workstation, on both the candidate and the merge target. | No. It is named, not hidden. |
| The browser-level field gaps | Listed in the completion record; their persistence is covered at the owners. | No, but nobody should describe the browser acceptance map as complete. |
| The package-mode fallback | No lane exercises it; a published `0.3.0` predates the sibling dialog repair. | No, unless the operator intends to ship a package-mode build. |
| Platform lanes that did not run here | Linux, macOS, containers, headless publish and the runtime portability browser lanes. | No, but they are unexecuted, and CI is where they run. |

Accepting any of these is the operator's decision, and each is named rather than inferred.

## Operator sequence

1. Publish anything the merge needs that is only local. Nothing is outstanding today: the sibling
   dialog repair `ff528974` is already the published `development` head of `CanDoItAll.Components`.
   Publishing a NuGet package that contains it is optional and only matters for a package-mode build.
2. Remove the agreed working-bundle roots, the tracked bundle directories the first command below
   names, from the working checkout and commit the removal with the usual signature. Optional in the
   same commit: the `.gitignore` negations and the `.gitattributes` entries that name paths inside
   those roots.
3. Run the post-removal checks below on the resulting commit.
4. Review and integrate `components-decoupling` into `development`.
5. Validate the actual `development` result, because it is a tree that has not existed before.
6. Review and integrate the reviewed `development` result into `main`.

## Post-removal checks

Run these on the proposed commit, not on the filesystem, so the check inspects what is actually
merged:

```powershell
git ls-files codex/bundles | Measure-Object -Line
git diff --name-only HEAD~1 HEAD | Where-Object { $_ -notlike "codex/bundles/*" }
./tools/Validation/Test-Documentation.ps1
./tools/Validation/Test-DocumentationEvidence.ps1
python ./tools/Validation/Portability/scan_portability.py --repo-root . --output $env:TEMP/portability-scan.json --tracked-only
python ./tools/Validation/Portability/enforce_portability_baseline.py --scan $env:TEMP/portability-scan.json --baseline ./tools/Validation/Portability/portability-risk-baseline.json
```

The first command must report no tracked path, and the second must print nothing but the paths the
operator intended to remove: anything else means the removal commit changed source as well, and the
gates below no longer describe the tree being merged. If source, dependencies, schema, assets or
validation logic changed beyond the removal, rerun the affected lanes instead of carrying this
record's results forward.

A merge after the removal keeps the older bundle commits in history. That is intended: the request
is that `development` and `main` stop carrying those files in their **trees**, not that history be
rewritten. Restoring the bundles later belongs to a future working branch, and restored instructions
must be refreshed against the maintained guidance before they are used again.
