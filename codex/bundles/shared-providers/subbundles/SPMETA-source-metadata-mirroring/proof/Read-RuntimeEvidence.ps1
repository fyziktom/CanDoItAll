param([Parameter(Mandatory)] [DateTimeOffset] $SinceUtc, [Parameter(Mandatory)] [string] $RunLabel)
$ErrorActionPreference = 'Stop'
Start-Transcript -Path (Join-Path $PSScriptRoot "transcripts\$RunLabel-runtime.txt") -Force | Out-Null
try {
    Write-Output "SPMETA META-NAMES META-PRICES META-PRIVATE META-SETTINGS META-E2E; read-only runtime evidence; cwd=$((Get-Location).Path); since=$($SinceUtc.ToUniversalTime().ToString('O'))"
    $dbUser = (docker exec candoitall-spui-db printenv POSTGRES_USER).Trim()
    $clientSql = 'SELECT "Id", "Name", "DefaultModel" FROM "Workspace_ProviderProfiles" WHERE "Name" LIKE ''UI Shared%'' ORDER BY "Name"; SELECT "ProviderProfileId", "RemotePublicationId", "RemoteRevision", "RemoteCatalogSnapshotJson"::jsonb->>''schemaVersion'' AS schema FROM "Workspace_SharedProviderImports" ORDER BY "ProviderProfileId";'
    Write-Output "Client identity query: $clientSql"
    docker exec candoitall-spui-db psql -U $dbUser -d candoitall_e2e_client_b -c $clientSql | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw 'Client identity query failed.'
    }
    $sinceText = $SinceUtc.ToUniversalTime().ToString('O')
    $ledgerSql = 'SELECT "StartedAtUtc", "Operation", "UpstreamModelId", "Outcome", "FailureCategory", "InputTokenCount", "OutputTokenCount", "ImageCount", "UsageCompleteness", "Price", "PricingCompleteness" FROM "Workspace_SharedProviderInvocations" WHERE "StartedAtUtc" >= ''' + $sinceText + ''' ORDER BY "StartedAtUtc";'
    Write-Output "Central production-ledger query: $ledgerSql"
    docker exec candoitall-spui-db psql -U $dbUser -d candoitall_e2e_central -c $ledgerSql | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw 'Central ledger query failed.'
    }
    $countsSql = 'SELECT count(*)::text || ''|'' || count(*) FILTER (WHERE "Outcome" = ''Succeeded'' AND "UsageCompleteness" = ''Complete'')::text || ''|'' || count(*) FILTER (WHERE "Operation" = ''ImageGenerations'' AND "ImageCount" = 1)::text FROM "Workspace_SharedProviderInvocations" WHERE "StartedAtUtc" >= ''' + $sinceText + ''';'
    Write-Output "Central run-count assertion query: $countsSql"
    $counts = (docker exec candoitall-spui-db psql -U $dbUser -d candoitall_e2e_central -Atc $countsSql).Trim()
    if ($LASTEXITCODE -ne 0 -or $counts -ne '8|8|1') {
        throw "Expected eight complete successes including one generated image; actual counts: $counts"
    }
    Write-Output "PASS production ledger counts: $counts"
    $controlToken = (docker exec candoitall-spui-upstream /usr/local/bin/busybox cat /run/secrets/upstream-control-token).Trim()
    $capture = Invoke-RestMethod -Uri 'http://127.0.0.1:5213/_test/captures' -Headers @{Authorization="Bearer $controlToken"}
    $controlToken = $null
    Write-Output 'GET /_test/captures: credential and request bodies omitted; only safe route/model/status/image-presence facts follow.'
    $requests = @($capture.requests | Where-Object { [DateTimeOffset]$_.received_at_utc -ge $SinceUtc })
    if ($requests.Count -ne 8 -or @($requests | Where-Object { $_.response_status_code -ne 200 }).Count -ne 0) {
        throw 'Expected eight successful upstream requests in the completed UI run.'
    }
    foreach ($request in $requests) {
        $model = [regex]::Match($request.body, '"model"\s*:\s*"([^"]+)"').Groups[1].Value
        [pscustomobject]@{
            Time=$request.received_at_utc
            Path=$request.path
            Model=$model
            Status=$request.response_status_code
            HasImageContent=$request.body.Contains('image_url') -or $request.body.Contains('data:image')
            BodyTruncated=$request.body_truncated
        } | ConvertTo-Json -Compress | Write-Output
    }
    Write-Output 'docker exec candoitall-spui-client busybox stat /data/workspace/shared-provider-ui/generated.png; sha256sum; PNG header'
    $imagePath = '/data/workspace/shared-provider-ui/generated.png'
    docker exec candoitall-spui-client /usr/local/bin/busybox stat $imagePath | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw 'The generated image artifact is missing.'
    }
    $modifiedUnix = docker exec candoitall-spui-client /usr/local/bin/busybox stat -c '%Y' $imagePath
    if ([DateTimeOffset]::FromUnixTimeSeconds([long]$modifiedUnix) -lt $SinceUtc) {
        throw 'The generated image predates this UI run.'
    }
    $pngHeader = (docker exec candoitall-spui-client /usr/local/bin/busybox od -An -tx1 -N8 $imagePath).Trim()
    if ($pngHeader -ne '89 50 4e 47 0d 0a 1a 0a') {
        throw 'The generated artifact does not have a PNG signature.'
    }
    Write-Output "PASS newly generated PNG signature: $pngHeader"
    docker exec candoitall-spui-client /usr/local/bin/busybox sha256sum $imagePath | Out-Host
    foreach ($url in @('http://127.0.0.1:5210/health', 'http://127.0.0.1:5212/health')) {
        $health = Invoke-WebRequest $url -TimeoutSec 15
        Write-Output "$url HTTP $($health.StatusCode): $($health.Content)"
    }
    foreach ($container in @('candoitall-spui-shared', 'candoitall-spui-client')) {
        $logs = docker logs --since $sinceText $container 2>&1
        $errors = @($logs | Where-Object { "$_" -match '^fail:|^crit:|^Unhandled exception' })
        Write-Output "docker logs --since $sinceText $container : $($errors.Count) error/critical/unhandled-exception headings; log bodies withheld."
        if ($LASTEXITCODE -ne 0 -or $errors.Count -gt 0) {
            throw "Unexpected application errors in $container during this UI run."
        }
    }
    docker ps --filter name=candoitall-spui --format '{{.Names}} {{.Image}} {{.Status}} {{.Ports}}' | Out-Host
    Write-Output 'Runtime evidence read completed. Exit code: 0'
} finally {
    Stop-Transcript | Out-Null
}
