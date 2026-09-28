# Source register

Reviewed repository: `fyziktom/CanDoItAll`, branch `components-decoupling`, commit `3c579fd1a923ad90f619fe144e6e4c1fe081fa8b`. SHA is provenance, not a required checkout. Git object IDs below are returned by GitHub, not recomputed over a local source clone.

The branch was re-read at review closure and its HEAD was unchanged. Detailed machine-readable scope: [sources.json](sources.json). Product execution: **not run by the reviewer**.

## R01

[src/Sandboxes/CanDoItAll.TestLab.UiSandbox/TestLabScenarioWorkspace.cs](https://github.com/fyziktom/CanDoItAll/blob/3c579fd1a923ad90f619fe144e6e4c1fe081fa8b/src/Sandboxes/CanDoItAll.TestLab.UiSandbox/TestLabScenarioWorkspace.cs)

**Read scope:** Full file (227 lines).

**Supports:** R1/R2, wait retirement, fake write versus view lifetime, scenario behavior.

**Git blob:** `b9fb6b5cd03a05825b993fe929e6f5cb95e90061`.

## R02

[src/Sandboxes/CanDoItAll.TestLab.UiSandbox/TestLabScenarioStore.cs](https://github.com/fyziktom/CanDoItAll/blob/3c579fd1a923ad90f619fe144e6e4c1fe081fa8b/src/Sandboxes/CanDoItAll.TestLab.UiSandbox/TestLabScenarioStore.cs)

**Read scope:** Full file (91 lines).

**Supports:** Known fake party, project/admission data, clone/commit/child identities.

**Git blob:** `7d1e8f0d5e60ce645d1b13abbb75ebaa4e5ae3d2`.

## R03

[src/Modules/CanDoItAll.Modules.TestLab/Workspace/TestLabWorkspaceSession.cs](https://github.com/fyziktom/CanDoItAll/blob/3c579fd1a923ad90f619fe144e6e4c1fe081fa8b/src/Modules/CanDoItAll.Modules.TestLab/Workspace/TestLabWorkspaceSession.cs)

**Read scope:** Full file (360 lines).

**Supports:** Production independent read lanes, saved-party fallback, save and receipt fencing.

**Git blob:** `bf111e9ed1e4c60b8efcb3858d0a5441836b9c5d`.

## R04

[src/UI/CanDoItAll.TestLab.UI/TestLabDraft.cs](https://github.com/fyziktom/CanDoItAll/blob/3c579fd1a923ad90f619fe144e6e4c1fe081fa8b/src/UI/CanDoItAll.TestLab.UI/TestLabDraft.cs)

**Read scope:** Full file (103 lines).

**Supports:** Pending/Finish/CanSave, admission changes, raw timestamp/validation.

**Git blob:** `015160ebad07a73560fccf6a4de3556ba4eb2b24`.

## R05

[src/UI/CanDoItAll.TestLab.UI/TestLabSubmission.cs](https://github.com/fyziktom/CanDoItAll/blob/3c579fd1a923ad90f619fe144e6e4c1fe081fa8b/src/UI/CanDoItAll.TestLab.UI/TestLabSubmission.cs)

**Read scope:** Full file (157 lines).

**Supports:** Deep snapshot, original-row identity, accepted-versus-newer field reconciliation.

**Git blob:** `bf92d67b827c4d6bd6ce81334d16f46f44aad8fe`.

## R06

[src/UI/CanDoItAll.TestLab.UI/TestLabWorkspaceSurface.razor](https://github.com/fyziktom/CanDoItAll/blob/3c579fd1a923ad90f619fe144e6e4c1fe081fa8b/src/UI/CanDoItAll.TestLab.UI/TestLabWorkspaceSurface.razor)

**Read scope:** Full file assembled from initial markup through evidence and explicit ranges 212–238 and 235–321 after response truncation.

**Supports:** Real responsible-party/project controls, form, draft identity, pending submit, unavailable fallback.

**Git blob:** `24c90a85e30d94dd81175b792f85b4ee2f66a3f7`.

## R07

[src/Modules/CanDoItAll.Modules.TestLab/Pages/TestLabPage.razor](https://github.com/fyziktom/CanDoItAll/blob/3c579fd1a923ad90f619fe144e6e4c1fe081fa8b/src/Modules/CanDoItAll.Modules.TestLab/Pages/TestLabPage.razor)

**Read scope:** Full file.

**Supports:** Route/per-page ownership, interactive readiness, Unknown notification heading C1.

**Git blob:** `ccc8a3f613e616970f1a5fba16eea1a55bb1a6b3`.

## R08

[src/Modules/CanDoItAll.Modules.TestLab/Workspace/TestLabWorkspaceOwner.cs](https://github.com/fyziktom/CanDoItAll/blob/3c579fd1a923ad90f619fe144e6e4c1fe081fa8b/src/Modules/CanDoItAll.Modules.TestLab/Workspace/TestLabWorkspaceOwner.cs)

**Read scope:** Full file (50 lines).

**Supports:** Existing owner adaptation, typed refusal/commit/unknown classification.

**Git blob:** `cb0fa6cd9f1bb6f73e842d9cc9718102941cd528`.

## R09

[src/Modules/CanDoItAll.Modules.TestLab.Contracts/TestLabContracts.cs](https://github.com/fyziktom/CanDoItAll/blob/3c579fd1a923ad90f619fe144e6e4c1fe081fa8b/src/Modules/CanDoItAll.Modules.TestLab.Contracts/TestLabContracts.cs)

**Read scope:** Full file (88 lines).

**Supports:** DTO shape/defaults, enum values, admission, JSON ignored lifetime, child identity.

**Git blob:** `5c7ecf76d970586b1f5c4e3a945aae316b99711b`.

## S01

[src/UI/CanDoItAll.Conversations.Shell/ConversationShellHost.razor.cs](https://github.com/fyziktom/CanDoItAll/blob/3c579fd1a923ad90f619fe144e6e4c1fe081fa8b/src/UI/CanDoItAll.Conversations.Shell/ConversationShellHost.razor.cs)

**Read scope:** Full file.

**Supports:** S0 source-supported correction; initialization, disposal and queued renders.

**Git blob:** `17b2f9d112fba7eafd10b962f36c4aa291ed1591`.

## S02

[tests/Components/CanDoItAll.Tests.Components/ConversationShellHostTests.cs](https://github.com/fyziktom/CanDoItAll/blob/3c579fd1a923ad90f619fe144e6e4c1fe081fa8b/tests/Components/CanDoItAll.Tests.Components/ConversationShellHostTests.cs)

**Read scope:** Lines 1–230: all test methods and beginning of helper contributor; remaining helpers not fully inspected.

**Supports:** Controlled cooperative/noncooperative disposal, queued notifications and live failures.

**Git blob:** `37c19c7a0fbadda1d5750236dd2c4b9141ab0ffe`.

## T01

[tests/Components/CanDoItAll.TestLab.UI.Tests/TestLabSurfaceTests.cs](https://github.com/fyziktom/CanDoItAll/blob/3c579fd1a923ad90f619fe144e6e4c1fe081fa8b/tests/Components/CanDoItAll.TestLab.UI.Tests/TestLabSurfaceTests.cs)

**Read scope:** Full file (190 lines).

**Supports:** Existing real-form/scenario coverage and missing R1/R2 combinations.

**Git blob:** `257da2e377ee440250966a44a8b9858e54b7e28d`.

## T02

[tests/Unit/CanDoItAll.TestLab.Tests/TestLabSessionTests.cs](https://github.com/fyziktom/CanDoItAll/blob/3c579fd1a923ad90f619fe144e6e4c1fe081fa8b/tests/Unit/CanDoItAll.TestLab.Tests/TestLabSessionTests.cs)

**Read scope:** Full file via ranges 1–270 and 270–524.

**Supports:** Production state, admission, original/successor, unknown and stale receipt tests.

**Git blob:** `d5225db09dc426c2181c0e0125431009dcc94d7f`.

## T03

[tests/Playwright/CanDoItAll.Tests.Playwright/TestLabBrowserTests.cs](https://github.com/fyziktom/CanDoItAll/blob/3c579fd1a923ad90f619fe144e6e4c1fe081fa8b/tests/Playwright/CanDoItAll.Tests.Playwright/TestLabBrowserTests.cs)

**Read scope:** Full file via ranges 1–180 and 180–283; latter request retried after transient connector failure.

**Supports:** Three TestLab browser methods, real owner paths, delayed input, sandbox/scenario/assets.

**Git blob:** `9caa6be8ce60cb6ba43c77f8489bbb49f7533fd4`.

## T04

[tests/Components/CanDoItAll.Tests.Components/TestLabReconciliationTests.cs](https://github.com/fyziktom/CanDoItAll/blob/3c579fd1a923ad90f619fe144e6e4c1fe081fa8b/tests/Components/CanDoItAll.Tests.Components/TestLabReconciliationTests.cs)

**Read scope:** Full file (88 lines).

**Supports:** Real PostgreSQL-backed host/form newer-input and duplicate-submit coverage.

**Git blob:** `67addd519af608ceadcdbb2939502b9a8157f1e3`.

## T05

[tests/Components/CanDoItAll.TestLab.UI.Tests/TestLabBoundaryTests.cs](https://github.com/fyziktom/CanDoItAll/blob/3c579fd1a923ad90f619fe144e6e4c1fe081fa8b/tests/Components/CanDoItAll.TestLab.UI.Tests/TestLabBoundaryTests.cs)

**Read scope:** Full file (72 lines).

**Supports:** Allowed transitive closure and unresolved-reference rejection.

**Git blob:** `ea8f1f26703605777efcf43d614020831a9a0833`.

## G01

[src/UI/CanDoItAll.TestLab.UI/CanDoItAll.TestLab.UI.csproj](https://github.com/fyziktom/CanDoItAll/blob/3c579fd1a923ad90f619fe144e6e4c1fe081fa8b/src/UI/CanDoItAll.TestLab.UI/CanDoItAll.TestLab.UI.csproj)

**Read scope:** Full file.

**Supports:** Direct UI-to-contracts and component package direction; not an evaluated graph.

**Git blob:** `e1b4c328b5f46ab28c6ebd065611cf12187c9a1a`.

## G02

[src/Sandboxes/CanDoItAll.TestLab.UiSandbox/CanDoItAll.TestLab.UiSandbox.csproj](https://github.com/fyziktom/CanDoItAll/blob/3c579fd1a923ad90f619fe144e6e4c1fe081fa8b/src/Sandboxes/CanDoItAll.TestLab.UiSandbox/CanDoItAll.TestLab.UiSandbox.csproj)

**Read scope:** Full file (16 lines).

**Supports:** Sandbox UI project edge and linked production CSS content, parity precondition.

**Git blob:** `50a4f1cd1164a3227631169fcd6f057aa05619c4`.

## D01

[docs/architecture/testlab-ui-boundary.md](https://github.com/fyziktom/CanDoItAll/blob/3c579fd1a923ad90f619fe144e6e4c1fe081fa8b/docs/architecture/testlab-ui-boundary.md)

**Read scope:** Initial response exposes implementation record/counts/measurements; final lines 190–248 explicitly reread after response truncation.

**Supports:** Author-reported 115-case proof, graph/watch/timing and static gate receipts; not reviewer execution.

**Git blob:** `95b5dfb45fbe2565fd9deb94456dc88a726eba9e`.

## D02

[docs/architecture/ui-component-seams.md](https://github.com/fyziktom/CanDoItAll/blob/3c579fd1a923ad90f619fe144e6e4c1fe081fa8b/docs/architecture/ui-component-seams.md)

**Read scope:** Full file.

**Supports:** Canonical UI/host/read-lane/outcome/scenario/measurement rules.

**Git blob:** `da998e19c2206ef4396e04b20aeed6ac4634bbaa`.

## D03

[AGENTS.md](https://github.com/fyziktom/CanDoItAll/blob/3c579fd1a923ad90f619fe144e6e4c1fe081fa8b/AGENTS.md)

**Read scope:** Full file.

**Supports:** Current authority and mandatory portability closure.

**Git blob:** `4d7ab165a035e032dcf327cf3e972316fb5b3d64`.

## D04

[.github/copilot-instructions.md](https://github.com/fyziktom/CanDoItAll/blob/3c579fd1a923ad90f619fe144e6e4c1fe081fa8b/.github/copilot-instructions.md)

**Read scope:** Full file.

**Supports:** Smallest correct change, English/code style, MCP and test conventions.

**Git blob:** `dde5c2fdbcb56b7b5f48dfa17ed74fda238c2e1a`.

## D05

[docs/testing.md](https://github.com/fyziktom/CanDoItAll/blob/3c579fd1a923ad90f619fe144e6e4c1fe081fa8b/docs/testing.md)

**Read scope:** Lines 1–160. Later detailed gate procedures must be read at execution.

**Supports:** Isolation, affected test discovery, current TestLab lanes and fixture configuration.

**Git blob:** `9be204c0687215480d7cde37dc093f9abeb5c0aa`.

## D06

[.github/workflows/ci.yml](https://github.com/fyziktom/CanDoItAll/blob/3c579fd1a923ad90f619fe144e6e4c1fe081fa8b/.github/workflows/ci.yml)

**Read scope:** Lines 1–170; diff inventory also records TestLab CI edits. Later shard commands were not fully reread.

**Supports:** Push/PR triggers, source dependency resolution and PostgreSQL lane setup; not proof of a CI run.

**Git blob:** `b78190814ef209eea9fa0cb5a02312abc3917c31`.

## N01

[src/Modules/CanDoItAll.Modules.SchedulerPlanner/Pages](https://github.com/fyziktom/CanDoItAll/tree/3c579fd1a923ad90f619fe144e6e4c1fe081fa8b/src/Modules/CanDoItAll.Modules.SchedulerPlanner/Pages)

**Read scope:** Complete two-file tree inventory only; Razor blob size 114778 bytes; no full semantic page review.

**Supports:** Limited next-module inventory; no next-module implementation assignment.

**Git tree:** `ad6b666f508d3969b0e1dc6ddbe3e47902626a30`.

## N02

[src/Modules/CanDoItAll.Modules.Plugins](https://github.com/fyziktom/CanDoItAll/tree/3c579fd1a923ad90f619fe144e6e4c1fe081fa8b/src/Modules/CanDoItAll.Modules.Plugins)

**Read scope:** Directory inventory only; no complete page/catalog/implementation read.

**Supports:** Limited next-module inventory; no next-module implementation assignment.

## N03

[src/Modules/CanDoItAll.Modules.Resources](https://github.com/fyziktom/CanDoItAll/tree/3c579fd1a923ad90f619fe144e6e4c1fe081fa8b/src/Modules/CanDoItAll.Modules.Resources)

**Read scope:** Directory inventory only; no complete rendered closure or owner review.

**Supports:** Limited next-module inventory; no next-module implementation assignment.

## H01

[repository-branch-snapshot](https://api.github.com/repos/fyziktom/CanDoItAll/branches/components-decoupling)

**Read scope:** Branch response: HEAD, parent, commit message/date and GitHub verification flag. Re-read at review closure: same HEAD.

**Supports:** Review provenance; mutable branch URL is not an execution pin.

## H02

[repository-comparison](https://github.com/fyziktom/CanDoItAll/compare/97989b9d13b9a6fa16280da98ec5b005a2f968c7...3c579fd1a923ad90f619fe144e6e4c1fe081fa8b)

**Read scope:** Connector compare metadata and changed-file inventory; 3 ahead, 0 behind.

**Supports:** Changes since preceding review and additive source/test extraction inventory.

## W01

[official-documentation](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/synchronization-context?view=aspnetcore-10.0)

**Read scope:** Microsoft Learn search excerpt about incomplete awaits, reentrancy and disposed components.

**Supports:** Why actual interaction/disposal can occur across pending work; supplementary, not application evidence.

## W02

[official-documentation](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtokensource.cancelasync?view=net-10.0)

**Read scope:** Microsoft Learn search excerpt covering cancellation transition and callback completion.

**Supports:** Supplementary cancellation lifecycle semantics; not a new confirmed product defect.

## H03

[repository-actions-snapshot](https://api.github.com/repos/fyziktom/CanDoItAll/actions/runs?head_sha=3c579fd1a923ad90f619fe144e6e4c1fe081fa8b&per_page=10)

**Read scope:** Complete response: total_count=0; workflow_runs=[]. No event-type filter was applied.

**Supports:** No GitHub Actions run was returned for this SHA at review time; does not disprove locally executed tests.
