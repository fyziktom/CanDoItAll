$ErrorActionPreference = 'Stop'
Start-Transcript -Path (Join-Path $PSScriptRoot 'transcripts/runtime-evidence.txt') -Force | Out-Null
try {
    [xml] $trx = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'transcripts/browser-final.trx') -Raw
    $since = ([DateTimeOffset] $trx.TestRun.Times.start).ToUniversalTime().ToString('O')
    $until = ([DateTimeOffset] $trx.TestRun.Times.finish).ToUniversalTime().ToString('O')
    Write-Output "LOCAL-UI-ACCESS API-BOUNDARY; final browser run $since through $until"
    $dbUser = (docker exec candoitall-spui-db printenv POSTGRES_USER).Trim()
    $window = '"StartedAtUtc" >= ''' + $since + ''' AND "StartedAtUtc" <= ''' + $until + ''''
    $ledgerSql = 'SELECT "Id", "StartedAtUtc", "UpstreamModelId", "Outcome", "InputTokenCount", "OutputTokenCount", "UsageCompleteness" FROM "Workspace_SharedProviderInvocations" WHERE ' + $window + ' ORDER BY "StartedAtUtc";'
    Write-Output "Command: docker exec candoitall-spui-db psql (read-only source ledger) $ledgerSql"
    docker exec candoitall-spui-db psql -U $dbUser -d candoitall_e2e_central -c $ledgerSql | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw 'Source usage query failed.'
    }
    $assertSql = 'SELECT "UpstreamModelId" FROM "Workspace_SharedProviderInvocations" WHERE ' + $window + ' AND "Outcome" = ''Succeeded'' AND "UsageCompleteness" = ''Complete'';'
    $models = @(docker exec candoitall-spui-db psql -U $dbUser -d candoitall_e2e_central -Atc $assertSql)
    if ($LASTEXITCODE -ne 0) {
        throw 'Source usage assertions failed.'
    }
    foreach ($model in @('gpt-5.4-mini', 'gemma3:4b')) {
        if ($models -notcontains $model) {
            throw "Missing complete shared invocation for $model"
        }
        Write-Output "PASS real shared model and complete token usage: $model"
    }
    foreach ($port in @(5210, 5212)) {
        Write-Output "Command: GET http://127.0.0.1:$port/health and unauthenticated API/file routes"
        $health = Invoke-WebRequest -Uri "http://127.0.0.1:$port/health" -TimeoutSec 15
        if ($health.StatusCode -ne 200 -or $health.Content.Trim() -ne 'Healthy') {
            throw "Unhealthy instance: $port"
        }
        Write-Output "$port HTTP 200 Healthy"
        foreach ($route in @('/api/llm-chats', '/authorized-files/content')) {
            $response = Invoke-WebRequest -Uri "http://127.0.0.1:$port$route" -SkipHttpErrorCheck -TimeoutSec 15
            if ($response.StatusCode -ne 401) {
                throw "Anonymous boundary failure: $port$route HTTP $($response.StatusCode)"
            }
            Write-Output "$port$route anonymous HTTP 401"
        }
    }
    foreach ($container in @('candoitall-spui-shared', 'candoitall-spui-client')) {
        Write-Output "Command: docker logs --since $since --until $until $container (failure headings only)"
        $logs = docker logs --since $since --until $until $container 2>&1
        $failures = @($logs | Where-Object { "$_" -match '^fail:|^crit:|^Unhandled exception' })
        Write-Output "$container : $($failures.Count) failure headings; bodies withheld"
        if ($LASTEXITCODE -ne 0 -or $failures.Count -gt 0) {
            throw 'Unexpected application failure during final browser run.'
        }
    }
    Write-Output 'Command: docker inspect test image, loopback ports and configured UI ingress (no secrets)'
    docker inspect candoitall-spui-shared candoitall-spui-client --format '{{.Name}} image={{.Image}} {{range $p, $v := .HostConfig.PortBindings}}{{range $v}}{{.HostIp}}:{{.HostPort}} {{end}}{{end}}' | Out-Host
    foreach ($container in @('candoitall-spui-shared', 'candoitall-spui-client')) {
        $gateway = docker exec $container printenv WebHost__LocalOperatorUi__TrustedAddresses__0
        Write-Output "$container trusted local UI ingress=$gateway"
    }
    Write-Output 'PASS browser, real source usage, API boundary and both hosts. Exit code: 0'
} finally {
    Stop-Transcript | Out-Null
}
