param([Parameter(Mandatory)][ValidateSet('before','after')][string]$Stage)
$ErrorActionPreference = 'Stop'
$proofRoot = 'C:/repositories/CanDoItAll/codex/bundles/agent-startup-performance/proof/deployment'
$paused = Get-Content -LiteralPath (Join-Path $proofRoot 'historical-paused-run-before.json') -Raw | ConvertFrom-Json
$executionRoot = $paused.executionRoot
$runRoot = Join-Path $executionRoot 'runs'
$journalPaths = @(Get-ChildItem -LiteralPath $executionRoot -File -Recurse -Filter 'pending*.json')
if ($journalPaths.Count -ne 0) {
    throw 'Native execution contains a pending commit journal.'
}
$rows = @()
foreach ($directory in @(Get-ChildItem -LiteralPath $runRoot -Directory)) {
    $path = Join-Path $directory.FullName 'run.json'
    $run = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    if ($run.id -eq $paused.runId) {
        if ([int]$run.state -ne 3 -or @($run.pendingApprovals).Count -ne 1 -or
            [DateTimeOffset]$run.updatedAtUtc -ne [DateTimeOffset]$paused.updatedAtUtc) {
            throw 'The historical paused approval changed.'
        }
    } elseif ([int]$run.state -notin @(5,6) -or !$run.completedAtUtc -or
        @($run.pendingApprovals | Where-Object { $null -ne $_ }).Count -ne 0) {
        throw 'Native execution contains a new active or unclassified run.'
    }
    $rows += [ordered]@{ RunId = $run.id; State = $run.state; Sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
}
foreach ($file in $paused.files) {
    $path = Join-Path $executionRoot $file.path
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $file.sha256) {
        throw 'A preserved historical approval/session file changed.'
    }
}
if ($rows.Count -lt 121) {
    throw 'Native run inventory is incomplete.'
}
$result = [ordered]@{
    CheckedUtc = [DateTimeOffset]::UtcNow.ToString('O')
    Stage = $Stage
    RunCount = $rows.Count
    TerminalRunCount = @($rows | Where-Object { [int]$_.State -in @(5,6) }).Count
    HistoricalPausedRunId = $paused.runId
    HistoricalPausedApprovalCount = 1
    PreservedFileCount = $paused.files.Count
    AllPreservedFileHashesMatch = $true
    PendingJournalCount = 0
    RunInventory = $rows
}
$result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $proofRoot "native-quiescence-$Stage.json") -Encoding utf8NoBOM
[pscustomobject]$result | Select-Object -ExcludeProperty RunInventory | ConvertTo-Json -Compress
