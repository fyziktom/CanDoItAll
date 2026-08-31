[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Unit','Integration','Components')][string]$Suite,
    [Parameter(Mandatory)][ValidatePattern('^[a-z0-9-]+$')][string]$Phase,
    [switch]$Discover,
    [switch]$NoBuild,
    [switch]$AllTests,
    [string]$Filter,
    [int]$ExpectedCount
)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
if ([bool]$AllTests -eq ![string]::IsNullOrWhiteSpace($Filter)) {
    throw 'Choose exactly one explicit AllTests or nonempty Filter scope.'
}
if ($AllTests -and !$NoBuild) {
    throw 'The frozen broad gate requires NoBuild; build ownership must be handed over first.'
}
if ($AllTests -and !$Discover -and $ExpectedCount -le 0) {
    throw 'Broad execution requires the positive count from its completed discovery.'
}
$artifactRoot = Join-Path $repositoryRoot '.artifacts/agent-startup-performance/sb01-tests'
$proofRoot = $PSScriptRoot
$target = if ($AllTests) {
    "tests/Solutions/CanDoItAll.Tests.$Suite.slnx"
} else {
    "tests/$Suite/CanDoItAll.Tests.$Suite/CanDoItAll.Tests.$Suite.csproj"
}
$suffix = if ($Discover) { 'discovery' } else { 'execution' }
$logPath = Join-Path $proofRoot "$Phase-$suffix.log"
$resultRoot = Join-Path $proofRoot "$Phase-results"
if (Test-Path -LiteralPath $logPath) {
    throw 'This phase already has a transcript; use a distinct reviewed phase rather than overwriting evidence.'
}
if ($AllTests -and $Suite -eq 'Integration' -and !$Discover -and !(Test-Path -LiteralPath (Join-Path $proofRoot 'legacy-scenario-before/manifest.json'))) {
    throw 'Preserve the fixed legacy scenario-proof subtree before broad Integration execution.'
}
$lockPath = Join-Path $proofRoot 'runner.lock'
$lock = [IO.File]::Open($lockPath, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
$environmentBefore = [Environment]::GetEnvironmentVariables('Process')
$locationBefore = Get-Location
$exitCode = 1
try {
    Set-Location -LiteralPath $repositoryRoot
    $disabled = [ordered]@{
        CANDOITALL_RUN_LIVE_AGENT_VALIDATION = 'false'
        CANDOITALL_ENABLE_LIVE_OPENAI_SMOKE = 'false'
        CANDOITALL_RUN_LIVE_OLLAMA_VALIDATION = 'false'
        CANDOITALL_RUN_DOCKER_PROOF = '0'
        CANDOITALL_RUN_LIVE_COMFYUI_FLUX_PROOF = '0'
        CANDOITALL_SECRET_SERVICE_INTEGRATION = '0'
        CANDOITALL_KEYCHAIN_INTEGRATION = '0'
        CANDOITALL_REQUIRE_DOCKER_INTEGRATION = '0'
    }
    foreach ($entry in $disabled.GetEnumerator()) {
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
    }
    $clearNames = @(
        'ASPNETCORE_URLS','ASPNETCORE_HTTP_PORTS','ASPNETCORE_HTTPS_PORTS','DOTNET_URLS','URLS',
        'OPENAI_API_KEY','AZURE_OPENAI_API_KEY','ANTHROPIC_API_KEY','GOOGLE_API_KEY','GEMINI_API_KEY',
        'GROQ_API_KEY','MISTRAL_API_KEY','OPENROUTER_API_KEY','DEEPSEEK_API_KEY','HF_TOKEN','HUGGINGFACEHUB_API_TOKEN'
    )
    foreach ($name in $clearNames) {
        [Environment]::SetEnvironmentVariable($name, $null, 'Process')
    }
    foreach ($name in @($environmentBefore.Keys)) {
        if ($name -match '^(Database|ControlPlane|Storage|SecretVault|DataProtection|DevelopmentManager)__') {
            [Environment]::SetEnvironmentVariable($name, $null, 'Process')
        }
    }
    [Environment]::SetEnvironmentVariable('CANDOITALL_TEST_CONFIGURATION', 'Release', 'Process')
    [Environment]::SetEnvironmentVariable('CANDOITALL_TEST_REPOSITORY_ROOT', $repositoryRoot, 'Process')
    $extraProofRoot = Join-Path $proofRoot "$Phase-extra-proof"
    foreach ($entry in @{
        CANDOITALL_LLMCHAT_RETENTION_QUERY_PLAN_DIR = 'llmchat-query-plans'
        CANDOITALL_Scenario05_QUERY_PLAN_DIR = 'process-query-plans'
        CANDOITALL_LIVE_COMFYUI_FLUX_PROOF_DIR = 'disabled-comfyui'
    }.GetEnumerator()) {
        [Environment]::SetEnvironmentVariable($entry.Key, (Join-Path $extraProofRoot $entry.Value), 'Process')
    }
    . (Join-Path $repositoryRoot '.artifacts/agent-startup-performance/test-postgres/Enter-IsolatedPostgresTestEnvironment.ps1') | Out-Null
    if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable('CANDOITALL_TESTS_POSTGRES_CONNECTION'))) {
        throw 'The isolated PostgreSQL bootstrap did not set a connection override.'
    }
    $arguments = @('test', $target, '--configuration', 'Release', '--artifacts-path', $artifactRoot, '--verbosity', 'quiet')
    if ($Filter) {
        $arguments += @('--filter', $Filter)
    }
    if ($Discover) {
        $arguments += '--list-tests'
    }
    if ($NoBuild -or !$Discover) {
        $arguments += @('--no-build','--no-restore')
    }
    if (!$Discover) {
        $arguments += @('--logger', 'trx;LogFileName=selected.trx', '--results-directory', $resultRoot)
    }
    $startedUtc = [DateTimeOffset]::UtcNow
    & dotnet @arguments *> $logPath
    $exitCode = $LASTEXITCODE
    $completedUtc = [DateTimeOffset]::UtcNow
    [ordered]@{
        Executable = 'dotnet'
        Arguments = $arguments
        WorkingDirectory = $repositoryRoot
        StartedUtc = $startedUtc
        CompletedUtc = $completedUtc
        ElapsedSeconds = ($completedUtc - $startedUtc).TotalSeconds
        ExitCode = $exitCode
        Scope = if ($AllTests) { 'AllSuppliedSuites workspace' } else { 'Focused selector' }
        ExpectedCount = $ExpectedCount
        DatabaseEndpoint = '127.0.0.1:52049; owned container identity checked'
        DisabledOptIns = @($disabled.Keys)
        ClearedCredentialNames = @($clearNames | Where-Object { $_ -match 'KEY|TOKEN' })
        Configuration = 'Release'
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath ($logPath + '.command.json') -Encoding utf8NoBOM
    if ($Discover) {
        $count = @(Get-Content -LiteralPath $logPath | Where-Object { $_ -match '^\s+CanDoItAll\.Tests\.' }).Count
        $summary = [ordered]@{ Discovered = $count; ExitCode = $exitCode }
    } else {
        $trxPath = Join-Path $resultRoot 'selected.trx'
        if (!(Test-Path -LiteralPath $trxPath)) {
            throw "The test process exited $exitCode without a TRX result; see the retained transcript."
        }
        [xml]$trx = Get-Content -LiteralPath $trxPath
        $counter = $trx.TestRun.ResultSummary.Counters
        $count = [int]$counter.total
        $summary = [ordered]@{
            Total = $count
            Executed = [int]$counter.executed
            Passed = [int]$counter.passed
            Failed = [int]$counter.failed
            NotExecuted = [int]$counter.notExecuted
            ExitCode = $exitCode
            NonPassing = @($trx.TestRun.Results.UnitTestResult | Where-Object { $_.outcome -ne 'Passed' } | ForEach-Object {
                [ordered]@{ Name = $_.testName; Outcome = $_.outcome }
            })
        }
    }
    $summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath ($logPath + '.summary.json') -Encoding utf8NoBOM
    Write-Output ($summary | ConvertTo-Json -Depth 8 -Compress)
    if ($count -eq 0 -or ($ExpectedCount -gt 0 -and $count -ne $ExpectedCount)) {
        throw "Unexpected runtime case count: $count; expected $ExpectedCount. Transcript/TRX are retained."
    }
    if ($exitCode -ne 0) {
        throw "Test process exited $exitCode; failure and skip counts were retained."
    }
} finally {
    Set-Location -LiteralPath $locationBefore.Path
    foreach ($name in @([Environment]::GetEnvironmentVariables('Process').Keys)) {
        if (!$environmentBefore.Contains($name)) {
            [Environment]::SetEnvironmentVariable($name, $null, 'Process')
        }
    }
    foreach ($name in $environmentBefore.Keys) {
        [Environment]::SetEnvironmentVariable($name, [string]$environmentBefore[$name], 'Process')
    }
    $lock.Dispose()
}