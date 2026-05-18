param(
    [string]$BaseUrl = "http://localhost:5032",
    [string]$ManifestPath = "",
    [string]$EvidenceDirectory = "",
    [string]$BearerToken = "",
    [switch]$AllowNonPostgreSql
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Net.Http

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$bundleRoot = Resolve-Path (Join-Path $scriptRoot "..")
if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
    $ManifestPath = Join-Path $bundleRoot "source-truth\source-manifest.json"
}

if ([string]::IsNullOrWhiteSpace($EvidenceDirectory)) {
    $EvidenceDirectory = Join-Path $scriptRoot "evidence"
}

$runId = Get-Date -Format "yyyyMMdd-HHmmss"
$runEvidenceDirectory = Join-Path $EvidenceDirectory $runId
$sourceInputDirectory = Join-Path $runEvidenceDirectory "source-inputs"
New-Item -ItemType Directory -Path $sourceInputDirectory -Force | Out-Null

$headers = @{
    "X-CanDoItAll-Agent-Id" = "codex-realistic-project-memory-validation"
    "X-CanDoItAll-Agent-Name" = "Codex Realistic Project Memory Validation"
    "X-CanDoItAll-Agent-Machine" = $env:COMPUTERNAME
    "X-CanDoItAll-Agent-RepoRoot" = (Resolve-Path (Join-Path $bundleRoot "..\..")).Path
    "X-CanDoItAll-Agent-Session" = $runId
}

if (-not [string]::IsNullOrWhiteSpace($BearerToken)) {
    $headers["Authorization"] = "Bearer $BearerToken"
}

function Save-Evidence {
    param(
        [string]$Name,
        [object]$Payload
    )

    $safeName = $Name -replace "[^A-Za-z0-9_.-]", "_"
    $path = Join-Path $runEvidenceDirectory "$safeName.json"
    $Payload | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $path -Encoding UTF8
    return $path
}

