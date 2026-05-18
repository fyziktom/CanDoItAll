param(
    [string]$BaseUrl = "http://localhost:5032",
    [string]$SourceRunId = "",
    [string]$EvidenceDirectory = ""
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$bundleRoot = Resolve-Path (Join-Path $scriptRoot "..")
if ([string]::IsNullOrWhiteSpace($EvidenceDirectory)) {
    $EvidenceDirectory = Join-Path $scriptRoot "evidence"
}

function Resolve-SourceRunId {
    if (-not [string]::IsNullOrWhiteSpace($SourceRunId)) {
        return $SourceRunId
    }

    $latest = Get-ChildItem -LiteralPath $EvidenceDirectory -Directory |
        Where-Object { Test-Path (Join-Path $_.FullName "99-run-summary.json") } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if ($null -eq $latest) {
        throw "No source run summary was found under '$EvidenceDirectory'."
    }

    return $latest.Name
}

function Save-Json {
    param(
        [string]$Path,
        [object]$Value
    )

    $Value | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $Path -Encoding UTF8
}

function Get-ObjectIdValue {
    param([object]$Value)

    if ($null -eq $Value) {
        return ""
    }

    $valueProperty = $Value.PSObject.Properties["value"]
    if ($null -ne $valueProperty) {
        return "$($valueProperty.Value)"
    }

    return "$Value"
}

function Invoke-CdiaJson {
    param(
        [string]$Method,
        [string]$Path,
        [object]$Body = $null,
        [string]$EvidenceName = ""
    )

    $uri = "$BaseUrl$Path"
    $arguments = @{
        Method = $Method
        Uri = $uri
        ContentType = "application/json"
        TimeoutSec = 180
    }

    if ($null -ne $Body) {
        $arguments["Body"] = ($Body | ConvertTo-Json -Depth 40)
    }

    try {
        $response = Invoke-RestMethod @arguments
        if (-not [string]::IsNullOrWhiteSpace($EvidenceName)) {
            Save-Json (Join-Path $runEvidenceDirectory "$EvidenceName.json") @{
                ok = $true
                method = $Method
                uri = $uri
                request = $Body
                response = $response
            }
        }

        return $response
    }
    catch {
        $failure = [pscustomobject]@{
            ok = $false
            method = $Method
            uri = $uri
            request = $Body
            error = $_.Exception.Message
            details = $_.ErrorDetails.Message
        }

        if (-not [string]::IsNullOrWhiteSpace($EvidenceName)) {
            Save-Json (Join-Path $runEvidenceDirectory "$EvidenceName.json") $failure
        }

        return $failure
    }
}

$resolvedSourceRunId = Resolve-SourceRunId
$sourceRunDirectory = Join-Path $EvidenceDirectory $resolvedSourceRunId
$sourceSummary = Get-Content -LiteralPath (Join-Path $sourceRunDirectory "99-run-summary.json") -Raw | ConvertFrom-Json
$manifest = Get-Content -LiteralPath (Join-Path $bundleRoot "source-truth\source-manifest.json") -Raw | ConvertFrom-Json
$runId = "$resolvedSourceRunId-post-repair-recall-$(Get-Date -Format "yyyyMMdd-HHmmss")"
$runEvidenceDirectory = Join-Path $EvidenceDirectory $runId
New-Item -ItemType Directory -Path $runEvidenceDirectory -Force | Out-Null

$projectsByKey = @{}
foreach ($project in @($sourceSummary.projects)) {
    $projectsByKey[$project.key] = $project
}

$status = Invoke-CdiaJson "GET" "/api/cognitive-memory/status" $null "00-cognitive-memory-status"
$recalls = @()
foreach ($probe in @($manifest.recallProbes)) {
    if (-not $projectsByKey.ContainsKey($probe.projectKey)) {
        throw "Project key '$($probe.projectKey)' was not found in source run '$resolvedSourceRunId'."
    }

    $project = $projectsByKey[$probe.projectKey]
    $body = @{
        projectId = $project.projectId
        query = $probe.question
        intent = "Architecture"
        mode = "FocusedTaskContext"
        policy = @{
            actorId = "codex:realistic-project-memory-validation-post-repair"
            accessLevel = "Restricted"
            policyProfileId = "developer-api"
            riskLevel = "Low"
            allowRestrictedContent = $true
        }
        budget = @{
            coarseCandidateLimit = 96
            graphExpansionDepth = 2
            vectorResultLimit = 16
            focusLimit = 28
            detailItemLimit = 28
            contextCharacterBudget = 48000
            maxSourceBytes = 100000
        }
        metadata = @{
            sourceRunId = $resolvedSourceRunId
            validationKind = "post-repair-recall"
            projectKey = $probe.projectKey
            stageId = $probe.stageId
            probeId = $probe.id
        }
    }

    $recall = Invoke-CdiaJson "POST" "/api/cognitive-memory/recall" $body "$($probe.id.ToLowerInvariant())-recall"
    $recalls += [ordered]@{
        probeId = $probe.id
        stageId = $probe.stageId
        projectKey = $probe.projectKey
        projectId = $project.projectId
        question = $probe.question
        requiredTerms = @($probe.requiredTerms)
        ok = if ($null -ne $recall.ok) { $recall.ok } else { $true }
        traceId = Get-ObjectIdValue $recall.traceId
        includedRecordCount = $recall.includedRecordCount
        selectedClaimCount = $recall.selectedClaimCount
        contextPack = $recall.contextPack
        error = $recall.error
    }
}

$summary = [ordered]@{
    baseUrl = $BaseUrl
    runId = $runId
    sourceRunId = $resolvedSourceRunId
    evidenceDirectory = $runEvidenceDirectory
    finalStatus = $status
    projects = $sourceSummary.projects
    stages = @($manifest.stageOrder | ForEach-Object { [ordered]@{ stageId = $_ } })
    reviewDecisions = @()
    recallProbes = $recalls
}

Save-Json (Join-Path $runEvidenceDirectory "99-run-summary.json") $summary
[ordered]@{
    runId = $runId
    sourceRunId = $resolvedSourceRunId
    evidenceDirectory = $runEvidenceDirectory
    recallProbeCount = $recalls.Count
    providerKindName = $status.providerKindName
} | ConvertTo-Json -Depth 8
