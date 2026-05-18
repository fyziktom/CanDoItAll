param(
    [string]$BaseUrl = "http://localhost:5032",
    [string]$RunId = "20260517-181521",
    [string]$AgentId = "9fc87ec2-e918-d756-b6c9-b42b8eecbe6e"
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$bundleRoot = Resolve-Path (Join-Path $scriptRoot "..")
$evidenceRoot = Join-Path $bundleRoot "validation\evidence"

function Save-Json {
    param(
        [string]$Path,
        [object]$Value
    )

    $Value | ConvertTo-Json -Depth 40 | Set-Content -Path $Path -Encoding UTF8
}

function Invoke-JsonPost {
    param(
        [string]$Uri,
        [object]$Body
    )

    Invoke-RestMethod -Method Post -Uri $Uri -ContentType "application/json" -Body ($Body | ConvertTo-Json -Depth 20)
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$outDir = Join-Path $evidenceRoot "$RunId-agent-chat-project-marker-$timestamp"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null

$databaseSelection = Invoke-RestMethod -Uri "$BaseUrl/api/cognitive-memory/database/selection"
if (-not $databaseSelection.isPostgreSql) {
    throw "Project-marker chat validation requires PostgreSQL, but active provider is '$($databaseSelection.providerKindName)'."
}

$cases = @(
    [pscustomobject]@{
        sourceId = "clinicflow-saas-S04"
        projectKey = "clinicflow-saas"
        projectId = "5128a19c-2c76-4ea6-9458-349616e2c383"
        expectedStageLocator = "clinicflow-saas-s04.md"
        stageSourcePattern = "clinicflow-saas-s04(?:-[0-9a-f]+)?\.md"
        question = "Which ClinicFlow instruction should be remembered for future product positioning, and what phrase must not be overgeneralized?"
        requiredSemanticPatterns = @("clinical[- ]prioritization|clinical prioritization", "administrative waitlist|staff")
    },
    [pscustomobject]@{
        sourceId = "docker-platform-S04"
        projectKey = "docker-platform"
        projectId = "5eef3db8-a958-4cea-85b9-670735e515cd"
        expectedStageLocator = "docker-platform-s04.md"
        stageSourcePattern = "docker-platform-s04(?:-[0-9a-f]+)?\.md"
        question = "Which Docker Platform instruction controls future agent-memory development and testing setup?"
        requiredSemanticPatterns = @("PostgreSQL", "agent[-‑ ]memory|cognitive[-‑]memory")
    },
    [pscustomobject]@{
        sourceId = "regional-economy-S04"
        projectKey = "regional-economy"
        projectId = "e342f056-39cc-47fb-8380-a07bfdd43e3f"
        expectedStageLocator = "regional-economy-s04.md"
        stageSourcePattern = "regional-economy-s04(?:-[0-9a-f]+)?\.md"
        question = "Which Regional Economy instruction should guide future analysis summaries?"
        requiredSemanticPatterns = @("observed (facts|indicators)|measured facts", "scenarios")
    }
)

$results = @()
foreach ($case in $cases) {
    $session = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/agents/$AgentId/chat-sessions"
    $prompt = @"
CognitiveMemoryProjectId: $($case.projectId)

Answer using CanDoItAll Cognitive Memory context only. If no memory context is available, answer with exactly: NO_MEMORY_CONTEXT.
Return concise JSON with keys: answer, sourceFilename, overgeneralizationRisk, confidence.

Project key: $($case.projectKey)
Question: $($case.question)
"@

    $chat = Invoke-JsonPost -Uri "$BaseUrl/api/agents/$AgentId/chat" -Body @{
        chatSessionId = $session.id
        prompt = $prompt
    }

    $answerText = [string]$chat.assistantMessage.content
    $missingPatterns = @($case.requiredSemanticPatterns | Where-Object { $answerText -notmatch $_ })
    $matchedExpectedLocator = $answerText -match [regex]::Escape($case.expectedStageLocator)
    $matchedStageSource = $answerText -match $case.stageSourcePattern
    $runtimeSnapshot = Invoke-RestMethod -Uri "$BaseUrl/api/agents/$AgentId/runtime-snapshot?chatSessionId=$($chat.chatSessionId)"
    $executionDetail = Invoke-RestMethod -Uri "$BaseUrl/api/agents/execution-runs/$($chat.executionRunId)"
    $serializedSessionState = [string]$executionDetail.run.serializedSessionStateJson
    $contextContainsExpectedLocator = $serializedSessionState -match [regex]::Escape($case.expectedStageLocator)

    $record = [pscustomobject][ordered]@{
        sourceId = $case.sourceId
        projectKey = $case.projectKey
        projectId = $case.projectId
        expectedStageLocator = $case.expectedStageLocator
        chatSessionId = $chat.chatSessionId
        executionRunId = $chat.executionRunId
        answerContainsExpectedLocator = $matchedExpectedLocator
        answerContainsStageSource = $matchedStageSource
        contextContainsExpectedLocator = $contextContainsExpectedLocator
        missingRequiredPatterns = $missingPatterns
        passed = $missingPatterns.Count -eq 0 -and $matchedStageSource -and $contextContainsExpectedLocator
        answer = $answerText
        metric = $chat.metric
        runtimeSnapshot = $runtimeSnapshot
        executionDetail = $executionDetail
    }

    $results += $record
    Save-Json (Join-Path $outDir "$($case.sourceId.ToLowerInvariant())-agent-chat-project-marker.json") $record
}

$summary = [ordered]@{
    baseUrl = $BaseUrl
    runId = $RunId
    agentId = $AgentId
    evidenceDirectory = $outDir
    databaseSelection = $databaseSelection
    generatedAtUtc = [DateTimeOffset]::UtcNow
    totalCases = $results.Count
    passedCases = @($results | Where-Object { $_.passed }).Count
    casesWithExpectedLocatorInAnswer = @($results | Where-Object { $_.answerContainsExpectedLocator }).Count
    casesWithStageSourceInAnswer = @($results | Where-Object { $_.answerContainsStageSource }).Count
    casesWithExpectedLocatorInContext = @($results | Where-Object { $_.contextContainsExpectedLocator }).Count
    cases = $results | Select-Object sourceId, projectKey, projectId, expectedStageLocator, chatSessionId, executionRunId, answerContainsExpectedLocator, answerContainsStageSource, contextContainsExpectedLocator, missingRequiredPatterns, passed, answer, metric
    note = "Prompts contain only CognitiveMemoryProjectId and a question. Project facts and source filenames must therefore come from the Cognitive Memory agent context contributor."
}

Save-Json (Join-Path $outDir "agent-chat-project-marker-validation-summary.json") $summary
$summary | ConvertTo-Json -Depth 12
