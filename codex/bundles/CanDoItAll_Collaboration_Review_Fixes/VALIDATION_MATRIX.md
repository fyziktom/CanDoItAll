# Follow-up validation matrix

This document defines what the executor must prove. No product test in this matrix was executed during handoff preparation. Test counts in the old boundary document are historical claims, not acceptance quotas or automatically valid results for new code.

## Reproduce before repairing

| ID | Deterministic setup | Required result after repair |
|---|---|---|
| R1.1 | Inbox, Unread only, A selected. Append #1 succeeds; hold reconciliation. Enter an unsent successor reply through the real field-change path, then return a snapshot with no unread candidates. | The unsent text/context stays accessible and attached to A under the documented policy; no automatic discard, replay or rebinding. Clean-filter-empty behavior remains valid when there is no outstanding editor content. |
| R1.2 | Same sequence, but B remains unread. | Background realignment cannot silently discard A's successor draft or make it B's message. |
| R1.3 | Dirty A reply exists before Mark-read or a manual refresh; fresh data removes A from the active filter. | The chosen safe automatic-transition policy preserves/recoverably retains that draft. Merely disabling newly typed edits during one save is not enough. |
| R1.4 | Explicitly select B, then return A, including an older in-flight reply. | Preserve the documented deliberate navigation behavior; retired callbacks do not mutate successor editors, selection, notifications or gates. |
| R1.5 | Same-target refresh, section change with target still visible, clean empty filter, refusal and unknown outcome. | Existing context, validation, empty-selection and recovery invariants remain intact. |
| R2a.1 | Sandbox AdmittedSave: submit on A, switch B, release reply, reopen A. | Stored A has exactly one new message; B's active draft/selection is unchanged. |
| R2a.2 | A -> B -> A with a newer draft and separate pending action. | Old completion applies only its admitted stored result and cannot clear/unlock/notify for the new editor. |
| R2a.3 | Retire/dispose a scenario while actions are pending. | No callback affects the replacement scenario; owned waits unwind under an explicit bounded disposal policy. |
| R2b.1 | Delayed create, then Inbox -> Threads with A still visible, then completion. | Created identity/data retained; newer section and current target preserved. |
| R2b.2 | Delayed create then effective same-target filter change; repeated no-op setter. | Genuine newer user intent retires obsolete navigation; no-op behavior remains consistent. |
| R3.1 | Hold the owner Mark-read operation incomplete after admission. | The old disabled-control assertion is shown to be insufficient; the chosen accepted-state observation cannot report completion during admission. |
| R3.2 | Real production browser create, mark-read and owner read-back. | Assertions wait for the actual selected target's completed outcome; no arbitrary sleeps or pre-existing Ready marker shortcut. |
| C1 | Drive notification and escalation creation through real production forms, visit Escalations and durable links, and exercise dirty-target behavior. | Actual stored/selected identities and semantic outcomes proved; not replaced by scenario seed data. |

These rows are obligations, not one-test-per-row or test-class quotas. Use controlled completions (`RunContinuationsAsynchronously`) and explicit event-task ownership. Keep tests on the appropriate renderer dispatcher; never block owner reads inside `WaitForAssertion`.

## Existing focused lanes to inspect and rerun

The following project/filter combinations are recorded in the reviewed repository. Confirm they are still the correct current entry points and include new tests. An affected test may not contain Collaboration in its name; use source/consumer analysis and current repository rules to expand the set.

| Lane | Project | Baseline filter | Historical cases reported |
|---|---|---|---:|
| Session/state | `tests/Unit/CanDoItAll.Collaboration.Tests/CanDoItAll.Collaboration.Tests.csproj` | `FullyQualifiedName~CollaborationWorkspaceSessionTests` | 29 |
| Real renderer/sandbox/boundary | `tests/Components/CanDoItAll.Collaboration.UI.Tests/CanDoItAll.Collaboration.UI.Tests.csproj` | `FullyQualifiedName~Collaboration` | 13 |
| Bounded context | `tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj` | `FullyQualifiedName~CollaborationDbContextTests` | 5 |
| Production host and shell | `tests/Components/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj` | `FullyQualifiedName~CollaborationHostTests\|FullyQualifiedName~MainLayoutCollaborationTests` | 3 |
| Real owner/schema/durable compatibility | `tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj` | `FullyQualifiedName~CollaborationIntegrationTests` | 7 |
| Real browser hosts | `tests/Playwright/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj` | `FullyQualifiedName~CollaborationBrowserTests\|FullyQualifiedName~CollaborationSandboxBrowserTests` | 2 |

The historical total of 59 is not a new execution result. Additions and parameterized data change discovery counts. Record expected/discovered/passed/failed/skipped separately for the actual final checkout.

Example command pattern, after choosing real current values for `$project`, `$filter` and a supported `$configuration`:

```powershell
dotnet test $project --configuration $configuration --list-tests --filter $filter /m:1
# Check the exit code, test assembly freshness, and expected/discovered case count.
dotnet test $project --configuration $configuration --no-build --no-restore --filter $filter --logger "trx;LogFileName=collaboration-review.trx" --results-directory artifacts/collaboration-review /m:1
```

Use distinct result files/directories per lane so runs do not overwrite each other. Do not blindly continue after failed build/discovery or count mismatch. Current `docs/testing.md` and CI take precedence over these baseline examples. Complete impacted layers without repeatedly invoking the whole suite after every edit.

## Build, graph and assets

Build the changed module, feature UI, sandbox and production host/necessary consumers. Retain the ordinary contracts library and current owner registration. Rerun the negative/positive boundary tests. If project references, assets or source-mode configuration change, inspect evaluated dependency metadata and `dotnet watch --list`; do not confuse a production CSS content link with a Web compile dependency.

No new performance number is required merely to repair a state transition. Reuse older measurements only as attributed historical records, never as current evidence or a comparative full-Web speedup. If changing the development-loop boundary, obtain new relevant measurements with exact configuration and report missing baselines honestly.

## Browser and persistence safety

Run sandbox and production browser proof at the current supported desktop viewport; the previous focused proof used 1600 x 1000. Use actual styles/fonts/scripts and actual BaseLib descendants. Inspect console, page, asset and server errors and review screenshots for the changed state presentation. Add the missing production escalation path. A browser fixture seeded with an escalation is not a successful UI create-path test.

Use the current explicitly isolated PostgreSQL 18 test setup through `CANDOITALL_TESTS_POSTGRES_CONNECTION`. Record sanitized endpoint/version/configuration. Leave the user's app on 5032, its databases and unrelated processes untouched. Owner writes, no-op Mark-read, post-commit observer isolation and profile/schema compatibility remain real integration proof, not fake-owner tests. Controlled-delay injection belongs in test seams or the sandbox, not production routes/query switches.

## Static gates and receipt

Read the current portability-static procedure. Scan the entire protected tree including new files, repair genuine defects, review added/stale findings, refresh only intentional baselines and require final enforcement without `--write-baseline`. Run maintained documentation checks and `git diff --check`. Expand validation under the current named broader-suite triggers when actual changes require it.

Append an accurate correction receipt to the maintained boundary record: actual start/final HEAD, sibling revisions, source/asset mode, findings, red/green commands, discovery and execution counts, result locations, failed/blocked lanes, graph changes or explicitly unchanged boundary, signing status and cleanup. Map previously missing original-matrix obligations to concrete evidence. Do not claim that a hash/link/archive check validates the application, or that a historical test summary proves the corrected code.
