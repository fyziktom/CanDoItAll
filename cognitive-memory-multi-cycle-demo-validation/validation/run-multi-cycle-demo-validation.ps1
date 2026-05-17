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
$bundleRoot = Split-Path -Parent $scriptRoot
if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
    $ManifestPath = Join-Path $bundleRoot "sample-data\source-manifest.json"
}

if ([string]::IsNullOrWhiteSpace($EvidenceDirectory)) {
    $EvidenceDirectory = Join-Path $scriptRoot "evidence"
}

$runId = Get-Date -Format "yyyyMMdd-HHmmss"
$runEvidenceDirectory = Join-Path $EvidenceDirectory $runId
New-Item -ItemType Directory -Path $runEvidenceDirectory -Force | Out-Null

$headers = @{
    "X-CanDoItAll-Agent-Id" = "codex-cognitive-memory-multicycle"
    "X-CanDoItAll-Agent-Name" = "Codex Cognitive Memory Multi-Cycle Runner"
    "X-CanDoItAll-Agent-Machine" = $env:COMPUTERNAME
    "X-CanDoItAll-Agent-RepoRoot" = (Resolve-Path (Join-Path $bundleRoot "..")).Path
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
        TimeoutSec = 120
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
        $failure = @{
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

        return [pscustomobject]$failure
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
        $payload = if ([string]::IsNullOrWhiteSpace($body)) { $null } else { $body | ConvertFrom-Json }

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

function ConvertTo-LinkKindValue {
    param([string]$Kind)

    switch ($Kind) {
        "DependsOn" { return 1 }
        "Uses" { return 2 }
        "Validates" { return 3 }
        "Tests" { return 4 }
        "Blocks" { return 5 }
        "DerivedFrom" { return 6 }
        default { return 2 }
    }
}

function Normalize-Key {
    param([string]$Value)
    return ($Value.ToLowerInvariant() -replace "[^a-z0-9]+", "-").Trim("-")
}

function Get-ShortExcerpt {
    param(
        [string]$Content,
        [int]$MaxLength = 1800
    )

    $trimmed = $Content.Trim()
    if ($trimmed.Length -le $MaxLength) {
        return $trimmed
    }

    return $trimmed.Substring(0, $MaxLength) + "`r`n`r`n[truncated for project-structure stage anchor]"
}

function New-RecallQuery {
    param(
        [string]$ProjectName,
        [string]$StageId
    )

    switch ($StageId) {
        "S01" { return "For $ProjectName, summarize the core source-of-truth boundary and two durable risks." }
        "S02" { return "What changed for $ProjectName after the operational update, and which existing memory should it update rather than duplicate?" }
        "S03" { return "Which earlier assumption for $ProjectName was contradicted, and what decision should the memory prefer now?" }
        "S04" { return "Which email-specific instruction for $ProjectName should affect future work, and what should not be overgeneralized?" }
        default { return "Summarize the useful source-backed memories for $ProjectName." }
    }
}

function Resolve-Decision {
    param([object]$ReviewItem)

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
            notes = "Rejected because project links should not become durable memories in this validation."
        }
    }

    if ($sourceSystem -eq "WorkbenchProjectStructure" -and ($sourceTitle -match "^S0[1-4] " -or $proposedTitle -match "^S0[1-4] ")) {
        return [pscustomobject]@{
            kind = "Reject"
            category = "duplicate-stage-anchor"
            notes = "Rejected as an intentional duplicate/summary anchor; the uploaded Markdown source is the primary memory evidence for this stage."
        }
    }

    if ($sourceSystem -eq "ExternalFile" -and $sourceLocator -match "-s0[1-4]\.md") {
        return [pscustomobject]@{
            kind = "Approve"
            category = "stage-source-memory"
            notes = "Approved: staged Markdown source is source-backed, project-scoped, and tracked in the XLSX manifest."
        }
    }

    return [pscustomobject]@{
        kind = "Defer"
        category = "manual-review-needed"
        notes = "Deferred because the source does not match the staged-corpus approval policy."
    }
}

$manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
$actorId = "codex:cognitive-memory-multicycle"

$accessStatus = Invoke-CdiaJson "GET" "/api/access/status" $null "00-access-status"
$databaseSelection = Invoke-CdiaJson "GET" "/api/cognitive-memory/database/selection" $null "01-database-selection"
$memoryStatus = Invoke-CdiaJson "GET" "/api/cognitive-memory/status" $null "02-cognitive-memory-status"

