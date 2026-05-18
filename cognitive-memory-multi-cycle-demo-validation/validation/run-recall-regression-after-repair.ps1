param(
    [string]$BaseUrl = "http://localhost:5032",
    [string]$RunId = "",
    [string]$ActorId = "codex:cognitive-memory-recall-repair"
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$bundleRoot = Resolve-Path (Join-Path $scriptRoot "..")
$evidenceRoot = Join-Path $bundleRoot "validation\evidence"

function Resolve-SourceRunId {
    if (-not [string]::IsNullOrWhiteSpace($RunId)) {
        return $RunId
    }

    $latest = Get-ChildItem $evidenceRoot -Directory |
        Where-Object { Test-Path (Join-Path $_.FullName "99-run-summary.json") } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if ($null -eq $latest) {
        throw "No evidence run with 99-run-summary.json was found under $evidenceRoot."
    }

    return $latest.Name
}

function Save-Json {
    param(
        [string]$Path,
        [object]$Value
    )

    $Value | ConvertTo-Json -Depth 40 | Set-Content -Path $Path -Encoding UTF8
}

function Get-SourceRefs {
    param([object]$ContextPack)

    $refs = @()
    if ($null -ne $ContextPack.sourceRefs) {
        $refs += @($ContextPack.sourceRefs)
    }

    foreach ($section in @($ContextPack.sections)) {
        if ($null -ne $section.sourceRefs) {
            $refs += @($section.sourceRefs)
        }
    }

    return @($refs | Where-Object { $null -ne $_.locator })
}

$sourceRunId = Resolve-SourceRunId
$sourceRunDirectory = Join-Path $evidenceRoot $sourceRunId
$summaryPath = Join-Path $sourceRunDirectory "99-run-summary.json"
$manifestPath = Join-Path $bundleRoot "sample-data\source-manifest.json"
$summary = Get-Content $summaryPath -Raw | ConvertFrom-Json
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$outDir = Join-Path $evidenceRoot "$sourceRunId-post-repair-recall-$timestamp"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null

$projectsByKey = @{}
foreach ($project in @($summary.projects)) {
    $projectsByKey[$project.key] = $project
}

$probeResults = @()
foreach ($source in @($manifest.sources)) {
    if (-not $projectsByKey.ContainsKey($source.projectKey)) {
        throw "Project key '$($source.projectKey)' from source '$($source.sourceId)' was not present in run summary."
    }

    $project = $projectsByKey[$source.projectKey]
    $body = @{
        projectId = $project.projectId
        query = $source.expectedChatQuestion
        intent = "Architecture"
        mode = "FocusedTaskContext"
        policy = @{
            actorId = $ActorId
            accessLevel = "Restricted"
            policyProfileId = "developer-api"
            riskLevel = "Low"
            allowRestrictedContent = $true
        }
        budget = @{
            coarseCandidateLimit = 32
            graphExpansionDepth = 1
            vectorResultLimit = 12
            focusLimit = 10
            detailItemLimit = 10
            contextCharacterBudget = 16000
            maxSourceBytes = 30000
        }
        metadata = @{
            sourceRunId = $sourceRunId
            sourceId = $source.sourceId
            stageId = $source.stageId
            projectKey = $source.projectKey
            validationKind = "post-repair-recall-regression"
        }
    }
    $json = $body | ConvertTo-Json -Depth 20
    $response = Invoke-RestMethod -Uri "$BaseUrl/api/cognitive-memory/recall" -Method Post -ContentType "application/json" -Body $json
    $sourceRefs = Get-SourceRefs $response.contextPack
    $locators = @($sourceRefs | ForEach-Object { [string]$_.locator } | Sort-Object -Unique)
    $stageLocator = "$($source.projectKey)-$($source.stageId.ToLowerInvariant()).md"
    $projectLocatorPrefix = "$($source.projectKey)-"
    $traceId = [string]$response.traceId
    if ($null -ne $response.traceId.value) {
        $traceId = [string]$response.traceId.value
    }

    $record = [ordered]@{
        sourceRunId = $sourceRunId
        sourceId = $source.sourceId
        projectKey = $source.projectKey
        projectId = $project.projectId
        stageId = $source.stageId
        question = $source.expectedChatQuestion
        traceId = $traceId
        sectionCount = @($response.contextPack.sections).Count
        sourceReferenceCount = $locators.Count
        expectedStageLocator = $stageLocator
        matchedExpectedStageLocator = @($locators | Where-Object { $_ -like "*$stageLocator*" }).Count -gt 0
        crossProjectLocatorCount = @($locators | Where-Object { $_ -notlike "$projectLocatorPrefix*" }).Count
        contextSummary = $response.contextPack.summary
        locators = $locators
        response = $response
    }
    $probeResults += [pscustomobject]$record
    Save-Json (Join-Path $outDir "$($source.sourceId.ToLowerInvariant())-recall.json") $record
}

$aggregate = [ordered]@{
    baseUrl = $BaseUrl
    sourceRunId = $sourceRunId
    evidenceDirectory = $outDir
    generatedAtUtc = [DateTimeOffset]::UtcNow
    totalProbes = $probeResults.Count
    probesWithContext = @($probeResults | Where-Object { $_.sectionCount -gt 0 }).Count
    probesWithExpectedStageLocator = @($probeResults | Where-Object { $_.matchedExpectedStageLocator }).Count
    probesWithCrossProjectLocators = @($probeResults | Where-Object { $_.crossProjectLocatorCount -gt 0 }).Count
    byStage = @($probeResults | Group-Object stageId | Sort-Object Name | ForEach-Object {
        [ordered]@{
            stageId = $_.Name
            total = $_.Count
            withContext = @($_.Group | Where-Object { $_.sectionCount -gt 0 }).Count
            withExpectedStageLocator = @($_.Group | Where-Object { $_.matchedExpectedStageLocator }).Count
            crossProjectLocatorCount = @($_.Group | Measure-Object crossProjectLocatorCount -Sum).Sum
        }
    })
    probes = $probeResults
}

Save-Json (Join-Path $outDir "post-repair-recall-summary.json") $aggregate
$aggregate | ConvertTo-Json -Depth 8
