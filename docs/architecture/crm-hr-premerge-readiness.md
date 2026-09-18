# CRM / HR pre-merge readiness

The current verdict for integrating `components-decoupling` into `development` and then into `main`.
It supersedes the [module-decoupling merge readiness](modules-decoupling/MERGE_READINESS.md), which
is the historical handoff of the branch that came before this one. The CRM / HR module's own
completion and coverage record is [crm-hr-ui-completion.md](crm-hr-ui-completion.md), and the durable
rules the extraction established are in [ui-component-seams.md](ui-component-seams.md).

## Verdicts

| Verdict | Value |
|---|---|
| `CRMHR_FUNCTIONAL_CLOSURE` | recorded at the closure checkpoint below |
| `DURABLE_DOCUMENTATION` | recorded at the closure checkpoint below |
| `DEPENDENCY_REPRODUCIBILITY` | recorded at the closure checkpoint below |
| `BUNDLE_FREE_SIMULATION` | recorded at the closure checkpoint below |
| `FINAL_GATE` | recorded at the closure checkpoint below |
| `ACTUAL_BUNDLE_REMOVAL` | `OPERATOR_PENDING` |
| `MERGE_TO_DEVELOPMENT` | recorded at the closure checkpoint below |
| `MERGE_TO_MAIN` | recorded at the closure checkpoint below |
| `MERGE_PERFORMED` | `NO` |
| `PUSH_PERFORMED` | `NO` |

## Candidate and targets

Refreshed at the start of this work; refresh again before acting on the sequence below, because a
target that moves invalidates the merge analysis.

| Ref | Commit at inspection |
|---|---|
| `components-decoupling` (local and `origin`) | `a8384e239e4903e8ca373d9b095a79cd3307e82e` at entry |
| `origin/development` | `d446dc2bad461c7e753cceb53a7969d6ff6b9cb2` |
| `origin/main` | `10a72521aae7cbcd5d5bc2b7c16366d496ef8285` |
| Sibling `CanDoItAll.Components` | `ff5289746573a6dd6456754a7764844627ddd49f`, the `development` head on GitHub |
| Sibling `CanDoItAll.FileTools` | checkout `7c7453c6583365ae5bd63f8fc6efc4a776e15818`, CI pin `498b36825bd5a5222429972af120b04becf4b3f6` |

## Merge analysis

`git merge-tree --write-tree` against both targets, without moving a ref, creating a merge commit or
touching the working tree:

| Merge | Exit status | Result |
|---|---|---|
| `origin/development` + candidate | 0 | merged tree equals the candidate's own tree |
| `origin/main` + candidate | 0 | merged tree equals the candidate's own tree |

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

`CRMHR-PREMERGE-CLOSURE` runs on an isolated, task-owned copy of the frozen candidate with the
bundle roots omitted, and its results are recorded here when it completes.

## Operator sequence

1. Publish anything the merge needs that is only local. Nothing is outstanding today: the sibling
   dialog repair `ff528974` is already the published `development` head of `CanDoItAll.Components`.
   Publishing a NuGet package that contains it is optional and only matters for a package-mode build.
2. Remove the agreed bundle roots (`codex/bundles/**`) from the working checkout and commit the
   removal with the usual signature. Optional in the same commit: the `.gitignore` negations and the
   `.gitattributes` entries that name paths inside those roots.
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
