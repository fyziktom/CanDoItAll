$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$transcriptRoot = Join-Path $repositoryRoot 'codex\bundles\Simple-Llm-Chats-Backend-Api\proof\SB00\transcripts'
$exitCodeFile = Join-Path $transcriptRoot 'focused-tests.exitcode'
$project = './tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj'
$artifactsPath = Join-Path $repositoryRoot 'artifacts\codex\simple-llm-chats\SB00'
$filters = @(
    'LlmConversationServiceTests',
    'FileLlmConversationStoreTests',
    'ProviderBackedLlmInvocationAdapterTests'
)

Set-Location $repositoryRoot
$overallExitCode = 0

for ($index = 0; $index -lt $filters.Count; $index++) {
    $filter = $filters[$index]
    $ordinal = $index + 3
    $transcript = Join-Path $transcriptRoot ("{0:D2}-{1}.txt" -f $ordinal, $filter)

    @(
        "Command: dotnet test $project --configuration Release --artifacts-path $artifactsPath --no-build --no-restore --filter FullyQualifiedName~$filter /m:1 -nologo"
        "Working directory: $repositoryRoot"
        "Started: $([DateTimeOffset]::Now.ToString('O'))"
    ) | Set-Content -Path $transcript

    & dotnet test $project --configuration Release --artifacts-path $artifactsPath --no-build --no-restore --filter "FullyQualifiedName~$filter" /m:1 -nologo 2>&1 |
        Tee-Object -FilePath $transcript -Append

    $exitCode = $LASTEXITCODE
    "Completed: $([DateTimeOffset]::Now.ToString('O'))" | Add-Content -Path $transcript
    "Exit code: $exitCode" | Add-Content -Path $transcript

    if ($exitCode -ne 0) {
        $overallExitCode = $exitCode
        break
    }
}

$overallExitCode | Set-Content -Path $exitCodeFile
exit $overallExitCode
