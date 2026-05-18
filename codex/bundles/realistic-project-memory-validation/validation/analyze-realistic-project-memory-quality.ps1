param(
    [string]$RunId = "",
    [string]$EvidenceDirectory = ""
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$bundleRoot = Resolve-Path (Join-Path $scriptRoot "..")
if ([string]::IsNullOrWhiteSpace($EvidenceDirectory)) {
    $EvidenceDirectory = Join-Path $scriptRoot "evidence"
}

function Resolve-RunId {
    if (-not [string]::IsNullOrWhiteSpace($RunId)) {
        return $RunId
    }

    $latest = Get-ChildItem -LiteralPath $EvidenceDirectory -Directory |
        Where-Object { Test-Path (Join-Path $_.FullName "99-run-summary.json") } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if ($null -eq $latest) {
        throw "No run summary was found under '$EvidenceDirectory'."
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

function Save-Markdown {
    param(
        [string]$Path,
        [string[]]$Lines
    )

    ($Lines -join "`r`n") + "`r`n" | Set-Content -LiteralPath $Path -Encoding UTF8
}

function Get-ContextText {
    param([object]$RecallRecord)

    $parts = New-Object System.Collections.Generic.List[string]
    if ($null -eq $RecallRecord -or $null -eq $RecallRecord.contextPack) {
        return ""
    }

    $pack = $RecallRecord.contextPack
    foreach ($propertyName in @("summary", "instructions", "warnings")) {
        $property = $pack.PSObject.Properties[$propertyName]
        if ($null -ne $property -and $null -ne $property.Value) {
            $parts.Add("$($property.Value)")
        }
    }

    foreach ($section in @($pack.sections)) {
        foreach ($propertyName in @("title", "summary", "content", "text")) {
            $property = $section.PSObject.Properties[$propertyName]
            if ($null -ne $property -and $null -ne $property.Value) {
                $parts.Add("$($property.Value)")
            }
        }
    }

    foreach ($ref in Get-SourceRefs $pack) {
        $parts.Add("$($ref.locator)")
    }

    return ($parts -join "`n")
}

function Get-SourceRefs {
    param([object]$ContextPack)

    $refs = @()
    if ($null -eq $ContextPack) {
        return @()
    }

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

function Test-TermMatch {
    param(
        [string]$Text,
        [string]$Term
    )

    if ([string]::IsNullOrWhiteSpace($Text)) {
        return $false
    }

    return $Text.IndexOf($Term, [StringComparison]::OrdinalIgnoreCase) -ge 0
}

$resolvedRunId = Resolve-RunId
$runDirectory = Join-Path $EvidenceDirectory $resolvedRunId
$summary = Get-Content -LiteralPath (Join-Path $runDirectory "99-run-summary.json") -Raw | ConvertFrom-Json
$manifest = Get-Content -LiteralPath (Join-Path $bundleRoot "source-truth\source-manifest.json") -Raw | ConvertFrom-Json

$recallRows = @()
foreach ($recall in @($summary.recallProbes)) {
    $text = Get-ContextText $recall
    $sourceRefs = Get-SourceRefs $recall.contextPack
    $locators = @($sourceRefs | ForEach-Object { "$($_.locator)" } | Sort-Object -Unique)
    $stageLocator = "$($recall.projectKey)-$($recall.stageId.ToLowerInvariant()).md"
    $requiredMatches = @()
    foreach ($term in @($recall.requiredTerms)) {
        $requiredMatches += [ordered]@{
            term = "$term"
            matched = Test-TermMatch $text "$term"
        }
    }

    $recallRows += [pscustomobject][ordered]@{
        probeId = $recall.probeId
        projectKey = $recall.projectKey
        projectId = $recall.projectId
        stageId = $recall.stageId
        ok = $recall.ok
        sectionCount = @($recall.contextPack.sections).Count
        includedRecordCount = $recall.includedRecordCount
        selectedClaimCount = $recall.selectedClaimCount
        expectedStageLocator = $stageLocator
        matchedExpectedStageLocator = @($locators | Where-Object { $_ -like "*$stageLocator*" }).Count -gt 0
        sourceReferenceCount = $locators.Count
        missingTerms = @($requiredMatches | Where-Object { -not $_.matched } | ForEach-Object { $_.term })
        matchedTermCount = @($requiredMatches | Where-Object { $_.matched }).Count
        requiredTermCount = @($requiredMatches).Count
        locators = $locators
        error = $recall.error
    }
}

$findings = @()
$expectedProbeCount = @($manifest.recallProbes).Count
if ($recallRows.Count -ne $expectedProbeCount) {
    $findings += [ordered]@{
        severity = "High"
        status = "Open"
        title = "Recall probe count does not match the manifest."
        evidence = "Expected $expectedProbeCount probes, found $($recallRows.Count)."
    }
}

$emptyRecalls = @($recallRows | Where-Object { $_.sectionCount -eq 0 -or $_.ok -eq $false })
if ($emptyRecalls.Count -gt 0) {
    $findings += [ordered]@{
        severity = "High"
        status = "Open"
        title = "Some recall probes returned no usable context."
        evidence = "$($emptyRecalls.Count) of $($recallRows.Count) probes had no sections or failed."
    }
}

$locatorMisses = @($recallRows | Where-Object { -not $_.matchedExpectedStageLocator })
if ($locatorMisses.Count -gt 0) {
    $findings += [ordered]@{
        severity = "Medium"
        status = "Open"
        title = "Some recalls did not cite the expected stage source locator."
        evidence = "$($locatorMisses.Count) of $($recallRows.Count) probes missed their stage locator."
    }
}

$termMisses = @($recallRows | Where-Object { @($_.missingTerms).Count -gt 0 })
if ($termMisses.Count -gt 0) {
    $findings += [ordered]@{
        severity = "High"
        status = "Open"
        title = "Some recalls missed required source-truth facts."
        evidence = "$($termMisses.Count) of $($recallRows.Count) probes missed one or more required terms."
    }
}

if ($findings.Count -eq 0) {
    $findings += [ordered]@{
        severity = "Info"
        status = "Passed"
        title = "All source-truth recall probes passed."
        evidence = "$($recallRows.Count) probes returned context, cited expected stage locators, and matched required terms."
    }
}

$projectRows = @($summary.projects | ForEach-Object {
    [ordered]@{
        key = $_.key
        projectId = $_.projectId
        stageCount = $_.stageCount
        createdNodeCount = $_.createdNodeCount
        createdLinkCount = $_.createdLinkCount
        uploadedSourceCount = @($_.uploadedSources).Count
    }
})

$decisionRows = @($summary.reviewDecisions | ForEach-Object {
    [ordered]@{
        stageId = $_.stageId
        projectKey = $_.projectKey
        decisionKind = $_.decisionKind
        category = $_.category
        sourceSystem = $_.sourceSystem
        sourceItemType = $_.sourceItemType
        sourceLocator = $_.sourceLocator
    }
})

$analysis = [ordered]@{
    runId = $resolvedRunId
    generatedAtUtc = [DateTimeOffset]::UtcNow
    evidenceDirectory = $runDirectory
    projectCount = @($summary.projects).Count
    stageCount = @($summary.stages).Count
    expectedProbeCount = $expectedProbeCount
    recallProbeCount = $recallRows.Count
    recallsWithContext = @($recallRows | Where-Object { $_.sectionCount -gt 0 }).Count
    recallsWithExpectedStageLocator = @($recallRows | Where-Object { $_.matchedExpectedStageLocator }).Count
    recallsWithAllRequiredTerms = @($recallRows | Where-Object { @($_.missingTerms).Count -eq 0 }).Count
    projectRows = $projectRows
    reviewDecisionCounts = @($decisionRows | Group-Object decisionKind | Sort-Object Name | ForEach-Object {
        [ordered]@{
            decisionKind = $_.Name
            count = $_.Count
        }
    })
    reviewDecisionCountsByCategory = @($decisionRows | Group-Object category | Sort-Object Name | ForEach-Object {
        [ordered]@{
            category = $_.Name
            count = $_.Count
        }
    })
    recallRows = $recallRows
    findings = $findings
}

$jsonPath = Join-Path $runDirectory "95-memory-quality-analysis.json"
Save-Json $jsonPath $analysis

$markdownLines = New-Object System.Collections.Generic.List[string]
$markdownLines.Add("# Realistic Project Memory Quality Analysis")
$markdownLines.Add("")
$markdownLines.Add("- Run: ``$resolvedRunId``")
$markdownLines.Add("- Projects: $($analysis.projectCount)")
$markdownLines.Add("- Stages: $($analysis.stageCount)")
$markdownLines.Add("- Recall probes: $($analysis.recallProbeCount) / $expectedProbeCount")
$markdownLines.Add("- Recalls with context: $($analysis.recallsWithContext)")
$markdownLines.Add("- Recalls with expected stage locator: $($analysis.recallsWithExpectedStageLocator)")
$markdownLines.Add("- Recalls with all required terms: $($analysis.recallsWithAllRequiredTerms)")
$markdownLines.Add("")
$markdownLines.Add("## Findings")
foreach ($finding in $findings) {
    $markdownLines.Add("- [$($finding.severity)] $($finding.status): $($finding.title) $($finding.evidence)")
}
$markdownLines.Add("")
$markdownLines.Add("## Recall Rows")
$markdownLines.Add("| Probe | Project | Stage | Sections | Locator | Missing terms |")
$markdownLines.Add("| --- | --- | --- | ---: | --- | --- |")
foreach ($row in $recallRows) {
    $missing = if (@($row.missingTerms).Count -eq 0) { "none" } else { (@($row.missingTerms) -join ", ") }
    $locator = if ($row.matchedExpectedStageLocator) { "matched" } else { "missing" }
    $markdownLines.Add("| $($row.probeId) | $($row.projectKey) | $($row.stageId) | $($row.sectionCount) | $locator | $missing |")
}

$markdownPath = Join-Path $runDirectory "96-memory-quality-analysis.md"
Save-Markdown $markdownPath $markdownLines

$analysis | ConvertTo-Json -Depth 20
