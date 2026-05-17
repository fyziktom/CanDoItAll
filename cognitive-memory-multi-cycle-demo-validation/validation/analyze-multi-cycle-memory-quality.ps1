param(
    [string]$RunId = "",
    [string]$RecallEvidenceDirectory = ""
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$bundleRoot = Resolve-Path (Join-Path $scriptRoot "..")
$evidenceRoot = Join-Path $bundleRoot "validation\evidence"

function Resolve-RunId {
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

function Resolve-RecallEvidenceDirectory {
    if (-not [string]::IsNullOrWhiteSpace($RecallEvidenceDirectory)) {
        return (Resolve-Path $RecallEvidenceDirectory).Path
    }

    $latest = Get-ChildItem $evidenceRoot -Directory |
        Where-Object { Test-Path (Join-Path $_.FullName "post-repair-recall-summary.json") } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if ($null -eq $latest) {
        return $null
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

function Get-RecallLocators {
    param([object]$Recall)

    $refs = @()
    if ($null -ne $Recall.contextPack.sourceRefs) {
        $refs += @($Recall.contextPack.sourceRefs)
    }

    foreach ($section in @($Recall.contextPack.sections)) {
        if ($null -ne $section.sourceRefs) {
            $refs += @($section.sourceRefs)
        }
    }

    return @($refs | Where-Object { $null -ne $_.locator } | ForEach-Object { [string]$_.locator } | Sort-Object -Unique)
}

$resolvedRunId = Resolve-RunId
$runDirectory = Join-Path $evidenceRoot $resolvedRunId
$summary = Get-Content (Join-Path $runDirectory "99-run-summary.json") -Raw | ConvertFrom-Json
$manifest = Get-Content (Join-Path $bundleRoot "sample-data\source-manifest.json") -Raw | ConvertFrom-Json
$recallDir = Resolve-RecallEvidenceDirectory
$postRepairSummary = $null
if (-not [string]::IsNullOrWhiteSpace($recallDir)) {
    $postRepairSummary = Get-Content (Join-Path $recallDir "post-repair-recall-summary.json") -Raw | ConvertFrom-Json
}

$originalRecallRows = @()
foreach ($stage in @($summary.stages)) {
    foreach ($cycle in @($stage.projectCycles)) {
        $locators = Get-RecallLocators $cycle.recall
        $stageLocator = "$($cycle.projectKey)-$($stage.stageId.ToLowerInvariant()).md"
        $originalRecallRows += [pscustomobject][ordered]@{
            stageId = $stage.stageId
            projectKey = $cycle.projectKey
            projectId = $cycle.projectId
            sectionCount = @($cycle.recall.contextPack.sections).Count
            matchedExpectedStageLocator = @($locators | Where-Object { $_ -like "*$stageLocator*" }).Count -gt 0
            locatorCount = $locators.Count
            contextSummary = $cycle.recall.contextPack.summary
            locators = $locators
        }
    }
}

$decisionRows = @()
foreach ($stage in @($summary.stages)) {
    foreach ($cycle in @($stage.projectCycles)) {
        foreach ($decision in @($cycle.decisions)) {
            $decisionRows += $decision
        }
    }
}

$approvedExternalByLocator = @($decisionRows |
    Where-Object { $_.decisionKind -eq "Approve" -and $_.sourceSystem -eq "ExternalFile" } |
    Group-Object { ([string]$_.sourceLocator) -replace "#.*", "" } |
    Sort-Object Name |
    ForEach-Object {
        [ordered]@{
            locator = $_.Name
            approvals = $_.Count
        }
    })

$rejectedDuplicateAnchors = @($decisionRows |
    Where-Object { $_.decisionKind -eq "Reject" -and $_.category -eq "duplicate-stage-anchor" })

$postRepairRows = @()
if ($null -ne $postRepairSummary) {
    $postRepairRows = @($postRepairSummary.probes | ForEach-Object {
        [pscustomobject][ordered]@{
            sourceId = $_.sourceId
            projectKey = $_.projectKey
            projectId = $_.projectId
            stageId = $_.stageId
            traceId = $_.traceId
            sectionCount = $_.sectionCount
            sourceReferenceCount = $_.sourceReferenceCount
            expectedStageLocator = $_.expectedStageLocator
            matchedExpectedStageLocator = $_.matchedExpectedStageLocator
            crossProjectLocatorCount = $_.crossProjectLocatorCount
            contextSummary = $_.contextSummary
            locators = $_.locators
        }
    })
}

$finalSnapshotSummary = $summary.finalSnapshot.summary
if ($null -eq $finalSnapshotSummary -and $null -ne $summary.finalSnapshot.response) {
    $finalSnapshotSummary = $summary.finalSnapshot.response.summary
}

$findings = @()
$originalWithContext = @($originalRecallRows | Where-Object { $_.sectionCount -gt 0 }).Count
if ($originalWithContext -lt $originalRecallRows.Count) {
    $findings += [ordered]@{
        severity = "High"
        status = "Repaired"
        title = "Original recall activation missed later-stage project memories."
        evidence = "$originalWithContext of $($originalRecallRows.Count) original cycle recall probes returned context before the recall repair."
    }
}

if ($null -ne $postRepairSummary -and $postRepairSummary.probesWithContext -eq $postRepairSummary.totalProbes) {
    $findings += [ordered]@{
        severity = "Info"
        status = "Passed"
        title = "Post-repair recall returned context for every staged source question."
        evidence = "$($postRepairSummary.probesWithContext) of $($postRepairSummary.totalProbes) post-repair recall probes returned context."
    }
}

if ($null -ne $postRepairSummary -and $postRepairSummary.probesWithExpectedStageLocator -lt $postRepairSummary.totalProbes) {
    $findings += [ordered]@{
        severity = "Medium"
        status = "Open"
        title = "Some post-repair recalls did not include the exact stage source locator."
        evidence = "$($postRepairSummary.probesWithExpectedStageLocator) of $($postRepairSummary.totalProbes) probes cited the exact expected stage file."
    }
}

if ($null -ne $postRepairSummary -and $postRepairSummary.probesWithCrossProjectLocators -gt 0) {
    $findings += [ordered]@{
        severity = "High"
        status = "Open"
        title = "Cross-project source locator leakage was detected."
        evidence = "$($postRepairSummary.probesWithCrossProjectLocators) probes included source locators outside the requested project key."
    }
}

$analysis = [ordered]@{
    runId = $resolvedRunId
    generatedAtUtc = [DateTimeOffset]::UtcNow
    sourceManifestCount = @($manifest.sources).Count
    projectCount = @($summary.projects).Count
    stageCount = @($summary.stages).Count
    cycleCount = $originalRecallRows.Count
    finalSnapshotSummary = $finalSnapshotSummary
    reviewDecisionCounts = @($decisionRows | Group-Object decisionKind | Sort-Object Name | ForEach-Object {
        [ordered]@{
            decisionKind = $_.Name
            count = $_.Count
        }
    })
    reviewDecisionCountsBySource = @($decisionRows | Group-Object sourceSystem, decisionKind | Sort-Object Name | ForEach-Object {
        [ordered]@{
            sourceAndDecision = $_.Name
            count = $_.Count
        }
    })
    approvedExternalLocatorCount = $approvedExternalByLocator.Count
    approvedExternalByLocator = $approvedExternalByLocator
    rejectedDuplicateAnchorCount = $rejectedDuplicateAnchors.Count
    originalRecallCoverage = [ordered]@{
        total = $originalRecallRows.Count
        withContext = $originalWithContext
        withExpectedStageLocator = @($originalRecallRows | Where-Object { $_.matchedExpectedStageLocator }).Count
        byStage = @($originalRecallRows | Group-Object stageId | Sort-Object Name | ForEach-Object {
            [ordered]@{
                stageId = $_.Name
                total = $_.Count
                withContext = @($_.Group | Where-Object { $_.sectionCount -gt 0 }).Count
                withExpectedStageLocator = @($_.Group | Where-Object { $_.matchedExpectedStageLocator }).Count
            }
        })
        rows = $originalRecallRows
    }
    postRepairRecallEvidenceDirectory = $recallDir
    postRepairRecallCoverage = if ($null -eq $postRepairSummary) {
        $null
    } else {
        [ordered]@{
            total = $postRepairSummary.totalProbes
            withContext = $postRepairSummary.probesWithContext
            withExpectedStageLocator = $postRepairSummary.probesWithExpectedStageLocator
            probesWithCrossProjectLocators = $postRepairSummary.probesWithCrossProjectLocators
            byStage = $postRepairSummary.byStage
            rows = $postRepairRows
        }
    }
    findings = $findings
}

$outPath = Join-Path $runDirectory "95-memory-quality-analysis.json"
Save-Json $outPath $analysis
$analysis | ConvertTo-Json -Depth 8
