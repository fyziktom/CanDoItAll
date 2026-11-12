#requires -Version 7.4
[CmdletBinding()]
param([Parameter(Mandatory)][string]$CandidatePath)
. (Join-Path $PSScriptRoot 'Demo.Common.ps1')
$candidate = Read-DemoCandidate $CandidatePath
$containers = @(Get-DemoContainers $candidate)
foreach ($service in @('app', 'db')) {
    foreach ($container in @($containers | Where-Object { $_.Config.Labels.'com.docker.compose.service' -ceq $service -and $_.State.Running })) {
        Invoke-DemoDocker @('stop', '--timeout', '60', $container.Id)
    }
}
Write-Output 'Owned demo containers stopped. Database, files, keys and accepted identities are preserved.'
