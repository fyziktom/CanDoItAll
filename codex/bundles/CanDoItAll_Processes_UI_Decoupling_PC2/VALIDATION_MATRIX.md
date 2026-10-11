# Impact-selected validation, with a non-negotiable regression floor

Read current `docs/testing.md`, `.github/workflows/ci.yml` and the root instructions. Refresh the configured CodeAnalytics index for the actual source/sibling state and record its revision. Query changed symbols, references, owning tests and public consumers. Re-query after new files/contracts or significant changes. Do not use search results indexed on another branch as proof of current behavior.

The selection is the union of MCP results, actual source consumers, Razor/DI/reflection/deferred/asset edges, new regressions, and this floor. A textual candidate scanner or an old TRX does not replace it. If the named MCP is unavailable, record the actual discovery/call failure and use current source plus evaluated graphs/assembly and direct test inspection; do not pretend a fallback was an MCP run.

## Loop and discovery

Build each changed production project before its tests. Use an isolated configuration and explicitly task-owned PostgreSQL 18 endpoint for native/database tests. Never start/reuse the ordinary development database, application port 5032 or retained manual-provider data. Inspect current test bootstrap, sibling prerequisites and private environment configuration without logging credentials.

For every new/changed filter, compute expected discovery from the current test source including partial files, InlineData/MemberData and theory expansion. Run `--list-tests`; zero tests or unexplained count mismatch invalidates proof. Report listed tests and expanded executed cases separately when the runner discovers one theory but enumerates cases at execution. Refresh the assembly before using `--no-build --no-restore`.

Example shape, not a claim this command was executed by the reviewer:

```powershell
$configuration = 'ProcUiProofPC2'
$project = './src/Modules/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj'
$tests = './tests/Components/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj'
$filter = 'FullyQualifiedName~ProcessWorkspaceShellTests'

dotnet build $project --configuration $configuration /m:1
if ($LASTEXITCODE -ne 0) { throw 'Production build failed.' }
dotnet test $tests --configuration $configuration --list-tests --filter $filter /m:1
if ($LASTEXITCODE -ne 0) { throw 'Test discovery failed.' }
# Confirm the source-derived expected discovery and expanded case count before continuing.
dotnet test $tests --configuration $configuration --no-build --no-restore --filter $filter /m:1
if ($LASTEXITCODE -ne 0) { throw 'Focused tests failed.' }
```

The example assumes the selected test assembly was built by the discovery invocation; verify actual runner behavior and build it explicitly if needed. Avoid concurrent writes to the same bin/obj/static-asset outputs. Use the same `CANDOITALL_TEST_CONFIGURATION` for browser child hosts as their prepared output configuration.

## Existing seed suites to reconcile with current source

These are concrete existing seeds, not an exhaustive list or a hardcoded pass count. Include new tests introduced by PC2 and all impacted consumers found by MCP/source inspection.

| Owner/project | Selection seed | Required proof |
| --- | --- | --- |
| `tests/Components/CanDoItAll.Processes.UI.Tests` | `FullyQualifiedName~CanDoItAll.Tests.Components.ProcessesUI` | Real renderers, all tabs, raw drafts, role selection/step metadata, stable row reconciliation, two canvases, real Markdown, public/transitive closure guard |
| `tests/Components/CanDoItAll.Tests.Components` | `FullyQualifiedName~ProcessWorkspaceShellTests` | Native host composition, all partial files including live dashboard tests, controlled read/write races, raw drafts, launch/context/manager integration |
| Same native component project | `FullyQualifiedName~ProcessRunFilesDialogTests|FullyQualifiedName~ProcessRunCancellationActionTests` | Late file success/error/action/cleanup; accepted cancel and post-result refresh; close is not cancel |
| Same native component project | `FullyQualifiedName~ChatWorkspacePanelTests|FullyQualifiedName~AgentExecutionActivityStatusTests|FullyQualifiedName~ProjectStructurePageProcessLaunchScopeTests` | Default and typed-child chat, streaming activity, selected scope and actual Workbench launch lifetime; required when changes affect these shared/native seams |
| `tests/Unit/CanDoItAll.Tests.Unit` | `FullyQualifiedName~ProcessDefinitionCatalogProjectionTests|FullyQualifiedName~ProcessProjectionPipelineTests` | Native definition/role/step/template owner results, expected versions, exact returned selections; do not present same-instance results as durable-client proof |
| Same unit project | `FullyQualifiedName~ProcessLaunchAtomicCommitTests|FullyQualifiedName~ProcessLaunchAgentOperationTests|FullyQualifiedName~ProcessLaunchProducerRequestTests` | Accepted launch identity, no replay, preparation/current scope, existing caller-intent contracts |
| Same unit project | `FullyQualifiedName~ProcessRuntimeOperatorApplicationServiceTests|FullyQualifiedName~ProcessRunFileScopeProviderTests|FullyQualifiedName~ProcessRunFilesCoordinatorTests` | Native operations, file authority and ownership cleanup |
| Same unit project | `FullyQualifiedName~ProjectStructureProcessLaunchSourceSnapshotMapperTests|FullyQualifiedName~ProjectStructureProcessLaunchContextBuilderTests` | Retain Workflow versus Agent executor mapping and source-node/lifetime capture |
| `tests/Integration/CanDoItAll.Tests.Integration` | `FullyQualifiedName~Maf122ProcessExecutionIntegrationTests|FullyQualifiedName~Maf122WorkflowProcessIntegrationTests|FullyQualifiedName~ProcessLaunchProducerApiTests` | Actual execution/result/artifact boundaries and prepared/caller-intent/API contracts when invalidated; current relevant slice is required at native closure |
| New native diagnosis in an explicitly owned lane | Derive from the P3 real-client reproducer | Distinguish request-scope loss, host restart, catalog and authoring version/launch consequences; known deficiency is not PASS durability |
| `tests/Playwright/CanDoItAll.Tests.Playwright` | `FullyQualifiedName~ProcessesSandboxBrowserTests|FullyQualifiedName~ProcessShellSmokeTests` | Source/published Fast and Parity, actual tabs/canvas/dialogs/charts/Mermaid/files/chat, route/interactivity and geometry |
| Same browser project | `FullyQualifiedName~ProcessNativeBrowserTests` | Actual prepared launch/workflow/native files/chat/attachment/cancel in global and project scope, no duplicate effects |
| Same browser project | `FullyQualifiedName~ProcessWorkbenchBrowserTests|FullyQualifiedName~ProcessVoiceBrowserTests` | Workflow launch with correct project link, actual voice ownership under permitted/denied/delayed browser media; required on invalidation of those seams |

