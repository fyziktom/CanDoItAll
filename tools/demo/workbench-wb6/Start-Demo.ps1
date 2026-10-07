#requires -Version 7.4
[CmdletBinding()]
param([Parameter(Mandatory)][string]$CandidatePath)
. (Join-Path $PSScriptRoot 'Demo.Common.ps1')
$candidate = Read-DemoCandidate $CandidatePath
$containers = @(Get-DemoContainers $candidate)
$runningApp = @($containers | Where-Object { $_.Config.Labels.'com.docker.compose.service' -ceq 'app' -and $_.State.Running })
$listener = @(Get-NetTCPConnection -State Listen -LocalPort $candidate.port -ErrorAction SilentlyContinue)
if ($listener.Count -gt 0) {
    if ($runningApp.Count -ne 1 -or $runningApp[0].Image -cne $candidate.imageId) {
        throw "Port $($candidate.port) is occupied. Stop the owned older candidate explicitly; no process was replaced."
    }
    Write-DemoCheck $candidate
    return
}
Invoke-DemoCompose $candidate @('up', '--detach', '--no-build', '--pull', 'never', '--wait', '--wait-timeout', '300')
Write-DemoCheck $candidate
