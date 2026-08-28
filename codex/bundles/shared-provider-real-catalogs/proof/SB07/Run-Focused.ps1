param()
$ErrorActionPreference = 'Stop'
$proof = $PSScriptRoot
$checks = @(
    @{ Name = 'unit'; Project = 'tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj'; Filter = 'FullyQualifiedName~SharedThinkingEffort|FullyQualifiedName~SharedProviderRelayPolicyTests|FullyQualifiedName~SharedProviderPublicationAndCatalogTests|FullyQualifiedName~SharedProviderProtocolContractTests|FullyQualifiedName~ProviderProfileThinkingCapabilityTests|FullyQualifiedName~AgentExecutionPreparationCacheTests' },
    @{ Name = 'components'; Project = 'tests/Components/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj'; Filter = 'FullyQualifiedName~AgentThinkingEffortSettingsTests|FullyQualifiedName~ProviderModelSelectorTests|FullyQualifiedName~AgentDetailsDialogThinkingEffortTests' },
    @{ Name = 'integration'; Project = 'tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj'; Filter = 'FullyQualifiedName~SharedProviderRuntimeProjectionIntegrationTests' }
)
$failed = $false
foreach ($check in $checks) {
    dotnet test $check.Project --no-restore --list-tests --filter $check.Filter -v quiet 2>&1 |
        Tee-Object -FilePath (Join-Path $proof "$($check.Name)-discovery.txt")
    if ($LASTEXITCODE -ne 0) {
        throw "Discovery/build failed: $($check.Name)"
    }
    dotnet test $check.Project --no-build --no-restore --filter $check.Filter --logger "trx;LogFileName=$($check.Name)-final.trx" --results-directory $proof -v quiet 2>&1 |
        Tee-Object -FilePath (Join-Path $proof "$($check.Name)-final.txt")
    if ($LASTEXITCODE -ne 0) {
        $failed = $true
    }
    [xml]$result = Get-Content -LiteralPath (Join-Path $proof "$($check.Name)-final.trx")
    $total = [int]$result.TestRun.ResultSummary.Counters.total
    if ($total -le 0) {
        throw "Zero test discovery: $($check.Name)"
    }
    Write-Output "$($check.Name): $total cases executed; verify exact names against discovery transcript."
}
if ($failed) {
    exit 1
}