Do not substitute the total count from the tracked PC1 note (or this review) for actual discovery. The previous maintained note reports meaningful execution, but its final raw ledger was not available to this reviewer.

## New regression matrix

| Case | Native/renderer observations |
| --- | --- |
| Save held; same-opening read completes; Save accepted | One command; correct accepted identity/version and receipt; later fields retained; no stuck or prematurely freed mutation slot |
| Save accepted; older read completes afterwards | No acknowledged version regression; no automatic re-save; safe read-only reconciliation |
| Repeat command / alternative action / Enter during pending write | Matching conflict gate; no replaced submission or accidental duplicate Add/Import; distinct independent view can still work |
| Command rejected or genuinely unknown | Draft retained; rejected/unknown distinguished; retry policy matches native knowledge, no blind replay |
| Actual opening/profile/project lifetime change, A→B→A, dispose | All success/error/finally/notification branches respect origin; no successor contamination or hidden global state |
| Add role with old dialog open; Delete current/last role | Correct selection/fields after owner result; no deleted raw draft/errors; correct null/empty state |
| Add/Delete returns after explicit user selection changed | New selection preserved; accepted catalog result accounted for without hijacking it |
| Save non-projection-selected step | Exact step key and nullable/non-null DecisionRoleKey preserved; first step untouched |
| Owner normalizes row while user edits another field | Stable identity, accepted untouched value and later user value both survive; preserve invalid numeric buffer |
| Reachable discard/reject then delayed completion | Correct submission cleanup and no old submission reused by subsequent intent |
| Existing accepted launch/files/live/chat/workflow paths | Preserve PC1 behavior and native effects rather than only labels |

Use TaskCompletionSource/controlled native fixture ordering, not sleeps as correctness oracles. Match bUnit's current dispatcher/disposal conventions. Count side effects after awaiting actual event tasks; wait for editor readiness and real DOM, not just a heading.

## Build and asset floor

Build the current Processes.UI project, native Processes module and Web. Build the sandbox with `ProcessesAssetMode=Parity` and `ProcessesAssetMode=Fast`, using separate mode-specific outputs. Resolve the evaluated dependency closure of UI **and sandbox**, plus relevant sibling packages/source mode; rerun positive and forbidden/unresolved negative guards. Do not claim a text search proves transitive closure.

Run real browser scenarios in source and published modes against task-owned ephemeral ports. Preserve current production and sandbox static asset configuration, `.razor.css`, JS, Tailwind and route discovery. `/two` must keep openings independent. A new generic wrapper or renamed test hook cannot hide missing functionality.

## Closure and broad gates

`portability-static` is mandatory when current protected paths are affected. Follow the current exact command and review intentional baseline changes; final enforcement must run without `--write-baseline`. Do not dismiss ADDED/STALE findings or mechanically accept the whole baseline delta.

Broad Stable is not run per stage. Use current documented triggers: CI/release/merge closure, a frozen checkpoint, or an identified shared-configuration/test-bootstrap/dependency invalidation. Record the trigger, source candidate and actual result. Browser/live/platform lanes remain separate even when Stable passes. If a necessary lane is unavailable, show that status and exact prerequisite; do not remove tests or weaken their assertions to manufacture green.

No fixed benchmark threshold is invented. If PC2 changes the dependency/build/asset/watch loop, measure comparable samples and report conditions. Otherwise validate launchability and record why the earlier measured scenario is not invalidated; do not invent a before/after speedup.