if (-not $AllowNonPostgreSql -and $memoryStatus.isPostgreSql -ne $true) {
    throw "Multi-cycle validation requires PostgreSQL. Active provider is '$($memoryStatus.providerKindName)'."
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
foreach ($projectSpec in $manifest.projects) {
    $projectIndex++
    $prefix = "{0:D2}-{1}" -f $projectIndex, $projectSpec.key
    $project = Invoke-CdiaJson "POST" "/api/project-structure/projects" @{
        name = $projectSpec.name
        description = "Multi-cycle Cognitive Memory demo validation project for $($projectSpec.domain). Owner: $($projectSpec.owner)."
        objective = "Validate staged source-backed memory retention, duplicate handling, contradiction handling, and chat recall for $($projectSpec.name)."
        currentPhase = "Multi-cycle memory validation"
        status = 1
    } "$prefix-00-project"

    $lease = Invoke-CdiaJson "POST" "/api/project-structure/leases/acquire" @{
        scopeKind = "Project"
        scopeKey = "$($project.id)"
        reason = "Load staged Cognitive Memory multi-cycle demo source data."
        durationMinutes = 180
    } "$prefix-01-lease"

    $projectsByKey[$projectSpec.key] = [ordered]@{
        key = $projectSpec.key
        name = $projectSpec.name
        domain = $projectSpec.domain
        owner = $projectSpec.owner
        projectId = "$($project.id)"
        leaseToken = "$($lease.leaseToken)"
        sources = @()
    }

    $summary.projects += [ordered]@{
        key = $projectSpec.key
        name = $projectSpec.name
        projectId = "$($project.id)"
        leaseToken = "$($lease.leaseToken)"
    }
}

$stages = $manifest.stages | Sort-Object id
foreach ($stage in $stages) {
    $stageId = "$($stage.id)"
    $stagePrefix = $stageId.ToLowerInvariant()
    $stageSources = @($manifest.sources | Where-Object { $_.stageId -eq $stageId } | Sort-Object projectKey)
    $stageSummary = [ordered]@{
        stageId = $stageId
        stageName = $stage.name
        sources = @()
        projectCycles = @()
        decisions = @()
        recalls = @()
    }

    foreach ($source in $stageSources) {
        $projectInfo = $projectsByKey[$source.projectKey]
        $projectId = $projectInfo.projectId
        $leaseToken = $projectInfo.leaseToken
        $sourcePath = Join-Path $bundleRoot $source.relativePath
        $content = Get-Content -LiteralPath $sourcePath -Raw
        $media = ConvertTo-MediaPayload $sourcePath "text/markdown"
        $rootNodeKey = "project:$projectId"
        $safeSourceKey = Normalize-Key $source.sourceId
        $sourceIndex = [array]::IndexOf($stageSources, $source) + 1
        $evidencePrefix = "$stagePrefix-{0:D2}-{1}" -f $sourceIndex, $source.projectKey

        $fileNode = Invoke-CdiaJson "POST" "/api/project-structure/projects/$projectId/nodes" @{
            objectType = "File"
            title = "$stageId $($source.stageName) Markdown source"
            subtitle = "$($source.sourceId)"
            notes = "Tracked source file for Cognitive Memory multi-cycle validation. Relative path: $($source.relativePath). Expected signals: $($source.expectedSignals)"
            parentNodeKey = $rootNodeKey
            x = 80 + (160 * ($sourceIndex % 3))
            y = 640 + (120 * [int]($stageId.Substring(1)))
            objectSubtype = "markdown"
            media = $media
            metadataJson = (@{
                bundle = "cognitive-memory-multi-cycle-demo-validation"
                sourceId = $source.sourceId
                stageId = $stageId
                projectKey = $source.projectKey
                relativePath = $source.relativePath
                intendedLoad = $source.intendedLoad
            } | ConvertTo-Json -Compress)
            leaseToken = $leaseToken
        } "$evidencePrefix-01-file-node"

        $anchorSubtype = switch ($stageId) {
            "S01" { "research" }
            "S02" { "operations" }
            "S03" { "risk" }
            "S04" { "delivery" }
            default { "research" }
        }

        $anchorNode = Invoke-CdiaJson "POST" "/api/project-structure/projects/$projectId/nodes" @{
            objectType = "ProjectBlock"
            title = "$stageId $($source.stageName) review anchor"
            subtitle = "Duplicate-control anchor for $($source.sourceId)"
            notes = "Source ID: $($source.sourceId)`r`nStage: $stageId`r`nExpected signals: $($source.expectedSignals)`r`n`r`nExcerpt:`r`n$(Get-ShortExcerpt $content)"
            parentNodeKey = $rootNodeKey
            x = 80 + (160 * ($sourceIndex % 3))
            y = 920 + (120 * [int]($stageId.Substring(1)))
            objectSubtype = $anchorSubtype
            metadataJson = (@{
                bundle = "cognitive-memory-multi-cycle-demo-validation"
                sourceId = $source.sourceId
                stageId = $stageId
                projectKey = $source.projectKey
                duplicateControl = $true
                primarySource = $source.relativePath
            } | ConvertTo-Json -Compress)
            leaseToken = $leaseToken
        } "$evidencePrefix-02-anchor-node"

        $upload = Invoke-CdiaMultipartFile "/api/cognitive-memory/external-sources/files" $sourcePath "text/markdown" $projectId $actorId "cognitive-memory-multicycle:$($source.sourceId):external-file:v1" "$evidencePrefix-03-external-file"

        $projectInfo.sources += [ordered]@{
            sourceId = $source.sourceId
            stageId = $stageId
            relativePath = $source.relativePath
            fileNodeId = "$($fileNode.id)"
            anchorNodeId = "$($anchorNode.id)"
            externalOperationId = "$($upload.operationId)"
        }

        $stageSummary.sources += [ordered]@{
            sourceId = $source.sourceId
            projectKey = $source.projectKey
            projectId = $projectId
            relativePath = $source.relativePath
            fileNodeId = "$($fileNode.id)"
            anchorNodeId = "$($anchorNode.id)"
            externalOperationId = "$($upload.operationId)"
        }
    }

    $processIngest = Invoke-CdiaJson "POST" "/api/cognitive-memory/ingestion/processes" @{
        take = 250
        idempotencyKey = "cognitive-memory-multicycle:${stageId}:${runId}:process-runtime:v1"
    } "$stagePrefix-90-process-ingestion" -AllowFailure
    $stageSummary.processIngest = $processIngest

    foreach ($projectInfo in $projectsByKey.Values) {
        $projectId = $projectInfo.projectId
        $projectKey = $projectInfo.key
        $cyclePrefix = "$stagePrefix-cycle-$projectKey"

        $readback = Invoke-CdiaJson "POST" "/api/project-structure/projects/$projectId/structure/read" @{
            includeLinks = $true
            includeLayout = $true
            includeMetadata = $true
            includeNotes = $true
            includeAssets = $true
            take = 300
        } "$cyclePrefix-01-structure-readback"

        $ingest = Invoke-CdiaJson "POST" "/api/cognitive-memory/ingestion/project-structure" @{
            scopeId = $projectId
            projectId = $projectId
            take = 300
            idempotencyKey = "cognitive-memory-multicycle:${stageId}:${projectKey}:${projectId}:project-structure:v1"
        } "$cyclePrefix-02-ingest-project-structure"

        $consolidation = Invoke-CdiaJson "POST" "/api/cognitive-memory/consolidation/runs" @{
            projectId = $projectId
            mode = "IncrementalRecent"
            triggerKind = "Manual"
            idempotencyKey = "cognitive-memory-multicycle:${stageId}:${projectKey}:${projectId}:consolidate:v1"
            profile = @{
                name = "developer-no-vector-projection"
                processSourceItems = $true
                detectContradictions = $true
                extractProcedures = $true
                rebuildProjections = $false
                createHumanReviewItems = $true
                maxItems = 120
            }
            policy = @{
                actorId = $actorId
                accessLevel = "Restricted"
                policyProfileId = "developer-api"
                riskLevel = "Low"
                allowRestrictedContent = $true
            }
        } "$cyclePrefix-03-consolidation"

        $snapshotBefore = Invoke-CdiaJson "GET" "/api/cognitive-memory/snapshot?projectId=$projectId&take=50" $null "$cyclePrefix-04-snapshot-before-review"
        $pendingItems = @($snapshotBefore.reviewItems | Where-Object { $_.status -eq 0 -and $null -ne $_.candidatePreview })
        $decisionRecords = @()

        foreach ($item in $pendingItems) {
            $decision = Resolve-Decision $item
            $decisionBody = @{
                decisionKind = $decision.kind
                actorId = $actorId
                notes = "$($decision.notes) Stage=$stageId; Project=$projectKey; Category=$($decision.category)."
                expectedConcurrencyToken = $item.concurrencyToken
            }

            $decisionResponse = Invoke-CdiaJson "POST" "/api/cognitive-memory/review-items/$($item.id.value)/decisions" $decisionBody "$cyclePrefix-05-review-$($item.id.value)-$($decision.kind)"
            $decisionRecord = [ordered]@{
                stageId = $stageId
                projectKey = $projectKey
                projectId = $projectId
                reviewItemId = "$($item.id.value)"
                decisionKind = $decision.kind
                category = $decision.category
                candidateTitle = "$($item.candidatePreview.proposedTitle)"
                sourceSystem = "$($item.candidatePreview.sourceSystem)"
                sourceItemType = "$($item.candidatePreview.sourceItemType)"
                sourceLocator = "$($item.candidatePreview.sourceLocator)"
                response = $decisionResponse
            }
            $decisionRecords += $decisionRecord
            $summary.reviewDecisions += $decisionRecord
        }

        $snapshotAfter = Invoke-CdiaJson "GET" "/api/cognitive-memory/snapshot?projectId=$projectId&take=50" $null "$cyclePrefix-06-snapshot-after-review"
        $recallQuery = New-RecallQuery $projectInfo.name $stageId
        $recall = Invoke-CdiaJson "POST" "/api/cognitive-memory/recall" @{
            projectId = $projectId
            query = $recallQuery
            intent = "Architecture"
            mode = "FocusedTaskContext"
            policy = @{
                actorId = $actorId
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
                stageId = $stageId
                projectKey = $projectKey
                validationRunId = $runId
            }
        } "$cyclePrefix-07-recall"

        $recallRecord = [ordered]@{
            stageId = $stageId
            projectKey = $projectKey
            projectId = $projectId
            query = $recallQuery
            traceId = "$($recall.traceId)"
            includedRecordCount = $recall.includedRecordCount
            selectedClaimCount = $recall.selectedClaimCount
            contextPack = $recall.contextPack
        }
        $summary.recallProbes += $recallRecord
        $stageSummary.recalls += $recallRecord

        $cycle = [ordered]@{
            projectKey = $projectKey
            projectId = $projectId
            readbackNodeCount = $readback.nodes.Count
            readbackLinkCount = $readback.links.Count
            ingest = $ingest
            consolidation = $consolidation
            snapshotBeforeSummary = $snapshotBefore.summary
            decisions = $decisionRecords
            snapshotAfterSummary = $snapshotAfter.summary
            recall = $recallRecord
        }
        $stageSummary.projectCycles += $cycle
    }

    $globalSnapshot = Invoke-CdiaJson "GET" "/api/cognitive-memory/snapshot?take=50" $null "$stagePrefix-99-global-snapshot-after-stage"
    $stageSummary.globalSnapshotSummary = $globalSnapshot.summary
    $summary.stages += $stageSummary
    Save-Evidence "$stagePrefix-stage-summary" $stageSummary | Out-Null
}

foreach ($projectInfo in $projectsByKey.Values) {
    Invoke-CdiaJson "POST" "/api/project-structure/leases/release" @{
        scopeKind = "Project"
        scopeKey = "$($projectInfo.projectId)"
        leaseToken = "$($projectInfo.leaseToken)"
    } "98-release-lease-$($projectInfo.key)" -AllowFailure | Out-Null
}

$finalSnapshot = Invoke-CdiaJson "GET" "/api/cognitive-memory/snapshot?take=50" $null "99-final-snapshot"
$finalStatus = Invoke-CdiaJson "GET" "/api/cognitive-memory/status" $null "99-final-status"
$summary.finalSnapshot = $finalSnapshot
$summary.finalStatus = $finalStatus
Save-Evidence "99-run-summary" $summary | Out-Null

$summary | ConvertTo-Json -Depth 100
