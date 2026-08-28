param()
$ErrorActionPreference = 'Stop'
$taskImage = 'sha256:96be062a4d15b1d239ce30d23e5c8eefe9a3c8223cf46da575734804fb7f6cdb'
$taskHosts = @(@{Name='candoitall-spui-shared';Port=5210},@{Name='candoitall-spui-client';Port=5212},@{Name='candoitall-spui-fresh-app-1';Port=5214})
$taskResults = foreach ($taskHost in $taskHosts) {
    $taskInspect = (docker inspect $taskHost.Name | ConvertFrom-Json)[0]
    $taskHealth = Invoke-WebRequest -Uri ('http://127.0.0.1:' + $taskHost.Port + '/health') -TimeoutSec 10
    if ($taskInspect.Image -ne $taskImage -or $taskInspect.State.Status -ne 'running' -or $taskHealth.StatusCode -ne 200 -or $taskHealth.Content -ne 'Healthy') {
        throw ('Unexpected final host state: ' + $taskHost.Name)
    }
    [pscustomobject]@{Name=$taskHost.Name; Port=$taskHost.Port; Image=$taskInspect.Image; Status=$taskInspect.State.Status; Health=$taskHealth.StatusCode; Body=$taskHealth.Content; CapturedAtUtc=[DateTime]::UtcNow.ToString('o'); Volumes=@($taskInspect.Mounts | ForEach-Object { $_.Name })}
}
$taskResults | ConvertTo-Json -Depth 4 | Out-File (Join-Path $PSScriptRoot 'final-health.json')
$taskResults | Format-Table Name,Port,Health,Status
Write-Output 'Same final image, all three hosts Healthy; no mutation or credential read performed.'
