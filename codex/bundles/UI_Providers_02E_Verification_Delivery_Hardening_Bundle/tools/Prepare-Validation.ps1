[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
$proofRoot = Join-Path $repository '.mcp-state/p02e'
$plan = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../plan/owning-plan.json') -Raw | ConvertFrom-Json -AsHashtable
Push-Location $repository
try {
    $projects = [ordered]@{
        ProviderManagement = 'src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/CanDoItAll.Modules.AgentFramework.ProviderManagement.csproj'
        AgentFramework = 'src/Modules/CanDoItAll.Modules.AgentFramework/CanDoItAll.Modules.AgentFramework.csproj'
        Web = 'src/App/CanDoItAll.Web/CanDoItAll.Web.csproj'
        Unit = 'tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj'
        Components = 'tests/Components/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj'
        Integration = 'tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj'
    }
    foreach ($project in $projects.GetEnumerator()) {
        $capture = Join-Path $proofRoot ('final-build-' + $project.Key + '.txt')
        @('P02E-VERIFY-DELIVERY', ('Start: ' + [DateTimeOffset]::UtcNow.ToString('O')), ('WorkingDirectory: ' + $repository),
          ('dotnet build ' + $project.Value + ' --configuration Release --no-restore /m:1')) | Set-Content -LiteralPath $capture
        & dotnet build $project.Value --configuration Release --no-restore /m:1 *>> $capture
        $resultCode = $LASTEXITCODE
        Add-Content -LiteralPath $capture -Value ('ExitCode: ' + $resultCode)
        Write-Output ($project.Key + ' build exit: ' + $resultCode)
        if ($resultCode -ne 0) {
            Get-Content -LiteralPath $capture -Tail 18
            exit $resultCode
        }
    }
    foreach ($suite in @('Unit','Components','Integration')) {
        $capture = Join-Path $proofRoot ('final-list-' + $suite + '.txt')
        & dotnet test $projects[$suite] --configuration Release --no-build --no-restore --list-tests --filter $plan[$suite].filter *> $capture
        if ($LASTEXITCODE -ne 0) {
            throw ('Discovery failed: ' + $suite)
        }
        $discovered = @(Get-Content -LiteralPath $capture | Where-Object { $_ -match '^    CanDoItAll.Tests\.' }).Count
        Write-Output ($suite + ': expected ' + $plan[$suite].expectedDiscovery + ', discovered ' + $discovered)
        if ($discovered -ne $plan[$suite].expectedDiscovery) {
            throw ('Unexpected discovery: ' + $suite)
        }
    }
} finally {
    Pop-Location
}

