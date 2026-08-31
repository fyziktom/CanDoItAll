param(
    [Parameter(Mandatory)][string]$CapturePath,
    [Parameter(Mandatory)][string]$RunRoot,
    [Parameter(Mandatory)][Guid]$AgentId,
    [Parameter(Mandatory)][string]$OutputPath
)
$ErrorActionPreference = 'Stop'
$events = @(Get-Content -LiteralPath $CapturePath | ForEach-Object { $_ | ConvertFrom-Json -DateKind String })
$runMappings = @($events | Where-Object kind -eq 'agent-run-trace')
$httpStarts = @($events | Where-Object kind -eq 'http-send-start')
$allowedPhases = @('Planning', 'Framework', 'Approval', 'Context contributors', 'Skills', 'Workspace tools', 'Runtime tool providers', 'Capability', 'Compaction', 'Execution authority', 'Model parameters', 'Session', 'Run', 'Streaming', 'Completed', 'Failed', 'Cancelled')
$rows = @()
$unmatchedStarts = 0
$otherAgentStarts = 0
foreach ($httpStart in $httpStarts) {
    $matches = @($runMappings | Where-Object { $_.traceId -eq $httpStart.traceId -and $_.spanId -eq $httpStart.parentSpanId })
    $matchingRunIds = @($matches.runId | Sort-Object -Unique)
    if ($matchingRunIds.Count -eq 0) {
        $unmatchedStarts++
        continue
    }
    if ($matchingRunIds.Count -ne 1) {
        throw 'Ambiguous direct parent span association.'
    }
    $runId = [Guid]$matchingRunIds[0]
    $runDirectory = Join-Path $RunRoot $runId.ToString('N')
    $run = Get-Content -LiteralPath (Join-Path $runDirectory 'run.json') -Raw | ConvertFrom-Json -DateKind String
    if ([Guid]$run.agentId -ne $AgentId) {
        $otherAgentStarts++
        continue
    }
    $logs = @(Get-ChildItem -LiteralPath (Join-Path $runDirectory 'logs') -Filter '*.json' -File | ForEach-Object {
        $log = Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json -DateKind String
        if ($log.phase -in $allowedPhases) {
            [pscustomobject]@{ utc = $log.createdAtUtc; phase = $log.phase; state = $log.state }
        }
    } | Sort-Object utc)
    $runtimeRun = $logs | Where-Object { $_.phase -eq 'Run' -and $_.state -eq 2 } | Select-Object -First 1
    $firstStreaming = $logs | Where-Object phase -eq 'Streaming' | Select-Object -First 1
    $httpStop = $events | Where-Object { $_.kind -eq 'http-send-stop' -and $_.traceId -eq $httpStart.traceId -and $_.spanId -eq $httpStart.spanId } | Select-Object -First 1
    $createdToDispatch = ([DateTimeOffset]$httpStart.utc - [DateTimeOffset]$run.createdAtUtc).TotalSeconds
    $runtimeRunToDispatch = if ($null -ne $runtimeRun) { ([DateTimeOffset]$httpStart.utc - [DateTimeOffset]$runtimeRun.utc).TotalSeconds } else { $null }
    $rows += [pscustomobject]@{
        runId = $runId.ToString('D')
        agentId = $AgentId.ToString('D')
        chatSessionId = $run.chatSessionId
        createdUtc = $run.createdAtUtc
        runtimeRunPersistedEventUtc = $runtimeRun.utc
        httpDispatchUtc = $httpStart.utc
        httpResponseHeadersUtc = $httpStop.utc
        firstPersistedStreamingEventUtc = $firstStreaming.utc
        completedUtc = $run.completedAtUtc
        persistedState = $run.state
        createdToDispatchSeconds = [Math]::Round($createdToDispatch, 6)
        runtimeRunEventToDispatchSeconds = if ($null -ne $runtimeRunToDispatch) { [Math]::Round($runtimeRunToDispatch, 6) } else { $null }
        traceId = $httpStart.traceId
        httpSpanId = $httpStart.spanId
        httpParentSpanId = $httpStart.parentSpanId
        association = 'exact-http-parent-span-to-run-activity-span'
        stages = $logs
    }
}
$proof = [ordered]@{
    schemaVersion = 1
    capturedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    eventBoundary = 'HttpHandlerDiagnosticListener/System.Net.Http.HttpRequestOut.Start'
    runtimeRunBoundary = 'persisted log createdAtUtc; this is before awaited durable write, not HTTP dispatch'
    responseHeadersBoundary = 'HTTP diagnostic Stop; not first UI token'
    unmatchedHttpStarts = $unmatchedStarts
    otherAgentHttpStarts = $otherAgentStarts
    count = $rows.Count
    runs = @($rows | Sort-Object httpDispatchUtc)
}
$proof | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $OutputPath -Encoding utf8NoBOM
[pscustomobject]@{ Count = $rows.Count; UnmatchedHttpStarts = $unmatchedStarts; OtherAgentHttpStarts = $otherAgentStarts; Output = $OutputPath }
