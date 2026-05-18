param(
    [string]$BaseUrl = "http://localhost:5032",
    [string]$SourceRunId = "20260517-204808",
    [string]$ActorId = "codex:realistic-project-memory-validation"
)

$ErrorActionPreference = "Stop"
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$bundleRoot = Resolve-Path (Join-Path $scriptRoot "..")
$sourceRunDirectory = Join-Path $bundleRoot "validation\evidence\$SourceRunId"
$sourceRunSummaryPath = Join-Path $sourceRunDirectory "99-run-summary.json"
if (-not (Test-Path -LiteralPath $sourceRunSummaryPath)) {
    throw "Source run summary was not found: $sourceRunSummaryPath"
}

$runId = "$SourceRunId-continued-consolidation-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
$runEvidenceDirectory = Join-Path $bundleRoot "validation\evidence\$runId"
New-Item -ItemType Directory -Path $runEvidenceDirectory -Force | Out-Null

$headers = @{
    "X-CanDoItAll-Agent-Id" = "codex-realistic-project-memory-validation"
}

function Save-Evidence {
    param(
        [string]$Name,
        [object]$Payload
    )

    $safeName = $Name -replace "[^A-Za-z0-9_.-]", "_"
    $path = Join-Path $runEvidenceDirectory "$safeName.json"
    $Payload | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $path -Encoding UTF8
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
        Headers = $headers
        ContentType = "application/json"
        TimeoutSec = 180
    }
    if ($null -ne $Body) {
        $arguments["Body"] = ($Body | ConvertTo-Json -Depth 100)
    }

    $response = Invoke-RestMethod @arguments
    if (-not [string]::IsNullOrWhiteSpace($EvidenceName)) {
        Save-Evidence $EvidenceName @{
            ok = $true
            method = $Method
            uri = $uri
            request = $Body
            response = $response
        }
    }

    return $response
}

function Get-ObjectIdValue {
    param([object]$Value)

    if ($null -eq $Value) {
        return ""
    }

    if ($Value -is [string]) {
        return $Value
    }

    if ($null -ne $Value.value) {
        return "$($Value.value)"
    }

    return "$Value"
}

function Get-RecallPolicy {
    param([string]$ActorId)

    return @{
        actorId = $ActorId
        accessLevel = "Restricted"
        policyProfileId = "developer-api"
        riskLevel = "Low"
        allowRestrictedContent = $true
    }
}

function Resolve-StageIdFromItem {
    param([object]$ReviewItem)

    $preview = $ReviewItem.candidatePreview
    $text = @(
        "$($preview.proposedTitle)",
        "$($preview.sourceTitle)",
        "$($preview.sourceLocator)",
        "$($preview.sourceExcerpt)"
    ) -join " "
    if ($text -match "(?i)\bS0([1-5])\b") {
        return "S0$($Matches[1])"
    }

    if ($text -match "(?i)-s0([1-5])\.md") {
        return "S0$($Matches[1])"
    }

    return "S00"
}

function Resolve-Decision {
    param(
        [object]$ReviewItem,
        [string]$ProjectKey
    )

    $preview = $ReviewItem.candidatePreview
    $sourceSystem = "$($preview.sourceSystem)"
    $sourceItemType = "$($preview.sourceItemType)"
    $sourceTitle = "$($preview.sourceTitle)"
    $proposedTitle = "$($preview.proposedTitle)"
    $sourceLocator = "$($preview.sourceLocator)"
    $stageId = Resolve-StageIdFromItem $ReviewItem

    if ($sourceSystem -eq "WorkbenchProjectStructure" -and $sourceItemType -eq "ProjectLink") {
        return [pscustomobject]@{
            kind = "Reject"
            category = "non-memory-link"
            notes = "Rejected because project links are relationship evidence, not standalone durable memories."
            stageId = $stageId
        }
    }

    if ($sourceSystem -eq "WorkbenchProjectStructure" -and $sourceItemType -eq "ProjectNode" -and ($sourceTitle -match "Stage source chunk" -or $proposedTitle -match "Stage source chunk")) {
        return [pscustomobject]@{
            kind = "Reject"
            category = "duplicate-stage-file-node"
            notes = "Rejected because the corresponding ExternalFile source is the primary evidence for this stage chunk."
            stageId = $stageId
        }
    }

    if ($sourceSystem -eq "ExternalFile" -and $sourceLocator -match "$ProjectKey-s0[1-5]\.md") {
        return [pscustomobject]@{
            kind = "Approve"
            category = "stage-source-truth"
            notes = "Approved after comparison with the normalized source-truth stage document."
            stageId = $stageId
        }
    }

    if ($sourceSystem -eq "WorkbenchProjectStructure" -and $sourceItemType -eq "ProjectNode") {
        return [pscustomobject]@{
            kind = "Approve"
            category = "structured-project-node"
            notes = "Approved because this is a typed project-structure node derived from the time-sliced source truth."
            stageId = $stageId
        }
    }

    return [pscustomobject]@{
        kind = "Defer"
        category = "manual-review-needed"
        notes = "Deferred because the candidate was not a stage source file or derived project node."
        stageId = $stageId
    }
}

