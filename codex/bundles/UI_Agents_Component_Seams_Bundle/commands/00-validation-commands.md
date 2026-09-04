# Validation commands

Run from the repository root with the same sibling source roots for every command.
Refresh paths/configuration if the repository testing guide changes.

## Variables

```powershell
$productionProject = ".\src\Modules\CanDoItAll.Modules.AgentFramework\CanDoItAll.Modules.AgentFramework.csproj"
$unitSolution = ".\tests\Solutions\CanDoItAll.Tests.Unit.slnx"
$componentSolution = ".\tests\Solutions\CanDoItAll.Tests.Components.slnx"
$stableSolution = ".\tests\Solutions\CanDoItAll.Tests.Stable.slnx"

$routeFilter = "FullyQualifiedName~CanDoItAll.Tests.Unit.AgentFramework.AgentFrameworkSimpleChatsRouteTests"
$primaryComponentFilter = "FullyQualifiedName~CanDoItAll.Tests.Components.AgentFramework.AgentsHomePageTests|FullyQualifiedName~CanDoItAll.Tests.Components.AgentFramework.AgentCatalogPanelTests|FullyQualifiedName~CanDoItAll.Tests.Components.AgentFramework.AgentDetailsDialog"
$newSeamUnitFilter = "FullyQualifiedName~CanDoItAll.Tests.Unit.AgentFramework.AgentsWorkspaceStateTests|FullyQualifiedName~CanDoItAll.Tests.Unit.AgentFramework.AgentsOverviewQueryTests|FullyQualifiedName~CanDoItAll.Tests.Unit.AgentFramework.AgentCatalogControllerTests|FullyQualifiedName~CanDoItAll.Tests.Unit.AgentFramework.AgentEditorControllerTests|FullyQualifiedName~CanDoItAll.Tests.Unit.AgentFramework.AgentUiDependencyBoundaryTests"
```

## SB01 baseline discovery and execution

Expected current discovery:

```text
$routeFilter: 10
$primaryComponentFilter: 46
```

```powershell
dotnet build $productionProject --configuration Release /m:1

dotnet test $unitSolution --configuration Release --list-tests --filter $routeFilter /m:1
dotnet test $unitSolution --configuration Release --no-build --no-restore --filter $routeFilter /m:1

dotnet test $componentSolution --configuration Release --list-tests --filter $primaryComponentFilter /m:1
dotnet test $componentSolution --configuration Release --no-build --no-restore --filter $primaryComponentFilter /m:1
```

If the test assemblies were not refreshed for current source, build the owning solution
before using `--no-build --no-restore`.

## New seam tests

Prepared expected discovery: 18. SB02 must freeze exact method/case names before relying
on this count; if a theory changes discovery, update the proof plan first.

```powershell
dotnet test $unitSolution --configuration Release --list-tests --filter $newSeamUnitFilter /m:1
dotnet test $unitSolution --configuration Release --no-build --no-restore --filter $newSeamUnitFilter /m:1
```

## Target test-hygiene check

This is temporary bundle proof, not a permanent product test:

```powershell
$targetTests = @(
  ".\tests\Components\CanDoItAll.Tests.Components\AgentCatalogPanelTests.cs",
  ".\tests\Components\CanDoItAll.Tests.Components\AgentDetailsDialogDeletionTests.cs",
  ".\tests\Components\CanDoItAll.Tests.Components\AgentDetailsDialogCapabilityTests.cs",
  ".\tests\Components\CanDoItAll.Tests.Components\AgentDetailsDialogThinkingEffortTests.cs",
  ".\tests\Components\CanDoItAll.Tests.Components\AgentDetailsDialogAvatarGenerationTests.cs",
  ".\tests\Components\CanDoItAll.Tests.Components\AgentDetailsDialogProjectStructureAccessTests.cs",
  ".\tests\Components\CanDoItAll.Tests.Components\AgentDetailsDialogSettingsTests.cs"
)

$forbidden = "BindingFlags\.NonPublic|GetField\(|GetMethod\(|RuntimeHelpers\.GetUninitializedObject|selectedTabIndex"
$matches = Select-String -Path $targetTests -Pattern $forbidden
if ($matches) {
  $matches | Format-Table Path, LineNumber, Line -AutoSize
  throw "Target Agents component tests still depend on private/source-shape seams."
}
```

