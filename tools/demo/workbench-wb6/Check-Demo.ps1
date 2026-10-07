#requires -Version 7.4
[CmdletBinding()]
param([Parameter(Mandatory)][string]$CandidatePath)
. (Join-Path $PSScriptRoot 'Demo.Common.ps1')
Write-DemoCheck (Read-DemoCandidate $CandidatePath)
