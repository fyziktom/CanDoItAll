$ErrorActionPreference = 'Stop'
$reviewRoot = Join-Path $PSScriptRoot '../reviews'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
Set-Location -LiteralPath $repoRoot
$log = Join-Path $reviewRoot 'sb09-builds.log'
"CP-MERGE-FROZEN: shared persistence, runtime and public contract merge gate; $(Get-Date -Format O)" | Set-Content -LiteralPath $log
function Invoke-RecordedDotnet([string[]]$Arguments) {
    "COMMAND dotnet $($Arguments -join ' ')" | Add-Content -LiteralPath $log
    & dotnet @Arguments *>> $log
    $result = $LASTEXITCODE
    "EXIT $result" | Add-Content -LiteralPath $log
    if ($result -ne 0) { Get-Content -LiteralPath $log -Tail 20; throw "Frozen build command failed." }
}
Invoke-RecordedDotnet -Arguments @('build','src/Integration/CanDoItAll.SharedProviders.Http/CanDoItAll.SharedProviders.Http.csproj','-c','Release','--artifacts-path','./artifacts/premerge','--no-restore','/m:1')
Invoke-RecordedDotnet -Arguments @('build','src/MAF/ProviderHistory/CanDoItAll.AgentFramework.ProviderHistory.Abstractions/CanDoItAll.AgentFramework.ProviderHistory.Abstractions.csproj','-c','Release','--artifacts-path','./artifacts/premerge','--no-restore','/m:1')
Invoke-RecordedDotnet -Arguments @('build','src/MAF/ProviderHistory/CanDoItAll.AgentFramework.ProviderHistory.Application/CanDoItAll.AgentFramework.ProviderHistory.Application.csproj','-c','Release','--artifacts-path','./artifacts/premerge','--no-restore','/m:1')
Invoke-RecordedDotnet -Arguments @('build','src/MAF/ProviderHistory/CanDoItAll.AgentFramework.ProviderHistory.Persistence/CanDoItAll.AgentFramework.ProviderHistory.Persistence.csproj','-c','Release','--artifacts-path','./artifacts/premerge','--no-restore','/m:1')
Invoke-RecordedDotnet -Arguments @('build','src/MAF/Common/CanDoItAll.AgentFramework.Providers/CanDoItAll.AgentFramework.Providers.csproj','-c','Release','--artifacts-path','./artifacts/premerge','--no-restore','/m:1')
Invoke-RecordedDotnet -Arguments @('build','src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj','-c','Release','--artifacts-path','./artifacts/premerge','--no-restore','/m:1')
Invoke-RecordedDotnet -Arguments @('build','src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/CanDoItAll.Modules.AgentFramework.ProviderManagement.csproj','-c','Release','--artifacts-path','./artifacts/premerge','--no-restore','/m:1')
Invoke-RecordedDotnet -Arguments @('build','src/App/CanDoItAll.Composition/CanDoItAll.Composition.csproj','-c','Release','--artifacts-path','./artifacts/premerge','--no-restore','/m:1')
Invoke-RecordedDotnet -Arguments @('build','src/App/CanDoItAll.Web/CanDoItAll.Web.csproj','-c','Release','--artifacts-path','./artifacts/premerge','--no-restore','/m:1')
Invoke-RecordedDotnet -Arguments @('restore','CanDoItAll.slnx','--artifacts-path','./artifacts/premerge')
Invoke-RecordedDotnet -Arguments @('build','CanDoItAll.slnx','-c','Release','--artifacts-path','./artifacts/premerge','--no-restore','/m:1')
Invoke-RecordedDotnet -Arguments @('restore','tests/Solutions/CanDoItAll.Tests.Stable.slnx','--artifacts-path','./artifacts/premerge')
Invoke-RecordedDotnet -Arguments @('build','tests/Solutions/CanDoItAll.Tests.Stable.slnx','-c','Release','--artifacts-path','./artifacts/premerge','--no-restore','/m:1')
Get-Content -LiteralPath $log -Tail 8
