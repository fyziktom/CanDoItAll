# Source index

Reviewed repository `fyziktom/CanDoItAll`, commit `97989b9d13b9a6fa16280da98ec5b005a2f968c7`, 2026-09-28.

Links identify evidence, not execution pins. Read the current checkout before implementation. Source search on the default branch was used only for discovery; substantive reads were pinned to the reviewed branch HEAD. No product tests were executed during this review.

Read scopes below deliberately distinguish full files, excerpts, directory/commit metadata and reported validation. GitHub-provided blob IDs can support drift checking; they are not a claim that the reviewer cloned or rebuilt the source.

<a id="c01"></a>
## C01 — docs/architecture/collaboration-ui-boundary.md

[Open primary source](https://github.com/fyziktom/CanDoItAll/blob/97989b9d13b9a6fa16280da98ec5b005a2f968c7/docs/architecture/collaboration-ui-boundary.md)

**Read scope:** Lines 295 to end: corrective implementation record and residual validation limitations.

**Supports:** Reported 82-case proof; historical provenance and known shared shell failure.

Git blob: `1790c85d2143dd1f5faf9964494950733980af38`.

<a id="c02"></a>
## C02 — src/Modules/CanDoItAll.Modules.Collaboration/Pages/CollaborationWorkspaceSession.cs

[Open primary source](https://github.com/fyziktom/CanDoItAll/blob/97989b9d13b9a6fa16280da98ec5b005a2f968c7/src/Modules/CanDoItAll.Modules.Collaboration/Pages/CollaborationWorkspaceSession.cs)

**Read scope:** Full file.

**Supports:** Corrected realignment and production read/write/editor lifetime.

Git blob: `85e779b72367947ff261c6b9b986cb1ef85cce8e`.

<a id="c03"></a>
## C03 — src/UI/CanDoItAll.Collaboration.UI/CollaborationDraft.cs

[Open primary source](https://github.com/fyziktom/CanDoItAll/blob/97989b9d13b9a6fa16280da98ec5b005a2f968c7/src/UI/CanDoItAll.Collaboration.UI/CollaborationDraft.cs)

**Read scope:** Full file.

**Supports:** Shared draft-retention predicate and form admission.

Git blob: `eaf65e8fe0fe1863fcac60d22fb75ac839245aea`.

<a id="c04"></a>
## C04 — src/UI/CanDoItAll.Collaboration.UI/CollaborationWorkspaceSurface.razor.cs

[Open primary source](https://github.com/fyziktom/CanDoItAll/blob/97989b9d13b9a6fa16280da98ec5b005a2f968c7/src/UI/CanDoItAll.Collaboration.UI/CollaborationWorkspaceSurface.razor.cs)

**Read scope:** Full file.

**Supports:** Origin-bound input handling before blur.

Git blob: `b4a35ba9d793a73381f6b2ef785d687fb7eec053`.

<a id="c05"></a>
## C05 — src/UI/CanDoItAll.Collaboration.UI/CollaborationWorkspaceSurface.razor

[Open primary source](https://github.com/fyziktom/CanDoItAll/blob/97989b9d13b9a6fa16280da98ec5b005a2f968c7/src/UI/CanDoItAll.Collaboration.UI/CollaborationWorkspaceSurface.razor)

**Read scope:** Lines 1–28 and 265–365.

**Supports:** Accepted selected-thread marker, actual reply form and oninput binding.

Git blob: `1c1caccd177112d6a6bc6d7f0c5976e44af12855`.

<a id="c06"></a>
## C06 — src/Sandboxes/CanDoItAll.Collaboration.UiSandbox/CollaborationScenarioWorkspace.cs

[Open primary source](https://github.com/fyziktom/CanDoItAll/blob/97989b9d13b9a6fa16280da98ec5b005a2f968c7/src/Sandboxes/CanDoItAll.Collaboration.UiSandbox/CollaborationScenarioWorkspace.cs)

**Read scope:** Full file.

**Supports:** Admitted fake writes, selection generation and scenario disposal.

Git blob: `be4054d366f2386905e42b040a018620cb6d0b97`.

<a id="c07"></a>
## C07 — tests/Unit/CanDoItAll.Collaboration.Tests/CollaborationWorkspaceSessionTests.cs

[Open primary source](https://github.com/fyziktom/CanDoItAll/blob/97989b9d13b9a6fa16280da98ec5b005a2f968c7/tests/Unit/CanDoItAll.Collaboration.Tests/CollaborationWorkspaceSessionTests.cs)

**Read scope:** Lines 1–230; not a full class execution or discovery.

**Supports:** Dirty/successor reply and delayed-read regression bodies.

Git blob: `d030affefd7790453ae8a8e2bd2c71622c8f0e2e`.

<a id="c08"></a>
## C08 — tests/Components/CanDoItAll.Tests.Components/CollaborationReconciliationTests.cs

[Open primary source](https://github.com/fyziktom/CanDoItAll/blob/97989b9d13b9a6fa16280da98ec5b005a2f968c7/tests/Components/CanDoItAll.Tests.Components/CollaborationReconciliationTests.cs)

**Read scope:** Full file.

**Supports:** Real-form before-blur retention and delayed mark-read acceptance.

Git blob: `e10192efe62aa3e76800664b75da3089dbc19471`.

<a id="c09"></a>
## C09 — tests/Components/CanDoItAll.Collaboration.UI.Tests/CollaborationScenarioTests.cs

[Open primary source](https://github.com/fyziktom/CanDoItAll/blob/97989b9d13b9a6fa16280da98ec5b005a2f968c7/tests/Components/CanDoItAll.Collaboration.UI.Tests/CollaborationScenarioTests.cs)

**Read scope:** Full file.

**Supports:** Delayed fake persistence, no-op/effective intent and disposal regressions.

Git blob: `8f2576bf4abe5af0058da14397b4ae08bdee2358`.

<a id="c10"></a>
## C10 — tests/Playwright/CanDoItAll.Tests.Playwright/CollaborationBrowserTests.cs

[Open primary source](https://github.com/fyziktom/CanDoItAll/blob/97989b9d13b9a6fa16280da98ec5b005a2f968c7/tests/Playwright/CanDoItAll.Tests.Playwright/CollaborationBrowserTests.cs)

**Read scope:** Full file.

**Supports:** Accepted target/read state, durable notification and escalation creation, browser log checks.

Git blob: `e976a8a76fa5a8267a21875b798d5b87e536e873`.

<a id="s01"></a>
## S01 — src/UI/CanDoItAll.Conversations.Shell/ConversationShellHost.razor.cs

[Open primary source](https://github.com/fyziktom/CanDoItAll/blob/97989b9d13b9a6fa16280da98ec5b005a2f968c7/src/UI/CanDoItAll.Conversations.Shell/ConversationShellHost.razor.cs)

**Read scope:** Full file.

**Supports:** Initialization continuation accesses source token after disposal; queued callback lifetime.

Git blob: `102ecaaa15c23d6ed2f6a06dec51e402dd75f1c5`.

<a id="s02"></a>
## S02 — tests/Components/CanDoItAll.Tests.Components/ConversationShellHostTests.cs

[Open primary source](https://github.com/fyziktom/CanDoItAll/blob/97989b9d13b9a6fa16280da98ec5b005a2f968c7/tests/Components/CanDoItAll.Tests.Components/ConversationShellHostTests.cs)

**Read scope:** Full file returned by 1–280 request.

**Supports:** Existing shell behavior tests use immediately completing contributors.

Git blob: `b9f436156a3a6c5f3998ab6a0322d94b19196a6b`.

<a id="t01"></a>
## T01 — src/Modules/CanDoItAll.Modules.TestLab/CanDoItAll.Modules.TestLab.csproj

[Open primary source](https://github.com/fyziktom/CanDoItAll/blob/97989b9d13b9a6fa16280da98ec5b005a2f968c7/src/Modules/CanDoItAll.Modules.TestLab/CanDoItAll.Modules.TestLab.csproj)

**Read scope:** Full file.

**Supports:** Current Razor module depends on Infrastructure and Projects implementation.

Git blob: `53a9755764649a74e16e401bf63beba72ebfc0bf`.

<a id="t02"></a>
## T02 — src/Modules/CanDoItAll.Modules.TestLab/Pages/TestLabPage.razor

[Open primary source](https://github.com/fyziktom/CanDoItAll/blob/97989b9d13b9a6fa16280da98ec5b005a2f968c7/src/Modules/CanDoItAll.Modules.TestLab/Pages/TestLabPage.razor)

**Read scope:** Markup and initial code from full fetch, plus 350 to end for the complete save/helper tail.

**Supports:** Four-section workspace, mutable async host, admission and committed-ID retention.

Git blob: `06b88f672395b90ce0634be3739bdf55aa1f5041`.

<a id="t03"></a>
## T03 — src/Modules/CanDoItAll.Modules.TestLab/TestLabModels.cs

[Open primary source](https://github.com/fyziktom/CanDoItAll/blob/97989b9d13b9a6fa16280da98ec5b005a2f968c7/src/Modules/CanDoItAll.Modules.TestLab/TestLabModels.cs)

**Read scope:** Full file through overlapping ranges 1–240 and 240 to end.

**Supports:** Mixed DTO/EF/service ownership; transaction and postcommit semantics.

Git blob: `879e21b382539e69aa3508d0c8aaf1e0c4e83c0e`.

<a id="t04"></a>
## T04 — src/Modules/CanDoItAll.Modules.TestLab/README.md

[Open primary source](https://github.com/fyziktom/CanDoItAll/blob/97989b9d13b9a6fa16280da98ec5b005a2f968c7/src/Modules/CanDoItAll.Modules.TestLab/README.md)

**Read scope:** Full file.

**Supports:** Owner model, historical references, projections, metadata rather than runner.

Git blob: `414d88efa6e29fdcadb2e6e585dfa84ccc2d2c55`.

<a id="t05"></a>
## T05 — src/Modules/CanDoItAll.Modules.TestLab/TestLabModuleServiceCollectionExtensions.cs

[Open primary source](https://github.com/fyziktom/CanDoItAll/blob/97989b9d13b9a6fa16280da98ec5b005a2f968c7/src/Modules/CanDoItAll.Modules.TestLab/TestLabModuleServiceCollectionExtensions.cs)

**Read scope:** Full file.

**Supports:** Scoped service, pooled owner factory and module marker.

Git blob: `7021acb83bfb060c439e7f5ee7e24eec09f7b4d3`.

<a id="t06"></a>
## T06 — src/Modules/CanDoItAll.Modules.Projects.Contracts/CanDoItAll.Modules.Projects.Contracts.csproj

[Open primary source](https://github.com/fyziktom/CanDoItAll/blob/97989b9d13b9a6fa16280da98ec5b005a2f968c7/src/Modules/CanDoItAll.Modules.Projects.Contracts/CanDoItAll.Modules.Projects.Contracts.csproj)

**Read scope:** Full file.

**Supports:** Existing lightweight Projects contract project with SharedKernel reference.

Git blob: `7c3ec809819a4787feacfd9216de55b6c1ff0045`.

<a id="t07"></a>
## T07 — src/Modules/CanDoItAll.Modules.Projects.Contracts/ProjectWriteAdmission.cs

[Open primary source](https://github.com/fyziktom/CanDoItAll/blob/97989b9d13b9a6fa16280da98ec5b005a2f968c7/src/Modules/CanDoItAll.Modules.Projects.Contracts/ProjectWriteAdmission.cs)

**Read scope:** Full file.

**Supports:** Immutable owner-issued profile/project/lifetime evidence and typed refusal.

Git blob: `a76f5e21197064c42356a54c880fe31747920705`.

<a id="t08"></a>
## T08 — src/Modules/CanDoItAll.Modules.Projects/ProjectWriteSelectionQuery.cs

[Open primary source](https://github.com/fyziktom/CanDoItAll/blob/97989b9d13b9a6fa16280da98ec5b005a2f968c7/src/Modules/CanDoItAll.Modules.Projects/ProjectWriteSelectionQuery.cs)

**Read scope:** Full file.

**Supports:** Selection record currently in implementation; query issues exact admissions.

Git blob: `80c52ed564ff08db828ecbd750c74cd4f015d7ba`.

<a id="t09"></a>
## T09 — src/Modules/CanDoItAll.Modules.Projects/ProjectPartyIntegrationContracts.cs

[Open primary source](https://github.com/fyziktom/CanDoItAll/blob/97989b9d13b9a6fa16280da98ec5b005a2f968c7/src/Modules/CanDoItAll.Modules.Projects/ProjectPartyIntegrationContracts.cs)

**Read scope:** Lines 1–280; option record and relevant interface methods, not the entire larger contract.

**Supports:** Rich party option shape and narrow list/get lookup needs.

Git blob: `9b43d1a99398dfaa12aa09d8ef89a5c8b6c536da`.

<a id="t10"></a>
## T10 — tests/Components/CanDoItAll.Tests.Components/OwnerPostcommitPageTests.cs

[Open primary source](https://github.com/fyziktom/CanDoItAll/blob/97989b9d13b9a6fa16280da98ec5b005a2f968c7/tests/Components/CanDoItAll.Tests.Components/OwnerPostcommitPageTests.cs)

**Read scope:** Full-fetch visible TestPlan regression bodies, plus lines 350 to end for helpers. Some intervening Resource-only text was truncated.

**Supports:** Real host/persistence postcommit, child identity and project selection semantics.

Git blob: `5850d6d32a37fb3181401d870b16a967e75cfa74`.

<a id="t11"></a>
## T11 — tests/Integration/CanDoItAll.Tests.Integration/TestLabOwnerPersistenceTests.cs

[Open primary source](https://github.com/fyziktom/CanDoItAll/blob/97989b9d13b9a6fa16280da98ec5b005a2f968c7/tests/Integration/CanDoItAll.Tests.Integration/TestLabOwnerPersistenceTests.cs)

**Read scope:** Lines 1–230; the trailing class content was not reviewed in this pass.

**Supports:** Exact model parity, historical restart/IDs and profile isolation baseline.

Git blob: `321e565c301436b92b2352613fe1bb52eca16adf`.

<a id="t12"></a>
## T12 — src/App/CanDoItAll.Composition/ModuleAssemblies.cs

[Open primary source](https://github.com/fyziktom/CanDoItAll/blob/97989b9d13b9a6fa16280da98ec5b005a2f968c7/src/App/CanDoItAll.Composition/ModuleAssemblies.cs)

**Read scope:** Full file.

**Supports:** Production assembly catalog uses TestLabModuleAssemblyMarker.

Git blob: `4865afb47f436ae410ae5cee93782ea1f30e013a`.

<a id="g01"></a>
## G01 — AGENTS.md

[Open primary source](https://github.com/fyziktom/CanDoItAll/blob/97989b9d13b9a6fa16280da98ec5b005a2f968c7/AGENTS.md)

**Read scope:** Full file.

**Supports:** Current authoritative instruction routing and mandatory closure.

Git blob: `4d7ab165a035e032dcf327cf3e972316fb5b3d64`.

<a id="g02"></a>
## G02 — .github/copilot-instructions.md

[Open primary source](https://github.com/fyziktom/CanDoItAll/blob/97989b9d13b9a6fa16280da98ec5b005a2f968c7/.github/copilot-instructions.md)

**Read scope:** Full file.

**Supports:** Engineering/component/MCP rules and bounded validation.

Git blob: `dde5c2fdbcb56b7b5f48dfa17ed74fda238c2e1a`.

<a id="g03"></a>
## G03 — docs/architecture/ui-component-seams.md

[Open primary source](https://github.com/fyziktom/CanDoItAll/blob/97989b9d13b9a6fa16280da98ec5b005a2f968c7/docs/architecture/ui-component-seams.md)

**Read scope:** Full file.

**Supports:** Canonical placement, independent lifetimes and post-submit reconciliation.

Git blob: `da998e19c2206ef4396e04b20aeed6ac4634bbaa`.

<a id="g04"></a>
## G04 — docs/testing.md

[Open primary source](https://github.com/fyziktom/CanDoItAll/blob/97989b9d13b9a6fa16280da98ec5b005a2f968c7/docs/testing.md)

**Read scope:** Lines 1–220; current prerequisites, test entry points, bUnit conventions and Collaboration lanes.

**Supports:** Executor must read the full current guide including static and broad-gate triggers.

Git blob: `a78454ebb67cdfdbf5efaf2428d902f133b66407`.

<a id="g05"></a>
## G05 — .github/workflows/ci.yml

[Open primary source](https://github.com/fyziktom/CanDoItAll/blob/97989b9d13b9a6fa16280da98ec5b005a2f968c7/.github/workflows/ci.yml)

**Read scope:** Lines 1–235: triggers, dependency source mode, platform split, database and build/test setup.

**Supports:** Executor must inspect remaining actual shard/test inclusion steps before adding projects.

Git blob: `3db271f26908b570c3a3837da76eb7cfe6142262`.

<a id="d01"></a>
## D01 — repository-directory

[Open primary source](https://api.github.com/repos/fyziktom/CanDoItAll/contents/src/Sandboxes?ref=97989b9d13b9a6fa16280da98ec5b005a2f968c7)

**Read scope:** Directory listing only.

**Supports:** Four current named sandboxes; TestLab not present in this directory.

<a id="p01"></a>
## P01 — commit-and-branch-metadata

[Open primary source](https://github.com/fyziktom/CanDoItAll/commit/97989b9d13b9a6fa16280da98ec5b005a2f968c7)

**Read scope:** Branch HEAD metadata and merge parents/signature returned by GitHub.

**Supports:** Reviewed actual components-decoupling merge HEAD; valid GitHub signature verification.

<a id="p02"></a>
## P02 — commit-metadata-and-diff

[Open primary source](https://github.com/fyziktom/CanDoItAll/commit/f50c958c3ac9df2ec9df43a76963a8753178735f)

**Read scope:** Commit metadata and visible patch; compact compare file list for 67413ee..f50c958; not a claim to have inspected every full patch.

**Supports:** Correction commit provenance, valid signature and 17 changed-file inventory.

<a id="p03"></a>
## P03 — workflow-run-query

[Open primary source](https://api.github.com/repos/fyziktom/CanDoItAll/actions/runs?head_sha=97989b9d13b9a6fa16280da98ec5b005a2f968c7&per_page=100)

**Read scope:** Query response: total_count=0, workflow_runs=[].

**Supports:** No Actions run returned for this SHA; this is not local test evidence.

<a id="e01"></a>
## E01 — external-primary-documentation

[Open primary source](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/synchronization-context?view=aspnetcore-10.0)

**Read scope:** Reentrancy/disposal guidance and asynchronous callbacks.

**Supports:** An incomplete await permits lifecycle changes before continuation.

<a id="e02"></a>
## E02 — external-primary-documentation

[Open primary source](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/component-disposal?view=aspnetcore-10.0)

**Read scope:** Component disposal guidance.

**Supports:** Disposal and async resource ownership context.

<a id="e03"></a>
## E03 — external-primary-documentation

[Open primary source](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtokensource.token)

**Read scope:** Token property documented exceptions.

**Supports:** Reading Token on a disposed CancellationTokenSource can throw ObjectDisposedException.

<a id="e04"></a>
## E04 — external-primary-documentation

[Open primary source](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-watch)

**Read scope:** Watched files, project-reference graph and list command.

**Supports:** An RCL move alone does not shrink the full Web watch graph.
