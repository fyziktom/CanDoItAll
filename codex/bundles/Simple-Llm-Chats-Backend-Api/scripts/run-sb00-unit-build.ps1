$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$transcript = Join-Path $repositoryRoot 'codex\bundles\Simple-Llm-Chats-Backend-Api\proof\SB00\transcripts\01-unit-build-confirmation.txt'
$exitCodeFile = Join-Path $repositoryRoot 'codex\bundles\Simple-Llm-Chats-Backend-Api\proof\SB00\transcripts\01-unit-build-confirmation.exitcode'
$artifactsPath = Join-Path $repositoryRoot 'artifacts\codex\simple-llm-chats\SB00'

Set-Location $repositoryRoot

@(
    "Command: dotnet build ./tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --configuration Release --artifacts-path $artifactsPath /m:1 -nologo"
    "Working directory: $repositoryRoot"
    "Started: $([DateTimeOffset]::Now.ToString('O'))"
) | Set-Content -Path $transcript

& dotnet build ./tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --configuration Release --artifacts-path $artifactsPath /m:1 -nologo 2>&1 |
    Tee-Object -FilePath $transcript -Append

$exitCode = $LASTEXITCODE
"Completed: $([DateTimeOffset]::Now.ToString('O'))" | Add-Content -Path $transcript
"Exit code: $exitCode" | Add-Content -Path $transcript
$exitCode | Set-Content -Path $exitCodeFile
exit $exitCode