## Production Razor dependency check

Temporary evidence only; the durable dependency test remains authoritative:

```powershell
Select-String `
  -Path ".\src\Modules\CanDoItAll.Modules.AgentFramework\Pages\AgentsHomePage.razor*" `
  -Pattern "IDbContextFactory|Microsoft.EntityFrameworkCore|AiResourceBinding"

Select-String `
  -Path ".\src\Modules\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentCatalogPanel.razor*" `
  -Pattern "@inject|\[Inject\]|DialogService|NotificationService|IAgentChatLauncher|IAgentFrameworkWorkspaceService|IProviderRuntimeAdministrationService"

Select-String `
  -Path ".\src\Modules\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentDetailsDialog.razor*" `
  -Pattern "IAgentFrameworkWorkspaceService|IProviderRuntimeAdministrationService|ProjectsService|SecretService|IExternalTargetPathRegistryFactory|IDbContextFactory|IServiceProvider"
```

All three commands must return no forbidden production dependency match after closure.
Review namespace-only false positives rather than weakening the durable test.

## Final focused gate

```powershell
dotnet build $productionProject --configuration Release /m:1

dotnet test $unitSolution --configuration Release --list-tests --filter $routeFilter /m:1
dotnet test $unitSolution --configuration Release --no-build --no-restore --filter $routeFilter /m:1

dotnet test $unitSolution --configuration Release --list-tests --filter $newSeamUnitFilter /m:1
dotnet test $unitSolution --configuration Release --no-build --no-restore --filter $newSeamUnitFilter /m:1

dotnet test $componentSolution --configuration Release --list-tests --filter $primaryComponentFilter /m:1
dotnet test $componentSolution --configuration Release --no-build --no-restore --filter $primaryComponentFilter /m:1
```

Also run the exact rewritten Workflows navigation case by full name, expected discovery 1.
Record its final name in SB06 proof before execution.

## Final stable gate

Required once at SB07 because UI DI/composition changed:

```powershell
dotnet restore .\CanDoItAll.slnx
dotnet build .\CanDoItAll.slnx --configuration Release --no-restore /m:1
dotnet restore $stableSolution
dotnet build $stableSolution --configuration Release --no-restore /m:1
dotnet test $stableSolution --configuration Release --no-build --no-restore --filter "Category!=Playwright&Category!=LiveProcess&Category!=LongRunning&Category!=Quarantined&Category!=UnixRuntimePortability&RequiresHostDocker!=true" /m:1
```

## Portability-static gate

```powershell
$portabilityScan = Join-Path ([System.IO.Path]::GetTempPath()) (
    "candoitall-portability-{0}.json" -f [guid]::NewGuid().ToString("N")
)

python .\tools\Validation\Portability\test_enforce_portability_baseline.py
python .\tools\Validation\Portability\test_scan_artifacts_for_secrets.py
python .\tools\Validation\Portability\scan_portability.py --repo-root . --output $portabilityScan --tracked-only
python .\tools\Validation\Portability\enforce_portability_baseline.py --scan $portabilityScan --baseline .\tools\Validation\Portability\portability-risk-baseline.json
```

If reviewed intentional deltas require a baseline refresh, follow `docs/testing.md`,
inspect the diff, and finish with no-write enforcement.

## Browser smoke

Use the real app and a 1600x1000-or-larger desktop viewport. Record source SHA, database
profile, exact URL, screenshots, browser console result, and the five scenarios in
`plan/02-proof-and-validation-plan.md`. This is host evidence, not a permanent source-shape
test.