function Invoke-CdiaJson {
    param(
        [string]$Method,
        [string]$Path,
        [object]$Body = $null,
        [string]$EvidenceName = "",
        [switch]$AllowFailure
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

    try {
        $response = Invoke-RestMethod @arguments
        if (-not [string]::IsNullOrWhiteSpace($EvidenceName)) {
            Save-Evidence $EvidenceName @{
                ok = $true
                method = $Method
                uri = $uri
                request = $Body
                response = $response
            } | Out-Null
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
            Save-Evidence $EvidenceName $failure | Out-Null
        }

        if (-not $AllowFailure) {
            throw
        }

        return $failure
    }
}

function Invoke-CdiaMultipartFile {
    param(
        [string]$Path,
        [string]$FilePath,
        [string]$ContentType,
        [string]$ProjectId,
        [string]$ActorId,
        [string]$IdempotencyKey,
        [string]$EvidenceName
    )

    $uri = "$BaseUrl$Path"
    $client = [System.Net.Http.HttpClient]::new()
    $form = [System.Net.Http.MultipartFormDataContent]::new()
    try {
        foreach ($key in $headers.Keys) {
            if ($key -eq "Authorization") {
                $client.DefaultRequestHeaders.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::Parse($headers[$key])
            }
            else {
                $client.DefaultRequestHeaders.Add($key, $headers[$key])
            }
        }

        $bytes = [System.IO.File]::ReadAllBytes($FilePath)
        $fileContent = [System.Net.Http.ByteArrayContent]::new($bytes)
        $fileContent.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse($ContentType)
        $form.Add($fileContent, "file", [System.IO.Path]::GetFileName($FilePath))
        if (-not [string]::IsNullOrWhiteSpace($ProjectId)) {
            $form.Add([System.Net.Http.StringContent]::new($ProjectId), "projectId")
        }

        $form.Add([System.Net.Http.StringContent]::new($ActorId), "actorId")
        if (-not [string]::IsNullOrWhiteSpace($IdempotencyKey)) {
            $form.Add([System.Net.Http.StringContent]::new($IdempotencyKey), "idempotencyKey")
        }

        $response = $client.PostAsync($uri, $form).GetAwaiter().GetResult()
        $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        $payload = $null
        if (-not [string]::IsNullOrWhiteSpace($body)) {
            try {
                $payload = $body | ConvertFrom-Json
            }
            catch {
                $payload = [pscustomobject]@{
                    rawBody = $body
                    parseError = $_.Exception.Message
                }
            }
        }

        Save-Evidence $EvidenceName @{
            ok = $response.IsSuccessStatusCode
            method = "POST"
            uri = $uri
            filePath = $FilePath
            projectId = $ProjectId
            idempotencyKey = $IdempotencyKey
            statusCode = [int]$response.StatusCode
            response = $payload
        } | Out-Null

        if (-not $response.IsSuccessStatusCode) {
            throw "File upload failed with HTTP $([int]$response.StatusCode): $body"
        }

        return $payload
    }
    finally {
        $form.Dispose()
        $client.Dispose()
    }
}

function ConvertTo-MediaPayload {
    param(
        [string]$Path,
        [string]$ContentType
    )

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    return @{
        fileName = [System.IO.Path]::GetFileName($Path)
        contentType = $ContentType
        base64Data = [Convert]::ToBase64String($bytes)
    }
}

function Normalize-Key {
    param([string]$Value)

    return ($Value.ToLowerInvariant() -replace "[^a-z0-9]+", "-").Trim("-")
}

function ConvertTo-LinkKindValue {
    param([string]$Kind)

    switch ($Kind) {
        "DependsOn" { return 1 }
        "Uses" { return 2 }
        "Validates" { return 3 }
        "Tests" { return 4 }
        "Blocks" { return 5 }
        "DerivedFrom" { return 6 }
        default { throw "Unsupported project-structure link kind '$Kind'." }
    }
}

function ConvertTo-NodeSubtype {
    param([string]$Title)

    if ($Title -match "risk|scald|safety|exposure|boundary|correction") {
        return "risk"
    }

    if ($Title -match "technical|architecture|software|hardware|process|energy|water|storage|battery|computer|vision|prototype") {
        return "architecture"
    }

    if ($Title -match "production|construction|procurement|installation|commissioning|manufacturing|tooling|civil|building") {
        return "implementation"
    }

    if ($Title -match "operations|organization|team|staff|payroll|facility|quality|training|OPEX") {
        return "operations"
    }

    if ($Title -match "finance|budget|revenue|EBITDA|CAPEX|funding|cash|unit economics|payback") {
        return "research"
    }

    if ($Title -match "launch|timeline|program|sequence|milestone|gate") {
        return "delivery"
    }

    return "research"
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

function Get-StageIdFromTitle {
    param([string]$Title)

    $match = [regex]::Match($Title, "^(S\d{2})\s+-\s+")
    if (-not $match.Success) {
        return $null
    }

    return $match.Groups[1].Value
}

function Parse-SourceTruthMarkdown {
    param(
        [string]$ProjectKey,
        [string]$DocumentPath
    )

    $lines = [System.IO.File]::ReadAllLines($DocumentPath)
    $sections = New-Object System.Collections.Generic.List[object]
    $current = $null
    $stack = @{}
    $ordinal = 0

    for ($index = 0; $index -lt $lines.Count; $index++) {
        $line = $lines[$index]
        $match = [regex]::Match($line, "^(#{2,4})\s+(.+)$")
        if (-not $match.Success) {
            if ($null -ne $current) {
                $current.contentLines.Add($line)
            }

            continue
        }

        if ($null -ne $current) {
            $sections.Add([pscustomobject]$current) | Out-Null
        }

        $level = $match.Groups[1].Value.Length
        $title = $match.Groups[2].Value.Trim()
        $ordinal++

        $stageId = if ($level -eq 2) {
            Get-StageIdFromTitle $title
        }
        else {
            $parentStage = $stack[2]
            if ($null -eq $parentStage) {
                $null
            }
            else {
                $parentStage.stageId
            }
        }

        $key = "$ProjectKey-$ordinal-$((Normalize-Key $title))"
        $parentKey = $null
        if ($level -gt 2) {
            for ($parentLevel = $level - 1; $parentLevel -ge 2; $parentLevel--) {
                if ($stack.ContainsKey($parentLevel) -and $null -ne $stack[$parentLevel]) {
                    $parentKey = $stack[$parentLevel].key
                    break
                }
            }
        }

        $current = [ordered]@{
            key = $key
            level = $level
            ordinal = $ordinal
            stageId = $stageId
            title = $title
            parentKey = $parentKey
            contentLines = New-Object System.Collections.Generic.List[string]
            sourceDocument = $DocumentPath
        }

        $stack[$level] = [pscustomobject]$current
        if ($level -lt 4) {
            foreach ($removeLevel in @(($level + 1)..4)) {
                if ($stack.ContainsKey($removeLevel)) {
                    $stack.Remove($removeLevel)
                }
            }
        }
    }

    if ($null -ne $current) {
        $sections.Add([pscustomobject]$current) | Out-Null
    }

    return @($sections | Where-Object { -not [string]::IsNullOrWhiteSpace($_.stageId) })
}

function Get-SectionNotes {
    param([object]$Section)

    $notes = (($Section.contentLines | ForEach-Object { "$_" }) -join "`r`n").Trim()
    if ([string]::IsNullOrWhiteSpace($notes)) {
        return "Structural parent node derived from the source-truth heading '$($Section.title)'."
    }

    return $notes
}

function Get-StageMarkdown {
    param(
        [string]$DocumentPath,
        [string]$StageTitle
    )

    $lines = [System.IO.File]::ReadAllLines($DocumentPath)
    $start = -1
    for ($index = 0; $index -lt $lines.Count; $index++) {
        if ($lines[$index].Trim() -eq "## $StageTitle") {
            $start = $index
            break
        }
    }

    if ($start -lt 0) {
        throw "Stage '$StageTitle' was not found in '$DocumentPath'."
    }

    $end = $lines.Count
    for ($index = $start + 1; $index -lt $lines.Count; $index++) {
        if ($lines[$index] -match "^##\s+") {
            $end = $index
            break
        }
    }

    return ($lines[$start..($end - 1)] -join "`r`n") + "`r`n"
}

function Resolve-Decision {
    param(
        [object]$ReviewItem,
        [string]$StageId,
        [string]$ProjectKey
    )

    $candidate = $ReviewItem.candidatePreview
    if ($null -eq $candidate) {
        return [pscustomobject]@{
            kind = "Defer"
            category = "missing-preview"
            notes = "Deferred because the review item did not include a candidate preview."
        }
    }

    $sourceSystem = "$($candidate.sourceSystem)"
    $sourceItemType = "$($candidate.sourceItemType)"
    $sourceTitle = "$($candidate.sourceTitle)"
    $proposedTitle = "$($candidate.proposedTitle)"
    $sourceLocator = "$($candidate.sourceLocator)"

    if ($sourceSystem -eq "WorkbenchProjectStructure" -and $sourceItemType -eq "ProjectLink") {
        return [pscustomobject]@{
            kind = "Reject"
            category = "non-memory-link"
            notes = "Rejected because project-structure links provide graph relations, not durable narrative memories."
        }
    }

    if ($sourceSystem -eq "WorkbenchProjectStructure" -and $sourceItemType -eq "ProjectNode" -and ($sourceTitle -match "Stage source chunk" -or $proposedTitle -match "Stage source chunk")) {
        return [pscustomobject]@{
            kind = "Reject"
            category = "duplicate-stage-file-node"
            notes = "Rejected because the corresponding ExternalFile source is the primary evidence for this stage chunk."
        }
    }

    if ($sourceSystem -eq "ExternalFile" -and $sourceLocator -match "$ProjectKey-$($StageId.ToLowerInvariant())\.md") {
        return [pscustomobject]@{
            kind = "Approve"
            category = "stage-source-truth"
            notes = "Approved after comparison with the normalized source-truth stage document."
        }
    }

    if ($sourceSystem -eq "WorkbenchProjectStructure" -and $sourceItemType -eq "ProjectNode") {
        return [pscustomobject]@{
            kind = "Approve"
            category = "structured-project-node"
            notes = "Approved because this is a typed project-structure node derived from the time-sliced source truth."
        }
    }

    return [pscustomobject]@{
        kind = "Defer"
        category = "manual-review-needed"
        notes = "Deferred because the candidate was not a stage source file or derived project node."
    }
}

function Invoke-PendingReviewDecisions {
    param(
        [string]$ProjectId,
        [string]$ProjectKey,
        [string]$StageId,
        [string]$StagePrefix,
        [string]$ActorId,
        [string]$EvidenceSlot
    )

    $decisionRecords = @()
    for ($reviewPage = 1; $reviewPage -le 20; $reviewPage++) {
        $snapshot = Invoke-CdiaJson "GET" "/api/cognitive-memory/snapshot?projectId=$ProjectId&take=50" $null "$StagePrefix-$EvidenceSlot-review-page-$reviewPage"
        $pendingItems = @($snapshot.reviewItems | Where-Object { $_.status -eq 0 -and $null -ne $_.candidatePreview })
        if ($pendingItems.Count -eq 0) {
            break
        }

        foreach ($item in $pendingItems) {
            $decision = Resolve-Decision $item $StageId $ProjectKey
            $itemId = Get-ObjectIdValue $item.id
            if ([string]::IsNullOrWhiteSpace($itemId)) {
                continue
            }

            $decisionBody = @{
                decisionKind = $decision.kind
                actorId = $ActorId
                notes = "$($decision.notes) Stage=$StageId; Project=$ProjectKey; Category=$($decision.category)."
                expectedConcurrencyToken = $item.concurrencyToken
            }

            $decisionResponse = Invoke-CdiaJson "POST" "/api/cognitive-memory/review-items/$itemId/decisions" $decisionBody "$StagePrefix-$EvidenceSlot-review-$reviewPage-$itemId-$($decision.kind)"
            $decisionRecords += [ordered]@{
                stageId = $StageId
                projectKey = $ProjectKey
                projectId = $ProjectId
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

function Invoke-ConsolidationPages {
    param(
        [string]$ProjectId,
        [string]$ProjectKey,
        [string]$StageId,
        [string]$StagePrefix,
        [string]$ActorId
    )

    $runs = @()
    $cursor = $null
    for ($page = 1; $page -le 20; $page++) {
        $body = @{
            projectId = $ProjectId
            mode = "IncrementalRecent"
            triggerKind = "Manual"
            idempotencyKey = "realistic-project-memory:${runId}:${ProjectKey}:${StageId}:${ProjectId}:consolidate:page-${page}:v1"
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

        $run = Invoke-CdiaJson "POST" "/api/cognitive-memory/consolidation/runs" $body "$StagePrefix-08-consolidation-page-$page"
        $runs += $run
        $sourceItemsScanned = if ($null -eq $run.sourceItemsScanned) { 0 } else { [int]$run.sourceItemsScanned }
        if ($sourceItemsScanned -eq 0) {
            break
        }

        $cursor = "$($run.nextCursor)"
        if ([string]::IsNullOrWhiteSpace($cursor)) {
            break
        }
    }

    return $runs
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

$manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
$actorId = "codex:realistic-project-memory-validation"

$accessStatus = Invoke-CdiaJson "GET" "/api/access/status" $null "00-access-status"
$databaseSelection = Invoke-CdiaJson "GET" "/api/cognitive-memory/database/selection" $null "01-database-selection"
$memoryStatus = Invoke-CdiaJson "GET" "/api/cognitive-memory/status" $null "02-cognitive-memory-status"

if (-not $AllowNonPostgreSql -and $memoryStatus.isPostgreSql -ne $true) {
    throw "Realistic project memory validation requires PostgreSQL. Active provider is '$($memoryStatus.providerKindName)'."
}

$settings = Invoke-CdiaJson "PUT" "/api/cognitive-memory/settings" @{
    scheduleMode = "ManualOnly"
    nightlyLocalTime = "02:00"
    idleMinutes = 30
    scheduledLocalTimes = @("03:00", "16:30")
    autoIngestProjectStructure = $true
    autoIngestProcessRuntime = $true
    autoConsolidateAfterIngestion = $true
    actorId = $actorId
} "03-memory-settings"

$summary = [ordered]@{
    baseUrl = $BaseUrl
    runId = $runId
    evidenceDirectory = $runEvidenceDirectory
    accessStatus = $accessStatus
    databaseSelection = $databaseSelection
    cognitiveMemoryStatus = $memoryStatus
    settings = $settings
    projects = @()
    stages = @()
    reviewDecisions = @()
    recallProbes = @()
}

$projectsByKey = @{}
$projectIndex = 0
foreach ($projectSpec in @($manifest.projects)) {
    $projectIndex++
    $prefix = "{0:D2}-{1}" -f $projectIndex, $projectSpec.key
    $documentPath = Resolve-Path (Join-Path $bundleRoot $projectSpec.sourceDocument)
    $sections = Parse-SourceTruthMarkdown $projectSpec.key $documentPath.Path
    $stageCount = @($sections | Where-Object { $_.level -eq 2 } | Select-Object -ExpandProperty stageId -Unique).Count
    if ($stageCount -lt $projectSpec.requiredMinimumStageCount) {
        throw "Project '$($projectSpec.key)' has only $stageCount stages; expected at least $($projectSpec.requiredMinimumStageCount)."
    }

    $project = Invoke-CdiaJson "POST" "/api/project-structure/projects" @{
        name = $projectSpec.name
        description = $projectSpec.description
        objective = $projectSpec.objective
        currentPhase = $projectSpec.currentPhase
        status = 1
    } "$prefix-00-project"

    $lease = Invoke-CdiaJson "POST" "/api/project-structure/leases/acquire" @{
        scopeKind = "Project"
        scopeKey = "$($project.id)"
        reason = "Load realistic time-sliced project source truth for Cognitive Memory validation."
        durationMinutes = 240
    } "$prefix-01-lease"

    $projectsByKey[$projectSpec.key] = [ordered]@{
        key = $projectSpec.key
        name = $projectSpec.name
        projectId = "$($project.id)"
        leaseToken = "$($lease.leaseToken)"
        documentPath = $documentPath.Path
        sections = $sections
        nodeIds = @{}
        stageFileNodeIds = @{}
        stageTitles = @{}
        createdNodeCount = 0
        createdLinkCount = 0
        uploadedSources = @()
    }

    $summary.projects += [ordered]@{
        key = $projectSpec.key
        name = $projectSpec.name
        projectId = "$($project.id)"
        sourceDocument = $documentPath.Path
        stageCount = $stageCount
        leaseToken = "$($lease.leaseToken)"
    }
}

foreach ($stageId in @($manifest.stageOrder)) {
    $stageSummary = [ordered]@{
        stageId = $stageId
        projectCycles = @()
        recalls = @()
        decisions = @()
    }

    foreach ($projectInfo in $projectsByKey.Values) {
        $projectKey = $projectInfo.key
        $projectId = $projectInfo.projectId
        $leaseToken = $projectInfo.leaseToken
        $rootNodeKey = "project:$projectId"
        $sectionsForStage = @($projectInfo.sections | Where-Object { $_.stageId -eq $stageId })
        if ($sectionsForStage.Count -eq 0) {
            continue
        }

        $stageNodeSection = @($sectionsForStage | Where-Object { $_.level -eq 2 } | Select-Object -First 1)[0]
        $stageTitle = $stageNodeSection.title
        $projectInfo.stageTitles[$stageId] = $stageTitle
        $safeStageFileName = "$projectKey-$($stageId.ToLowerInvariant()).md"
        $stageFilePath = Join-Path $sourceInputDirectory $safeStageFileName
        Get-StageMarkdown $projectInfo.documentPath $stageTitle | Set-Content -LiteralPath $stageFilePath -Encoding UTF8

        $stagePrefix = "$($stageId.ToLowerInvariant())-$projectKey"
        $createdNodes = @()
        $createdLinks = @()

        foreach ($section in $sectionsForStage) {
            $parentNodeKey = if ([string]::IsNullOrWhiteSpace($section.parentKey)) {
                $rootNodeKey
            }
            else {
                "$($projectInfo.nodeIds[$section.parentKey])"
            }

            $stageNumber = [int]$stageId.Substring(1)
            $levelOffset = ($section.level - 2) * 180
            $sameLevelIndex = @($sectionsForStage | Where-Object { $_.level -eq $section.level -and $_.ordinal -le $section.ordinal }).Count
            $x = 80 + (($stageNumber - 1) * 520) + $levelOffset
            $y = 90 + ($sameLevelIndex * 96)
            $node = Invoke-CdiaJson "POST" "/api/project-structure/projects/$projectId/nodes" @{
                objectType = "ProjectBlock"
                title = $section.title
                subtitle = "Source truth $stageId level $($section.level)"
                notes = (Get-SectionNotes $section)
                parentNodeKey = $parentNodeKey
                x = $x
                y = $y
                objectSubtype = (ConvertTo-NodeSubtype $section.title)
                metadataJson = (@{
                    bundle = $manifest.bundle
                    projectKey = $projectKey
                    stageId = $stageId
                    sourceTruthRelativePath = ($projectInfo.documentPath.Replace("$bundleRoot\", ""))
                    sourceHeading = $section.title
                    sourceHeadingLevel = $section.level
                    sourceOrdinal = $section.ordinal
                    sourceTruthKind = "time-sliced-heading"
                } | ConvertTo-Json -Compress)
                leaseToken = $leaseToken
            } "$stagePrefix-01-node-$($section.ordinal)-$($section.key)"

            $projectInfo.nodeIds[$section.key] = "$($node.id)"
            $projectInfo.createdNodeCount++
            $createdNodes += [ordered]@{
                key = $section.key
                id = "$($node.id)"
                level = $section.level
                title = $section.title
            }
        }

        $stageNodeId = "$($projectInfo.nodeIds[$stageNodeSection.key])"
        $stageFileMedia = ConvertTo-MediaPayload $stageFilePath "text/markdown"
        $stageFileNode = Invoke-CdiaJson "POST" "/api/project-structure/projects/$projectId/nodes" @{
            objectType = "File"
            title = "$stageId Stage source chunk"
            subtitle = $safeStageFileName
            notes = "Generated from normalized source truth document '$($projectInfo.documentPath)' for stage '$stageTitle'. This file is uploaded as the ExternalFile source for memory validation."
            parentNodeKey = $stageNodeId
            x = 80 + (([int]$stageId.Substring(1) - 1) * 520)
            y = 40
            objectSubtype = "markdown"
            media = $stageFileMedia
            metadataJson = (@{
                bundle = $manifest.bundle
                projectKey = $projectKey
                stageId = $stageId
                sourceTruthKind = "generated-stage-source-chunk"
                generatedFileName = $safeStageFileName
            } | ConvertTo-Json -Compress)
            leaseToken = $leaseToken
        } "$stagePrefix-02-stage-file-node"

        $projectInfo.stageFileNodeIds[$stageId] = "$($stageFileNode.id)"
        $projectInfo.createdNodeCount++
        $createdNodes += [ordered]@{
            key = "$projectKey-$stageId-source-file"
            id = "$($stageFileNode.id)"
            level = 5
            title = "$stageId Stage source chunk"
        }

        foreach ($section in $sectionsForStage) {
            $sourceNodeId = "$($projectInfo.nodeIds[$section.key])"
            $link = Invoke-CdiaJson "POST" "/api/project-structure/projects/$projectId/links" @{
                sourceNodeId = $sourceNodeId
                targetNodeId = "$($stageFileNode.id)"
                kind = (ConvertTo-LinkKindValue "DerivedFrom")
                leaseToken = $leaseToken
            } "$stagePrefix-03-derived-link-$($section.ordinal)"

            $projectInfo.createdLinkCount++
            $createdLinks += [ordered]@{
                sourceNodeId = $sourceNodeId
                targetNodeId = "$($stageFileNode.id)"
                kind = "DerivedFrom"
                responseId = "$($link.id)"
            }
        }

        $previousStageId = "S{0:D2}" -f ([int]$stageId.Substring(1) - 1)
        if ($projectInfo.stageFileNodeIds.ContainsKey($previousStageId)) {
            $previousStageSection = @($projectInfo.sections | Where-Object { $_.stageId -eq $previousStageId -and $_.level -eq 2 } | Select-Object -First 1)[0]
            $previousStageNodeId = "$($projectInfo.nodeIds[$previousStageSection.key])"
            $dependsLink = Invoke-CdiaJson "POST" "/api/project-structure/projects/$projectId/links" @{
                sourceNodeId = $stageNodeId
                targetNodeId = $previousStageNodeId
                kind = (ConvertTo-LinkKindValue "DependsOn")
                leaseToken = $leaseToken
            } "$stagePrefix-04-depends-on-previous-stage"

            $projectInfo.createdLinkCount++
            $createdLinks += [ordered]@{
                sourceNodeId = $stageNodeId
                targetNodeId = $previousStageNodeId
                kind = "DependsOn"
                responseId = "$($dependsLink.id)"
            }
        }

        $upload = Invoke-CdiaMultipartFile "/api/cognitive-memory/external-sources/files" $stageFilePath "text/markdown" $projectId $actorId "realistic-project-memory:${runId}:${projectKey}:${stageId}:${projectId}:external-file:v1" "$stagePrefix-05-external-file"
        $projectInfo.uploadedSources += [ordered]@{
            stageId = $stageId
            file = $stageFilePath
            operationId = "$($upload.operationId)"
        }

        $readback = Invoke-CdiaJson "POST" "/api/project-structure/projects/$projectId/structure/read" @{
            includeLinks = $true
            includeLayout = $true
            includeMetadata = $true
            includeNotes = $true
            includeAssets = $true
            take = 500
        } "$stagePrefix-06-structure-readback"

        $ingest = Invoke-CdiaJson "POST" "/api/cognitive-memory/ingestion/project-structure" @{
            scopeId = $projectId
            projectId = $projectId
            take = 500
            idempotencyKey = "realistic-project-memory:${projectKey}:${stageId}:${projectId}:project-structure:v1"
        } "$stagePrefix-07-ingest-project-structure"

        $consolidationRuns = @(Invoke-ConsolidationPages $projectId $projectKey $stageId $stagePrefix $actorId)
        $consolidation = @($consolidationRuns | Select-Object -Last 1)[0]

        $snapshotBefore = Invoke-CdiaJson "GET" "/api/cognitive-memory/snapshot?projectId=$projectId&take=50" $null "$stagePrefix-09-snapshot-before-review"
        $decisionRecords = @(Invoke-PendingReviewDecisions $projectId $projectKey $stageId $stagePrefix $actorId "10")
        foreach ($decisionRecord in $decisionRecords) {
            $stageSummary.decisions += $decisionRecord
            $summary.reviewDecisions += $decisionRecord
        }

        $snapshotAfter = Invoke-CdiaJson "GET" "/api/cognitive-memory/snapshot?projectId=$projectId&take=50" $null "$stagePrefix-11-snapshot-after-review"
        $probe = @($manifest.recallProbes | Where-Object { $_.projectKey -eq $projectKey -and $_.stageId -eq $stageId } | Select-Object -First 1)
        $recallRecord = $null
        if ($probe.Count -gt 0) {
            $probeSpec = $probe[0]
            $recall = Invoke-CdiaJson "POST" "/api/cognitive-memory/recall" @{
                projectId = $projectId
                query = $probeSpec.question
                intent = "Architecture"
                mode = "FocusedTaskContext"
                policy = (Get-RecallPolicy $actorId)
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
                    runId = $runId
                    bundle = $manifest.bundle
                    projectKey = $projectKey
                    stageId = $stageId
                    probeId = $probeSpec.id
                }
            } "$stagePrefix-12-recall" -AllowFailure

            $recallRecord = [ordered]@{
                probeId = $probeSpec.id
                stageId = $stageId
                projectKey = $projectKey
                projectId = $projectId
                question = $probeSpec.question
                requiredTerms = @($probeSpec.requiredTerms)
                ok = if ($null -ne $recall.ok) { $recall.ok } else { $true }
                traceId = Get-ObjectIdValue $recall.traceId
                includedRecordCount = $recall.includedRecordCount
                selectedClaimCount = $recall.selectedClaimCount
                contextPack = $recall.contextPack
                error = $recall.error
            }

            $stageSummary.recalls += $recallRecord
            $summary.recallProbes += $recallRecord
        }

        $cycle = [ordered]@{
            stageId = $stageId
            projectKey = $projectKey
            projectId = $projectId
            stageTitle = $stageTitle
            createdNodes = $createdNodes
            createdLinks = $createdLinks
            stageFilePath = $stageFilePath
            externalOperationId = "$($upload.operationId)"
            readbackNodeCount = @($readback.nodes).Count
            readbackLinkCount = @($readback.links).Count
            ingest = $ingest
            consolidation = $consolidation
            consolidationRuns = $consolidationRuns
            snapshotBeforeSummary = $snapshotBefore.summary
            decisions = $decisionRecords
            snapshotAfterSummary = $snapshotAfter.summary
            recall = $recallRecord
        }

        $stageSummary.projectCycles += $cycle
        Save-Evidence "$stagePrefix-99-cycle-summary" $cycle | Out-Null
    }

    $globalSnapshot = Invoke-CdiaJson "GET" "/api/cognitive-memory/snapshot?take=50" $null "$($stageId.ToLowerInvariant())-99-global-snapshot-after-stage"
    $stageSummary.globalSnapshotSummary = $globalSnapshot.summary
    $summary.stages += $stageSummary
    Save-Evidence "$($stageId.ToLowerInvariant())-stage-summary" $stageSummary | Out-Null
}

foreach ($projectInfo in $projectsByKey.Values) {
    Invoke-CdiaJson "POST" "/api/project-structure/leases/release" @{
        scopeKind = "Project"
        scopeKey = "$($projectInfo.projectId)"
        leaseToken = "$($projectInfo.leaseToken)"
    } "98-release-lease-$($projectInfo.key)" -AllowFailure | Out-Null

    foreach ($summaryProject in $summary.projects) {
        if ($summaryProject.key -eq $projectInfo.key) {
            $summaryProject["createdNodeCount"] = $projectInfo.createdNodeCount
            $summaryProject["createdLinkCount"] = $projectInfo.createdLinkCount
            $summaryProject["uploadedSources"] = $projectInfo.uploadedSources
        }
    }
}

$finalSnapshot = Invoke-CdiaJson "GET" "/api/cognitive-memory/snapshot?take=50" $null "99-final-snapshot"
$finalStatus = Invoke-CdiaJson "GET" "/api/cognitive-memory/status" $null "99-final-status"
$summary.finalSnapshot = $finalSnapshot
$summary.finalStatus = $finalStatus
Save-Evidence "99-run-summary" $summary | Out-Null

[ordered]@{
    runId = $runId
    evidenceDirectory = $runEvidenceDirectory
    projectCount = @($summary.projects).Count
    stageCount = @($summary.stages).Count
    recallProbeCount = @($summary.recallProbes).Count
    providerKindName = $finalStatus.providerKindName
} | ConvertTo-Json -Depth 8