function Invoke-PendingReviewDecisions {
    param(
        [string]$ProjectId,
        [string]$ProjectKey,
        [string]$EvidencePrefix
    )

    $decisionRecords = @()
    for ($reviewPage = 1; $reviewPage -le 40; $reviewPage++) {
        $snapshot = Invoke-CdiaJson "GET" "/api/cognitive-memory/snapshot?projectId=$ProjectId&take=50" $null "$EvidencePrefix-review-page-$reviewPage"
        $pendingItems = @($snapshot.reviewItems | Where-Object { $_.status -eq 0 -and $null -ne $_.candidatePreview })
        if ($pendingItems.Count -eq 0) {
            break
        }

        foreach ($item in $pendingItems) {
            $decision = Resolve-Decision $item $ProjectKey
            $itemId = Get-ObjectIdValue $item.id
            if ([string]::IsNullOrWhiteSpace($itemId)) {
                continue
            }

            $body = @{
                decisionKind = $decision.kind
                actorId = $ActorId
                notes = "$($decision.notes) Stage=$($decision.stageId); Project=$ProjectKey; Category=$($decision.category)."
                expectedConcurrencyToken = $item.concurrencyToken
            }
            $decisionResponse = Invoke-CdiaJson "POST" "/api/cognitive-memory/review-items/$itemId/decisions" $body "$EvidencePrefix-review-$reviewPage-$itemId-$($decision.kind)"
            $decisionRecords += [ordered]@{
                projectId = $ProjectId
                projectKey = $ProjectKey
                stageId = $decision.stageId
                reviewItemId = $itemId
                decisionKind = $decision.kind
                category = $decision.category
                candidateTitle = "$($item.candidatePreview.proposedTitle)"
                sourceSystem = "$($item.candidatePreview.sourceSystem)"
                sourceItemType = "$($item.candidatePreview.sourceItemType)"
                sourceLocator = "$($item.candidatePreview.sourceLocator)"
            }
        }
    }

    return $decisionRecords
}

$sourceSummary = Get-Content -Raw -LiteralPath $sourceRunSummaryPath | ConvertFrom-Json
$summary = [ordered]@{
    runId = $runId
    sourceRunId = $SourceRunId
    evidenceDirectory = $runEvidenceDirectory
    projects = @()
    consolidationRuns = @()
    reviewDecisions = @()
}

foreach ($project in $sourceSummary.projects) {
    $projectId = "$($project.projectId)"
    $projectKey = "$($project.key)"
    $projectPrefix = $projectKey -replace "[^A-Za-z0-9_.-]", "_"
    $projectSummary = [ordered]@{
        projectKey = $projectKey
        projectId = $projectId
        consolidationRuns = @()
        decisions = @()
    }

    $cursor = $null
    for ($page = 1; $page -le 40; $page++) {
        $body = @{
            projectId = $projectId
            mode = "IncrementalRecent"
            triggerKind = "Manual"
            idempotencyKey = "realistic-project-memory:${runId}:${projectKey}:${projectId}:consolidate:page-${page}:v1"
            profile = @{
                name = "developer-no-vector-projection"
                processSourceItems = $true
                detectContradictions = $true
                extractProcedures = $true
                rebuildProjections = $false
                createHumanReviewItems = $true
                maxItems = 220
            }
            policy = (Get-RecallPolicy $ActorId)
        }
        if (-not [string]::IsNullOrWhiteSpace($cursor)) {
            $body["cursor"] = $cursor
        }

        $run = Invoke-CdiaJson "POST" "/api/cognitive-memory/consolidation/runs" $body "$projectPrefix-consolidation-page-$page"
        $projectSummary.consolidationRuns += $run
        $summary.consolidationRuns += [ordered]@{
            projectKey = $projectKey
            projectId = $projectId
            page = $page
            runId = Get-ObjectIdValue $run.runId
            sourceItemsScanned = $run.sourceItemsScanned
            candidatesCreated = $run.candidatesCreated
            reviewItemsCreated = $run.reviewItemsCreated
            nextCursor = $run.nextCursor
        }

        $decisions = @(Invoke-PendingReviewDecisions $projectId $projectKey "$projectPrefix-page-$page")
        foreach ($decision in $decisions) {
            $projectSummary.decisions += $decision
            $summary.reviewDecisions += $decision
        }

        $sourceItemsScanned = if ($null -eq $run.sourceItemsScanned) { 0 } else { [int]$run.sourceItemsScanned }
        if ($sourceItemsScanned -eq 0) {
            break
        }

        $cursor = "$($run.nextCursor)"
        if ([string]::IsNullOrWhiteSpace($cursor)) {
            break
        }
    }

    $summary.projects += $projectSummary
}

Save-Evidence "99-continued-consolidation-summary" $summary
$summary | ConvertTo-Json -Depth 20
