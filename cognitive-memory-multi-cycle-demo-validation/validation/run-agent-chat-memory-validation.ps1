param(
    [string]$BaseUrl = "http://localhost:5032",
    [string]$RunId = "20260517-181521",
    [string]$RecallEvidenceDirectory = "",
    [string]$AgentId = "9fc87ec2-e918-d756-b6c9-b42b8eecbe6e"
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$bundleRoot = Resolve-Path (Join-Path $scriptRoot "..")
$evidenceRoot = Join-Path $bundleRoot "validation\evidence"

function Resolve-RecallEvidenceDirectory {
    if (-not [string]::IsNullOrWhiteSpace($RecallEvidenceDirectory)) {
        return (Resolve-Path $RecallEvidenceDirectory).Path
    }

    $latest = Get-ChildItem $evidenceRoot -Directory |
        Where-Object { Test-Path (Join-Path $_.FullName "post-repair-recall-summary.json") } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if ($null -eq $latest) {
        throw "No post-repair recall evidence directory was found under $evidenceRoot."
    }

    return $latest.FullName
}

function Save-Json {
    param(
        [string]$Path,
        [object]$Value
    )

    $Value | ConvertTo-Json -Depth 40 | Set-Content -Path $Path -Encoding UTF8
}

function Shorten {
    param(
        [string]$Text,
        [int]$MaxLength = 1200
    )

    if ([string]::IsNullOrWhiteSpace($Text)) {
        return ""
    }

    $trimmed = $Text.Trim()
    if ($trimmed.Length -le $MaxLength) {
        return $trimmed
    }

    return $trimmed.Substring(0, $MaxLength) + "`n[truncated]"
}

function Get-SectionLocators {
    param([object]$Section)

    return @($Section.sourceRefs |
        Where-Object { $null -ne $_.locator } |
        ForEach-Object { [string]$_.locator } |
        Sort-Object -Unique)
}

function Render-ContextForPrompt {
    param(
        [object]$RecallRecord,
        [string]$ExpectedStageLocator
    )

    $sections = @($RecallRecord.response.contextPack.sections)
    $preferred = @($sections | Where-Object {
        @(Get-SectionLocators $_ | Where-Object { $_ -like "*$ExpectedStageLocator*" }).Count -gt 0
    })
    if ($preferred.Count -lt 3) {
        $preferred += @($sections | Where-Object { $preferred -notcontains $_ } | Select-Object -First (6 - $preferred.Count))
    }

    $blocks = @()
    $index = 1
    foreach ($section in @($preferred | Select-Object -First 6)) {
        $locators = Get-SectionLocators $section
        $blocks += @"
[$index] $($section.title)
Source locators: $($locators -join "; ")
$((Shorten $section.content 1400))
"@
        $index++
    }

    return ($blocks -join "`n`n")
}

$recallDir = Resolve-RecallEvidenceDirectory
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$outDir = Join-Path $evidenceRoot "$RunId-agent-chat-$timestamp"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null

$cases = @(
    [pscustomobject]@{
        sourceId = "clinicflow-saas-S04"
        expectedStageLocator = "clinicflow-saas-s04.md"
        question = "Which ClinicFlow email instruction should affect future product positioning, and what phrase should the agent avoid overgeneralizing?"
        requiredPatterns = @("clinical[- ]prioritization|clinical prioritization", "administrative waitlist|staff")
    },
    [pscustomobject]@{
        sourceId = "docker-platform-S04"
        expectedStageLocator = "docker-platform-s04.md"
        question = "Which Docker Platform email instruction affects future development and testing setup?"
        requiredPatterns = @("PostgreSQL", "agent[- ]memory|cognitive-memory")
    },
    [pscustomobject]@{
        sourceId = "regional-economy-S04"
        expectedStageLocator = "regional-economy-s04.md"
        question = "Which Regional Economy instruction should guide future analysis summaries?"
        requiredPatterns = @("observed (facts|indicators)|measured facts", "scenarios")
    }
)

$results = @()
foreach ($case in $cases) {
    $recallPath = Join-Path $recallDir "$($case.sourceId.ToLowerInvariant())-recall.json"
    if (-not (Test-Path $recallPath)) {
        throw "Recall evidence file '$recallPath' was not found."
    }

    $recall = Get-Content $recallPath -Raw | ConvertFrom-Json
    $context = Render-ContextForPrompt $recall $case.expectedStageLocator
    $prompt = @"
You are validating CanDoItAll Cognitive Memory in an agent chat.
Use only the Cognitive Memory context below. Do not use outside knowledge.
Answer the question in concise JSON with keys: answer, sourceLocators, overgeneralizationRisk, confidence.

Project key: $($recall.projectKey)
Question: $($case.question)

Cognitive Memory context:
$context
"@

    $session = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/agents/$AgentId/chat-sessions"
    $chat = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/agents/$AgentId/chat" -ContentType "application/json" -Body (@{
        chatSessionId = $session.id
        prompt = $prompt
    } | ConvertTo-Json -Depth 8)

    $answerText = [string]$chat.assistantMessage.content
    $missingPatterns = @($case.requiredPatterns | Where-Object { $answerText -notmatch $_ })
    $matchedExpectedLocator = $answerText -match [regex]::Escape($case.expectedStageLocator)
    $record = [pscustomobject][ordered]@{
        sourceId = $case.sourceId
        projectKey = $recall.projectKey
        expectedStageLocator = $case.expectedStageLocator
        chatSessionId = $chat.chatSessionId
        executionRunId = $chat.executionRunId
        answerContainsExpectedLocator = $matchedExpectedLocator
        missingRequiredPatterns = $missingPatterns
        passed = $missingPatterns.Count -eq 0
        answer = $answerText
        metric = $chat.metric
    }

    $results += $record
    Save-Json (Join-Path $outDir "$($case.sourceId.ToLowerInvariant())-agent-chat.json") $record
}

$summary = [ordered]@{
    baseUrl = $BaseUrl
    runId = $RunId
    agentId = $AgentId
    recallEvidenceDirectory = $recallDir
    evidenceDirectory = $outDir
    generatedAtUtc = [DateTimeOffset]::UtcNow
    totalCases = $results.Count
    passedCases = @($results | Where-Object { $_.passed }).Count
    casesWithExpectedLocatorInAnswer = @($results | Where-Object { $_.answerContainsExpectedLocator }).Count
    cases = $results
    note = "The Agents API chat endpoint is organization-scoped; this validation injects Cognitive Memory context packs into chat prompts. Automatic project-scoped Cognitive Memory contribution should be tracked separately."
}

Save-Json (Join-Path $outDir "agent-chat-memory-validation-summary.json") $summary
$summary | ConvertTo-Json -Depth 8
