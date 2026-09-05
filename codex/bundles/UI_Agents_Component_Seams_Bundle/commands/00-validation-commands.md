> Implementation authorized by the owner on 2026-09-04. See inputs/04-implementation-authorization.md and reviews/02-execution-status.md. Documentation-only wording below records the preparation stage and does not block this authorized execution.

# Validation commands for later execution

**Do not run implementation gates under the current documentation-only request.** Run from repository root when execution is authorized. Refresh this recipe against docs/testing.md and .github/workflows/ci.yml. Preserve the same sibling source roots/configuration across commands.

## Reusable paths and existing selectors

~~~powershell
$productionProject = ".\src\Modules\CanDoItAll.Modules.AgentFramework\CanDoItAll.Modules.AgentFramework.csproj"
$unitSolution = ".\tests\Solutions\CanDoItAll.Tests.Unit.slnx"
$componentSolution = ".\tests\Solutions\CanDoItAll.Tests.Components.slnx"
$stableSolution = ".\tests\Solutions\CanDoItAll.Tests.Stable.slnx"

$routeFilter = "FullyQualifiedName~CanDoItAll.Tests.Unit.AgentFramework.AgentFrameworkSimpleChatsRouteTests"
$homeFilter = "FullyQualifiedName~CanDoItAll.Tests.Components.AgentFramework.AgentsHomePageTests"
$catalogFilter = "FullyQualifiedName~CanDoItAll.Tests.Components.AgentFramework.AgentCatalogPanelTests"
$detailsFilter = "FullyQualifiedName~CanDoItAll.Tests.Components.AgentFramework.AgentDetailsDialog"
$primaryComponentFilter = "$homeFilter|$catalogFilter|$detailsFilter"
$historyFilter = "FullyQualifiedName=CanDoItAll.Tests.Components.AgentFramework.ProviderAdministrationLayoutTests.History_hosts_make_no_aggregate_or_history_reads_until_requested"
$workflowFilter = "FullyQualifiedName=CanDoItAll.Tests.Components.AgentFramework.WorkflowsPageTests.Agents_shell_exposes_workflows_page_navigation"

$chatContextCases = @(
    "CanDoItAll.Tests.Unit.AgentFramework.AgentFrameworkModuleChatContextBuilderTests.Request_history_context_has_no_invented_summary_or_inherited_agent_selection"
    "CanDoItAll.Tests.Unit.AgentFramework.AgentFrameworkModuleChatContextBuilderTests.Agents_builder_exposes_only_selections_relevant_to_the_active_view"
    "CanDoItAll.Tests.Unit.AgentFramework.AgentFrameworkModuleChatContextBuilderTests.Agents_builder_uses_validated_component_selection_labels"
)
$chatContextFilter = ($chatContextCases | ForEach-Object { "FullyQualifiedName=$_" }) -join "|"
$baselineUnitFilter = "$routeFilter|$chatContextFilter"
$baselineComponentFilter = "$primaryComponentFilter|$historyFilter|$workflowFilter"
~~~

Historical expected anchors: route 10, primary 46, history 2 (Providers/RequestHistory), Workflows button 1. The three selected chat-context methods are additional source-observed candidates; discover their cases. SB01 freezes actual names/data/counts and resolves drift. The final primary count is derived from preserved behaviors, not forced to 46.

## Fresh baseline builds, discovery and execution

~~~powershell
dotnet build $productionProject --configuration Release /m:1
dotnet build $unitSolution --configuration Release /m:1
dotnet build $componentSolution --configuration Release /m:1

dotnet test $unitSolution --configuration Release --no-build --no-restore --list-tests --filter $baselineUnitFilter /m:1
dotnet test $componentSolution --configuration Release --no-build --no-restore --list-tests --filter $baselineComponentFilter /m:1

dotnet test $unitSolution --configuration Release --no-build --no-restore --filter $baselineUnitFilter /m:1
dotnet test $componentSolution --configuration Release --no-build --no-restore --filter $baselineComponentFilter /m:1
~~~

Run dependent commands only after successful builds/discovery reconciliation. Never test stale binaries. Record actual exit codes and runner-expanded theory names, not only a total printed at the end.

## Phase selection

| Phase | Existing core selectors | Additional named coverage frozen before edits |
|---|---|---|
| SB01 | baseline Unit/Components | Characterization gaps and required descendant methods from B01–B30 |
| SB02 | route, home, history, selected chat context | Workspace/query operations, demand/error regions, actual composition |
| SB03 | home, catalog, selected chat context | Requested-open/host results, real catalog operations and managed identity |
| SB04 | affected details, home/catalog host cases | Section/session/load/stale/instance cases and real descendants |
| SB05 | affected details and selected descendants | Policy/real operations/adapters/version/commit/refresh/capability |
| SB06 | all current baseline selectors | Complete replacement/new/child map and hygiene |
| SB07 | final selected union | Actual production composition plus stable/portability/browser gates |

Select exact needed methods from ExternalWorkspaceRootSelectionFieldTests, SharedProviderRefreshButtonTests, AgentMemorySettingsPanelTests/OrderingTests and StorageCatalogSelectionComponentsTests. Do not assume parent details tests exercise them. Namespaces/data cases are rediscovered from actual test source/runner.

There is no prepared quota of new tests. Before each phase, put exact actual fully qualified methods and data-case expectations in its proof manifest. Form filters from those names. For example, after filling the chosen Unit cases:

~~~powershell
$phaseUnitCases = @(
    # Fill from the accepted phase scenario-to-test map before execution.
)
if ($phaseUnitCases.Count -eq 0) {
    throw "Freeze exact phase Unit cases before running the selector."
}
$phaseUnitFilter = ($phaseUnitCases | ForEach-Object { "FullyQualifiedName=$_" }) -join "|"

