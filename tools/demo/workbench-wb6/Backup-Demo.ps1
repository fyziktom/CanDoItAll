#requires -Version 7.4
[CmdletBinding()]
param([Parameter(Mandatory)][string]$CandidatePath, [Parameter(Mandatory)][string]$SnapshotPath)
. (Join-Path $PSScriptRoot 'Demo.Common.ps1')
$candidate = Read-DemoCandidate $CandidatePath
$null = Write-DemoCheck $candidate
$snapshotRoot = [IO.Path]::GetFullPath($SnapshotPath)
if (Test-Path -LiteralPath $snapshotRoot) {
    throw 'The snapshot target already exists. Preserve it and choose a new directory.'
}
$containers = @(Get-DemoContainers $candidate)
$app = @($containers | Where-Object { $_.Config.Labels.'com.docker.compose.service' -ceq 'app' })[0]
$db = @($containers | Where-Object { $_.Config.Labels.'com.docker.compose.service' -ceq 'db' })[0]
New-Item -ItemType Directory -Path $snapshotRoot | Out-Null
Invoke-DemoDocker @('stop', '--timeout', '60', $app.Id) | Out-Null
$dump = '/tmp/wb6-' + [Guid]::NewGuid().ToString('N') + '.dump'
Invoke-DemoDocker @('exec', $db.Id, 'pg_dump', '--username', $candidate.databaseUser, '--dbname', $candidate.database, '--format', 'custom', '--file', $dump)
Invoke-DemoDocker @('cp', "$($db.Id):$dump", (Join-Path $snapshotRoot 'database.dump'))
foreach ($entry in @(@{ volume = $candidate.volumes.app; file = 'app-data.tar.gz' }, @{ volume = $candidate.volumes.keys; file = 'key-data.tar.gz' })) {
    Invoke-DemoDocker @('run', '--rm', '--name', ($candidate.project + '-backup-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)), '--label', "io.candoitall.task=$($candidate.task)", '--network', 'none', '--read-only', '--mount', "type=volume,source=$($entry.volume),target=/input,readonly", '--mount', "type=bind,source=$snapshotRoot,target=/backup", '--entrypoint', 'tar', $db.Image, '-czf', "/backup/$($entry.file)", '-C', '/input', '.')
}
Invoke-DemoDocker @('stop', '--timeout', '60', $db.Id) | Out-Null
$receipt = [ordered]@{
    task = $candidate.task
    sourceProject = $candidate.project
    imageId = $candidate.imageId
    sourceCommit = $candidate.sourceCommit
    databaseImageId = $db.Image
    createdUtc = [DateTimeOffset]::UtcNow.ToString('O')
    files = @(Get-ChildItem -LiteralPath $snapshotRoot -File | ForEach-Object {
        [ordered]@{ file = $_.Name; bytes = $_.Length; sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    })
}
$receipt | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $snapshotRoot 'snapshot.json')
Write-Output "Database, storage and key snapshot saved to $snapshotRoot. The demo is stopped; run Start-Demo to reopen it. Original private configuration is still required."
