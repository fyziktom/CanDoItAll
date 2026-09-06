
$ErrorActionPreference = 'Stop'
$plan = Get-Content codex/bundles/UI_Providers_02D_Recovery_Bundle/proof/D/focused-plan.json -Raw | ConvertFrom-Json
foreach ($name in @('Unit','Components','Integration')) {
    dotnet test "tests/Solutions/CanDoItAll.Tests.$name.slnx" --configuration Release --no-build --no-restore --filter $plan.$name.filter --logger "trx;LogFileName=owning-$name.trx" --results-directory .mcp-state/p02d-results /m:1 > ".mcp-state/p02d-owning-$name.txt" 2>&1
    $code = $LASTEXITCODE
    [pscustomobject]@{suite=$name;exit=$code} | ConvertTo-Json -Compress
    Get-Content ".mcp-state/p02d-owning-$name.txt" -Tail 4
}
