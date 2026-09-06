
$ErrorActionPreference = 'Stop'
$targets = [ordered]@{
 ProviderManagement = 'src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/CanDoItAll.Modules.AgentFramework.ProviderManagement.csproj'
 AgentFramework = 'src/Modules/CanDoItAll.Modules.AgentFramework/CanDoItAll.Modules.AgentFramework.csproj'
 Web = 'src/App/CanDoItAll.Web/CanDoItAll.Web.csproj'
 Unit = 'tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj'
 Components = 'tests/Components/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj'
 Integration = 'tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj'
}
foreach ($item in $targets.GetEnumerator()) {
    dotnet build $item.Value --configuration Release --no-restore /m:1 > ".mcp-state/p02d-final-build-$($item.Key).txt" 2>&1
    $code = $LASTEXITCODE
    [pscustomobject]@{project=$item.Key;exit=$code} | ConvertTo-Json -Compress
    if ($code -ne 0) {
        Get-Content ".mcp-state/p02d-final-build-$($item.Key).txt" -Tail 16
        exit $code
    }
}
