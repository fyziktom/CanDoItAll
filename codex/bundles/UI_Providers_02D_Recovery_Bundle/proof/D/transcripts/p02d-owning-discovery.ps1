
$plan = Get-Content codex/bundles/UI_Providers_02D_Recovery_Bundle/proof/D/focused-plan.json -Raw | ConvertFrom-Json
foreach ($name in @('Unit','Components','Integration')) {
    dotnet test "tests/Solutions/CanDoItAll.Tests.$name.slnx" --configuration Release --no-build --no-restore --list-tests --filter $plan.$name.filter /m:1 > ".mcp-state/p02d-owning-$name-list.txt" 2>&1
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    $actual = (Select-String -Path ".mcp-state/p02d-owning-$name-list.txt" -Pattern '^    CanDoItAll.Tests.').Count
    [pscustomobject]@{suite=$name;expected=$plan.$name.expectedDiscovery;actual=$actual} | ConvertTo-Json -Compress
}
