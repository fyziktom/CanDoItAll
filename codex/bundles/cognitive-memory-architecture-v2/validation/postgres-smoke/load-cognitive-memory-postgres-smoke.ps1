param(
    [string]$BaseUrl = "http://localhost:5032",
    [string]$StructurePath = "",
    [string]$EvidenceDirectory = "",
    [string]$BearerToken = "",
    [switch]$AllowNonPostgreSql
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($StructurePath)) {
    $StructurePath = Join-Path $scriptRoot "sample-projects.structure.json"
}

if ([string]::IsNullOrWhiteSpace($EvidenceDirectory)) {
    $EvidenceDirectory = Join-Path $scriptRoot "evidence"
}

$runId = Get-Date -Format "yyyyMMdd-HHmmss"
$runEvidenceDirectory = Join-Path $EvidenceDirectory $runId
New-Item -ItemType Directory -Path $runEvidenceDirectory -Force | Out-Null

$headers = @{
    "X-CanDoItAll-Agent-Id" = "codex-cognitive-memory-smoke"
    "X-CanDoItAll-Agent-Name" = "Codex Cognitive Memory Smoke"
    "X-CanDoItAll-Agent-Machine" = $env:COMPUTERNAME
    "X-CanDoItAll-Agent-RepoRoot" = (Resolve-Path (Join-Path $scriptRoot "..\..\..\..\..")).Path
    "X-CanDoItAll-Agent-Branch" = "codex/cognitive-memory-architecture-v2"
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

    if ([string]::IsNullOrWhiteSpace($Name)) {
        return
    }

    $safeName = $Name -replace "[^A-Za-z0-9_.-]", "_"
    $path = Join-Path $runEvidenceDirectory "$safeName.json"
    $Payload | ConvertTo-Json -Depth 80 | Set-Content -Path $path -Encoding UTF8
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
    }

    if ($null -ne $Body) {
        $arguments["Body"] = ($Body | ConvertTo-Json -Depth 80)
    }

    try {
        $response = Invoke-RestMethod @arguments
        Save-Evidence $EvidenceName @{
            ok = $true
            method = $Method
            uri = $uri
            request = $Body
            response = $response
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
        Save-Evidence $EvidenceName $failure

        if (-not $AllowFailure) {
            throw
        }

        return $failure
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

function Get-PropertyValue {
    param(
        [object]$Value,
        [string]$PropertyName,
        [object]$DefaultValue = $null
    )

    if ($null -eq $Value) {
        return $DefaultValue
    }

    $property = $Value.PSObject.Properties[$PropertyName]
    if ($null -eq $property) {
        return $DefaultValue
    }

    return $property.Value
}

function ConvertTo-LinkKindValue {
    param([string]$Kind)

    switch ($Kind) {
        "Contains" { return 0 }
        "DependsOn" { return 1 }
        "Uses" { return 2 }
        "Validates" { return 3 }
        "Tests" { return 4 }
        "Blocks" { return 5 }
        "DerivedFrom" { return 6 }
        "BelongsTo" { return 7 }
        default { throw "Unsupported project-structure link kind '$Kind'." }
    }
}

$manifest = Get-Content -Path $StructurePath -Raw | ConvertFrom-Json
$sourceDocumentPath = Join-Path $scriptRoot $manifest.sourceDocument
$sourceMindmapPath = Join-Path $scriptRoot $manifest.sourceMindmap
$sourceDocumentMedia = ConvertTo-MediaPayload $sourceDocumentPath "text/markdown"
$sourceMindmapMedia = ConvertTo-MediaPayload $sourceMindmapPath "text/plain"

$accessStatus = Invoke-CdiaJson "GET" "/api/access/status" $null "00-access-status"
$databaseSelection = Invoke-CdiaJson "GET" "/_dev/database/selection" $null "01-database-selection" -AllowFailure
$memoryStatus = Invoke-CdiaJson "GET" "/api/cognitive-memory/status" $null "02-cognitive-memory-status"

if (-not $AllowNonPostgreSql -and $memoryStatus.isPostgreSql -ne $true) {
    throw "Cognitive Memory smoke requires PostgreSQL. Active provider is '$($memoryStatus.providerKindName)'."
}

$summary = [ordered]@{
    baseUrl = $BaseUrl
    runId = $runId
    evidenceDirectory = $runEvidenceDirectory
    accessStatus = $accessStatus
    databaseSelection = $databaseSelection
    cognitiveMemoryStatus = $memoryStatus
    projects = @()
}

$projectIndex = 0
foreach ($projectSpec in $manifest.projects) {
    $projectIndex++
    $prefix = "{0:D2}-{1}" -f $projectIndex, $projectSpec.key
    $project = Invoke-CdiaJson "POST" "/api/project-structure/projects" @{
        name = $projectSpec.name
        description = $projectSpec.description
        objective = $projectSpec.objective
        currentPhase = $projectSpec.currentPhase
        status = 1
    } "$prefix-01-project"

    $lease = Invoke-CdiaJson "POST" "/api/project-structure/leases/acquire" @{
        scopeKind = "Project"
        scopeKey = "$($project.id)"
        reason = "Load Cognitive Memory PostgreSQL smoke sample project."
        durationMinutes = 30
    } "$prefix-02-lease"

    $nodeIds = @{}
    $rootNodeKey = "project:$($project.id)"

    $docNode = Invoke-CdiaJson "POST" "/api/project-structure/projects/$($project.id)/nodes" @{
        objectType = "File"
        title = "Bundle sample source document"
        subtitle = "Markdown source"
        notes = "Loaded from validation/postgres-smoke/sample-projects.md for Cognitive Memory PostgreSQL smoke."
        parentNodeKey = $rootNodeKey
        x = 40
        y = 700
        objectSubtype = "markdown"
        media = $sourceDocumentMedia
        metadataJson = (@{
            sourceKind = "bundle-smoke-document"
            projectKey = $projectSpec.key
        } | ConvertTo-Json -Compress)
        leaseToken = $lease.leaseToken
    } "$prefix-03-source-document"
    $nodeIds["source-document"] = $docNode.id

    $mindmapNode = Invoke-CdiaJson "POST" "/api/project-structure/projects/$($project.id)/nodes" @{
        objectType = "File"
        title = "Bundle sample Mermaid mindmap"
        subtitle = "Mindmap source"
        notes = "Loaded from validation/postgres-smoke/sample-projects.mmd for Cognitive Memory PostgreSQL smoke."
        parentNodeKey = $rootNodeKey
        x = 360
        y = 700
        objectSubtype = "mermaid"
        media = $sourceMindmapMedia
        metadataJson = (@{
            sourceKind = "bundle-smoke-mindmap"
            projectKey = $projectSpec.key
        } | ConvertTo-Json -Compress)
        leaseToken = $lease.leaseToken
    } "$prefix-04-source-mindmap"
    $nodeIds["source-mindmap"] = $mindmapNode.id

    $nodeIndex = 0
    foreach ($nodeSpec in $projectSpec.nodes) {
        $nodeIndex++
        $parentKey = Get-PropertyValue $nodeSpec "parent" $null
        $parentNodeKey = if ([string]::IsNullOrWhiteSpace($parentKey)) {
            $rootNodeKey
        }
        else {
            $nodeIds[$parentKey]
        }

        $metadataJson = @{
            sampleProjectKey = $projectSpec.key
            sampleNodeKey = $nodeSpec.key
            sourceBundle = "cognitive-memory-architecture-v2"
            smokePurpose = "source-backed cognitive memory behavior validation"
        } | ConvertTo-Json -Compress

        $node = Invoke-CdiaJson "POST" "/api/project-structure/projects/$($project.id)/nodes" @{
            objectType = $nodeSpec.type
            title = $nodeSpec.title
            subtitle = $nodeSpec.subtitle
            notes = $nodeSpec.notes
            parentNodeKey = $parentNodeKey
            x = $nodeSpec.x
            y = $nodeSpec.y
            objectSubtype = $nodeSpec.subtype
            metadataJson = $metadataJson
            leaseToken = $lease.leaseToken
        } ("$prefix-05-node-{0:D2}-{1}" -f $nodeIndex, $nodeSpec.key)

        $nodeIds[$nodeSpec.key] = $node.id
    }

    $linkIndex = 0
    foreach ($linkSpec in $projectSpec.links) {
        $linkIndex++
        Invoke-CdiaJson "POST" "/api/project-structure/projects/$($project.id)/links" @{
            sourceNodeId = $nodeIds[$linkSpec.source]
            targetNodeId = $nodeIds[$linkSpec.target]
            kind = ConvertTo-LinkKindValue $linkSpec.kind
            leaseToken = $lease.leaseToken
        } ("$prefix-06-link-{0:D2}-{1}-to-{2}" -f $linkIndex, $linkSpec.source, $linkSpec.target)
    }

    $readback = Invoke-CdiaJson "POST" "/api/project-structure/projects/$($project.id)/structure/read" @{
        includeLinks = $true
        includeLayout = $true
        includeMetadata = $true
        includeNotes = $true
        includeAssets = $true
        take = 250
    } "$prefix-07-structure-readback"

    $ingest = Invoke-CdiaJson "POST" "/api/cognitive-memory/sources/ingest" @{
        sourceKind = "WorkbenchProjectStructure"
        scopeId = $project.id
        projectId = $project.id
        take = 250
        idempotencyKey = "cognitive-memory-postgres-smoke:$($projectSpec.key):ingest:v1"
    } "$prefix-08-ingest"

    $consolidation = Invoke-CdiaJson "POST" "/api/cognitive-memory/consolidation/runs" @{
        projectId = $project.id
        mode = "IncrementalRecent"
        triggerKind = "Manual"
        idempotencyKey = "cognitive-memory-postgres-smoke:$($projectSpec.key):consolidate:v1"
        profile = @{
            name = "developer-no-vector-projection"
            processSourceItems = $true
            detectContradictions = $true
            extractProcedures = $true
            rebuildProjections = $false
            createHumanReviewItems = $true
            maxItems = 80
        }
        policy = @{
            actorId = "codex:cognitive-memory-smoke"
            accessLevel = "Project"
            policyProfileId = "developer-api"
            riskLevel = "Low"
            allowRestrictedContent = $false
        }
    } "$prefix-09-consolidation"

    $snapshot = Invoke-CdiaJson "GET" "/api/cognitive-memory/snapshot?projectId=$($project.id)&take=25" $null "$prefix-10-snapshot"

    $selfRegulation = Invoke-CdiaJson "POST" "/api/cognitive-memory/self-regulation/assessments" @{
        projectId = $project.id
        actorId = "codex:cognitive-memory-smoke"
        modelProfileId = "gpt-5.5"
        roleKey = "developer"
        domainKey = $projectSpec.key
        taskTypeKey = "postgres-smoke"
        riskLevel = "Medium"
        sourceSufficiency = 0.46
        evidenceCoverage = 0.5
        contextFit = 0.55
        contradictionPressure = 0.22
        redactionPressure = 0.1
        cognitiveLoad = 0.35
        highImpact = $true
        recentCorrection = $false
        policy = @{
            actorId = "codex:cognitive-memory-smoke"
            accessLevel = "Project"
            policyProfileId = "developer-api"
            riskLevel = "Medium"
            allowRestrictedContent = $false
        }
    } "$prefix-11-self-regulation"

    $assessmentId = $selfRegulation.assessment.id
    $postureDecisionId = $selfRegulation.posture.id

    $answerGate = Invoke-CdiaJson "POST" "/api/cognitive-memory/answer-gate/decisions" @{
        projectId = $project.id
        actorId = "codex:cognitive-memory-smoke"
        selfRegulationAssessmentId = $assessmentId
        answerPostureDecisionId = $postureDecisionId
        sourceSufficiency = 0.46
        contextFit = 0.55
        evidenceSupport = 0.5
        contradictionPressure = 0.22
        stalenessPressure = 0.2
        redactionPressure = 0.1
        calibrationRisk = 0.42
        riskLevel = "Medium"
        procedureUnvalidated = $true
        professorReviewRequired = $true
        draftAnswerSummary = "PostgreSQL smoke draft answer for $($projectSpec.name)."
        policy = @{
            actorId = "codex:cognitive-memory-smoke"
            accessLevel = "Project"
            policyProfileId = "developer-api"
            riskLevel = "Medium"
            allowRestrictedContent = $false
        }
    } "$prefix-12-answer-gate"

    $professorReview = Invoke-CdiaJson "POST" "/api/cognitive-memory/professor-reviews" @{
        projectId = $project.id
        reviewMode = "SocraticChallenge"
        actorId = "codex:cognitive-memory-smoke"
        modelProfileId = "gpt-5.5"
        promptProfileVersion = "developer-api-v1"
        selfRegulationAssessmentId = $assessmentId
        answerPostureDecisionId = $postureDecisionId
        inputSummary = "Review whether the smoke answer keeps $($projectSpec.name) separate from unrelated sample projects."
        contextSummary = "Use source-backed project-structure summaries only. Do not treat generated summaries as source truth."
        suggestionKinds = @("ReviewItem", "Regression", "LearningProposal")
        policy = @{
            actorId = "codex:cognitive-memory-smoke"
            accessLevel = "Project"
            policyProfileId = "developer-api"
            riskLevel = "Medium"
            allowRestrictedContent = $false
        }
    } "$prefix-13-professor-review"

    $professorCompletion = Invoke-CdiaJson "POST" "/api/cognitive-memory/professor-reviews/$($professorReview.id)/complete" @{
        critique = "Smoke critique: keep project scope, source evidence, and answer posture explicit before relying on memory output."
        missingEvidence = "Require source links for any project-specific recommendation."
        recommendedPosture = "Caveated"
        suggestionKinds = @("ReviewItem", "Regression", "LearningProposal")
    } "$prefix-14-professor-review-complete"

    $epistemicScan = Invoke-CdiaJson "POST" "/api/cognitive-memory/epistemic-drive/scans" @{
        projectId = $project.id
        actorId = "codex:cognitive-memory-smoke"
        policy = @{
            actorId = "codex:cognitive-memory-smoke"
            accessLevel = "Project"
            policyProfileId = "developer-api"
            riskLevel = "Low"
            allowRestrictedContent = $false
        }
    } "$prefix-15-epistemic-scan"

    $learningDecision = $null
    if ($epistemicScan -is [System.Array] -and $epistemicScan.Count -gt 0) {
        $learningDecision = Invoke-CdiaJson "POST" "/api/cognitive-memory/epistemic-drive/proposals/$($epistemicScan[0].id)/decisions" @{
            decision = "Approved"
            actorId = "codex:cognitive-memory-smoke"
            notes = "Approved by PostgreSQL smoke to prove learning tasks remain approval-gated."
        } "$prefix-16-learning-decision"
    }
    elseif ($epistemicScan.id) {
        $learningDecision = Invoke-CdiaJson "POST" "/api/cognitive-memory/epistemic-drive/proposals/$($epistemicScan.id)/decisions" @{
            decision = "Approved"
            actorId = "codex:cognitive-memory-smoke"
            notes = "Approved by PostgreSQL smoke to prove learning tasks remain approval-gated."
        } "$prefix-16-learning-decision"
    }
    else {
        Save-Evidence "$prefix-16-learning-decision" @{
            ok = $true
            skipped = $true
            reason = "Epistemic scan did not create a learning proposal."
        }
    }

    $probeSession = Invoke-CdiaJson "POST" "/api/cognitive-memory/probes/sessions" @{
        projectId = $project.id
        title = "PostgreSQL smoke probe for $($projectSpec.name)"
        recallMode = "FocusedTaskContext"
        policy = @{
            actorId = "codex:cognitive-memory-smoke"
            accessLevel = "Project"
            policyProfileId = "developer-api"
            riskLevel = "Low"
            allowRestrictedContent = $false
        }
    } "$prefix-17-probe-session"

    $probeTurn = Invoke-CdiaJson "POST" "/api/cognitive-memory/probes/sessions/$($probeSession.id)/turns" @{
        question = "What source-backed constraints should shape the next implementation or planning decision for $($projectSpec.name)?"
        intent = "Architecture"
        budget = @{
            coarseCandidateLimit = 24
            graphExpansionDepth = 1
            vectorResultLimit = 8
            focusLimit = 8
            detailItemLimit = 8
            contextCharacterBudget = 12000
            maxSourceBytes = 24000
        }
        metadata = @{
            smoke = "postgres"
            projectKey = $projectSpec.key
        }
    } "$prefix-18-probe-turn" -AllowFailure

    $probeFeedback = $null
    if ($probeTurn.ok -eq $false) {
        Save-Evidence "$prefix-19-probe-feedback" @{
            ok = $true
            skipped = $true
            reason = "Probe ask did not return a turn. See probe-turn evidence for explicit error."
        }
    }
    else {
        $probeFeedback = Invoke-CdiaJson "POST" "/api/cognitive-memory/probes/turns/$($probeTurn.turn.id)/feedback" @{
            action = "WrongScope"
            notes = "Smoke correction intentionally checks that feedback creates review, regression, calibration, and findings instead of direct truth mutation."
            correctionText = "Keep $($projectSpec.name) facts separate from other sample projects and cite project-structure source nodes."
            riskLevel = "Medium"
            createRegressionTest = $true
            requestHumanReview = $true
            calibrationOutcome = "WrongScope"
        } "$prefix-19-probe-feedback"
    }

    $crossProject = $null
    if ($snapshot.memoryRecords -and $snapshot.memoryRecords.Count -gt 0) {
        $memoryRecordId = $snapshot.memoryRecords[0].id
        $crossProject = Invoke-CdiaJson "POST" "/api/cognitive-memory/cross-project/promotions" @{
            sourceMemoryRecordId = $memoryRecordId
            sourceProjectId = $project.id
            actorId = "codex:cognitive-memory-smoke"
            semanticSimilarity = 0.78
            entityEquivalence = 0.64
            contextSeparation = 0.72
            sourceReusePermission = 0.85
            policyCompatibility = 0.82
            reason = "PostgreSQL smoke candidate for source-backed reusable project-structure memory."
            policy = @{
                actorId = "codex:cognitive-memory-smoke"
                accessLevel = "Project"
                policyProfileId = "developer-api"
                riskLevel = "Low"
                allowRestrictedContent = $false
            }
        } "$prefix-20-cross-project-promotion"
    }
    else {
        Save-Evidence "$prefix-20-cross-project-promotion" @{
            ok = $true
            skipped = $true
            reason = "Snapshot did not contain memory records to promote."
        }
    }

    $worker = Invoke-CdiaJson "POST" "/api/cognitive-memory/distributed/workers" @{
        workerId = "codex-local-smoke"
        machineName = $env:COMPUTERNAME
        capabilities = @("ProjectionRebuild", "ReplayAnalysis")
    } "$prefix-21-distributed-worker"

    $job = Invoke-CdiaJson "POST" "/api/cognitive-memory/distributed/jobs" @{
        projectId = $project.id
        jobKind = "ProjectionRebuild"
        sourceScopeKey = "project:$($project.id)"
        inputPayloadJson = (@{
            projectId = "$($project.id)"
            profile = "developer-no-vector-projection"
            projectKey = $projectSpec.key
        } | ConvertTo-Json -Compress)
        expectedOutputSchema = "cognitive-memory.projection-result.v1"
        algorithmVersion = "developer-api-v1"
        policyProfileId = "developer-api"
    } "$prefix-22-distributed-job"

    $claimedJob = Invoke-CdiaJson "POST" "/api/cognitive-memory/distributed/jobs/claim" @{
        workerId = "codex-local-smoke"
        capabilities = @("ProjectionRebuild")
        leaseMinutes = 5
    } "$prefix-23-distributed-claim"

    $workerResult = Invoke-CdiaJson "POST" "/api/cognitive-memory/distributed/jobs/$($claimedJob.jobId)/results" @{
        workerId = "codex-local-smoke"
        leaseToken = $claimedJob.leaseToken
        inputHash = $claimedJob.inputHash
        outputPayloadJson = (@{
            status = "completed"
            recordsProjected = 0
            note = "Relational smoke did not rebuild vector projections."
        } | ConvertTo-Json -Compress)
        algorithmVersion = "developer-api-v1"
        outputSchema = "cognitive-memory.projection-result.v1"
    } "$prefix-24-distributed-result"

    $summary.projects += [ordered]@{
        key = $projectSpec.key
        projectId = $project.id
        nodeCount = $readback.nodes.Count
        linkCount = $readback.links.Count
        ingest = $ingest
        consolidation = $consolidation
        snapshotSummary = $snapshot.summary
        selfRegulationAssessmentId = $assessmentId
        answerGateDecisionId = $answerGate.id
        professorReviewId = $professorReview.id
        learningDecisionId = $learningDecision.id
        probeSessionId = $probeSession.id
        crossProjectCandidateId = $crossProject.id
        distributedJobId = $job.id
        distributedResultId = $workerResult.id
    }
}

Save-Evidence "99-summary" $summary
$summary | ConvertTo-Json -Depth 80
