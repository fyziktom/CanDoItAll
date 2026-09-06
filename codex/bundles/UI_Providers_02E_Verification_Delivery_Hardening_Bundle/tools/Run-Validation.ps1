[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
$proofRoot = Join-Path $repository '.mcp-state/p02e'
$plan = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../plan/owning-plan.json') -Raw | ConvertFrom-Json -AsHashtable
Push-Location $repository
try {
    foreach ($suite in @('Unit','Components','Integration')) {
        $project = 'tests/' + $suite + '/CanDoItAll.Tests.' + $suite + '/CanDoItAll.Tests.' + $suite + '.csproj'
        $capture = Join-Path $proofRoot ('owning-' + $suite + '.txt')
        @('P02E-VERIFY-DELIVERY', ('Start: ' + [DateTimeOffset]::UtcNow.ToString('O')), ('WorkingDirectory: ' + $repository),
          ('dotnet test ' + $project + ' --configuration Release --no-build --no-restore --filter "' + $plan[$suite].filter + '" --logger trx --results-directory .mcp-state/p02e')) | Set-Content -LiteralPath $capture
        & dotnet test $project --configuration Release --no-build --no-restore --filter $plan[$suite].filter --logger ('trx;LogFileName=owning-' + $suite + '.trx') --results-directory $proofRoot *>> $capture
        $resultCode = $LASTEXITCODE
        Add-Content -LiteralPath $capture -Value ('ExitCode: ' + $resultCode)
        Write-Output ($suite + ' exit: ' + $resultCode)
        Get-Content -LiteralPath $capture -Tail 5
        if ($resultCode -ne 0) {
            exit $resultCode
        }
    }
} finally {
    Pop-Location
}