dotnet build $unitSolution --configuration Release /m:1
dotnet test $unitSolution --configuration Release --no-build --no-restore --list-tests --filter $phaseUnitFilter /m:1
dotnet test $unitSolution --configuration Release --no-build --no-restore --filter $phaseUnitFilter /m:1
~~~

Use the same method-list approach for added Components/composition cases. A method with multiple data cases requires the manifest to list those cases even when the filter selects the method. Zero matches, missing expected cases or unexplained extra discovery fail the selection gate.

## Temporary hygiene and architecture evidence

These are review aids, not permanent tests of source shape. Enumerate the actual changed harnesses and new shared helpers before SB06, then inspect all hits:

~~~powershell
rg -n 'BindingFlags\.NonPublic|GetUninitializedObject|selectedTabIndex|GetField\(|GetMethod\(' tests/Components/CanDoItAll.Tests.Components -g 'AgentsHomePage*' -g 'AgentCatalogPanelTests.cs' -g 'AgentDetailsDialog*'
rg -n 'OpenWorkflows|BindingFlags\.NonPublic' tests/Components/CanDoItAll.Tests.Components/WorkflowsPageTests.cs
rg -n 'IDbContextFactory|Microsoft.EntityFrameworkCore|AiResourceBinding' src/Modules/CanDoItAll.Modules.AgentFramework/Pages -g 'AgentsHomePage.razor' -g 'AgentsHomePage.razor.cs'
rg -n 'DialogService|IAgentChatLauncher|IAgentFrameworkWorkspaceService|IProviderRuntimeAdministrationService|IServiceProvider' src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components -g 'AgentCatalogPanel.razor' -g 'AgentCatalogPanel.razor.cs'
rg -n 'IAgentFrameworkWorkspaceService|IProviderRuntimeAdministrationService|ProjectsService|SecretService|IExternalTargetPathRegistryFactory|IDbContextFactory|IServiceProvider' src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components -g 'AgentDetailsDialog.razor' -g 'AgentDetailsDialog.razor.cs'
~~~

On shells where glob paths are not expanded, use directory roots with rg -g file patterns. Review namespace-only hits and unrelated Workflows methods precisely. Required parent I/O and affected private test coupling must be absent after closure; do not widen scope to every reflection use in an unrelated suite.

Durable tests inspect actual boundary types/dependency direction. Also audit constructor graphs, real descendants, public result type assemblies and evaluated project/static-asset dependencies. Neither grep nor metadata alone proves isolation.

## Final focused and stable gates

Rebuild changed owning production/test projects and run the final selected Unit/Components union with recorded discovery before the broad gate. Required once at SB07 for UI DI/composition change:

~~~powershell
dotnet restore .\CanDoItAll.slnx
dotnet build .\CanDoItAll.slnx --configuration Release --no-restore /m:1
dotnet restore $stableSolution
dotnet build $stableSolution --configuration Release --no-restore /m:1
dotnet test $stableSolution --configuration Release --no-build --no-restore --filter "Category!=Playwright&Category!=LiveProcess&Category!=LongRunning&Category!=Quarantined&Category!=UnixRuntimePortability&RequiresHostDocker!=true" /m:1
~~~

Use current repository policy if this selector changes before execution. Repeat broad testing only after changes/failures invalidate the prior result.

## Portability-static closure after final source/test edits

~~~powershell
$portabilityScan = Join-Path ([System.IO.Path]::GetTempPath()) (
    "candoitall-portability-{0}.json" -f [guid]::NewGuid().ToString("N")
)
python .\tools\Validation\Portability\test_enforce_portability_baseline.py
python .\tools\Validation\Portability\test_scan_artifacts_for_secrets.py
python .\tools\Validation\Portability\scan_portability.py --repo-root . --output $portabilityScan --tracked-only
python .\tools\Validation\Portability\enforce_portability_baseline.py --scan $portabilityScan --baseline .\tools\Validation\Portability\portability-risk-baseline.json
~~~

Review every ADDED/STALE finding; repair genuine defects, regenerate after source edits, refresh only intentional reviewed baseline deltas under docs/testing.md, inspect the diff, and finish without --write-baseline. Check how current scanner covers newly added files before accepting a tracked-only scan; use the repository-prescribed tracked/index workflow so new source is not omitted.

## Browser, measurement and proof

Execute [real-host scenarios](../plan/02-proof-and-validation-plan.md) and [baseline/comparison protocol](../plan/03-sandbox-and-navigation-handoff.md). Capture actual commands, URLs, data profile, source identities, screenshots, interactions, console results and timing samples. Use [proof placement](../proof/README.md) for artifact hashes and readiness.

## Governed semantic and anti-stub audit

Freeze the actual changed production file list and inspect it for TODO/NotImplemented paths, test-only switches, fixture-specific branching and template-only output. A reproducible review aid is:

~~~powershell
rg -n 'TODO|NotImplemented|IsTest|TestOnly|Fixture|sample[-_ ]?only|template[-_ ]?only' src/Modules/CanDoItAll.Modules.AgentFramework -g '*.cs' -g '*.razor'
~~~

Record the command/output even when there are no hits. Review relevant flows manually; a clean text scan is insufficient. Existing unrelated hits are classified, not swept into this refactor. Map production producers, consumers and lifetimes for session/selection/operation outcomes, and capture before/after changed-file hashes plus positive/negative evidence in the portable semantic-invariant contract. Follow proof/README.md for the required governed manifest and verifier artifact.
