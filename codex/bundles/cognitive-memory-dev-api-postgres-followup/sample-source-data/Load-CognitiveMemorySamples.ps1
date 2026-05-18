param(
    [string]$BaseUrl = "http://127.0.0.1:5087",
    [string]$DataPath = (Join-Path $PSScriptRoot "sample-projects.json"),
    [string]$EvidenceDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) "evidence"),
    [string]$AccessToken = ""
)

$ErrorActionPreference = "Stop"

function New-ApiHeaders {
    $headers = @{
        "X-CanDoItAll-Agent-Id" = "codex-cognitive-memory-smoke"
        "X-CanDoItAll-Agent-Name" = "Codex Cognitive Memory Smoke"
        "X-CanDoItAll-Agent-Machine" = $env:COMPUTERNAME
        "X-CanDoItAll-Agent-RepoRoot" = "C:\repositories\CanDoItAll"
        "X-CanDoItAll-Agent-Branch" = "cognitive-memory"
        "X-CanDoItAll-Agent-Session" = [guid]::NewGuid().ToString("N")
    }

    if (-not [string]::IsNullOrWhiteSpace($AccessToken)) {
        $headers["Authorization"] = "Bearer $AccessToken"
    }

    return $headers
}

function Invoke-Api {
    param(
        [ValidateSet("GET", "POST", "PUT")]
        [string]$Method,
        [string]$Path,
        [object]$Body = $null,
        [hashtable]$Headers
    )

    $uri = "$($BaseUrl.TrimEnd('/'))$Path"
    try {
        if ($null -eq $Body) {
            return Invoke-RestMethod -Method $Method -Uri $uri -Headers $Headers -TimeoutSec 120
        }

        $json = $Body | ConvertTo-Json -Depth 32
        return Invoke-RestMethod -Method $Method -Uri $uri -Headers $Headers -Body $json -ContentType "application/json" -TimeoutSec 120
    } catch {
        Write-Error "API request failed: $Method $Path"
        if ($_.ErrorDetails.Message) {
            Write-Error $_.ErrorDetails.Message
        }

        throw
    }
}

function Get-PropertyValue {
    param(
        [object]$Object,
        [string[]]$Names
    )

    foreach ($name in $Names) {
        if ($Object.PSObject.Properties.Name -contains $name) {
            return $Object.$name
        }
    }

    return $null
}

function New-AssetMedia {
    param(
        [string]$FilePath,
        [string]$ContentType
    )

    return @{
        fileName = [System.IO.Path]::GetFileName($FilePath)
        contentType = $ContentType
        base64Data = [Convert]::ToBase64String([System.IO.File]::ReadAllBytes($FilePath))
    }
}

function Get-LinkKindValue {
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

New-Item -ItemType Directory -Force -Path $EvidenceDirectory | Out-Null

$headers = New-ApiHeaders
$status = Invoke-Api -Method GET -Path "/api/cognitive-memory/status" -Headers $headers
$isPostgreSql = Get-PropertyValue $status @("isPostgreSql", "IsPostgreSql")
if ($isPostgreSql -ne $true) {
    $provider = Get-PropertyValue $status @("providerKind", "ProviderKind")
    throw "Cognitive Memory smoke requires an active PostgreSQL profile. Current provider is '$provider'."
}

$samples = Get-Content -Raw -Path $DataPath | ConvertFrom-Json
$createdProjects = @()

foreach ($project in $samples.projects) {
    Write-Host "Creating project $($project.name)"
    $projectResponse = Invoke-Api -Method POST -Path "/api/project-structure/projects" -Headers $headers -Body @{
        name = $project.name
        description = $project.description
        objective = $project.objective
        currentPhase = $project.currentPhase
        status = 1
    }

    $projectId = Get-PropertyValue $projectResponse @("id", "Id")
    if ([string]::IsNullOrWhiteSpace([string]$projectId)) {
        throw "Project creation did not return an id for '$($project.name)'."
    }

    $nodeIds = @{}

    $sourceNode = Invoke-Api -Method POST -Path "/api/project-structure/projects/$projectId/nodes" -Headers $headers -Body @{
        objectType = "ProjectBlock"
        objectSubtype = "research"
        title = "Source documents and mindmap"
        subtitle = "Bundle source files loaded for cognitive memory smoke"
        notes = "This node groups the markdown and mermaid source documents used to seed Cognitive Memory for the $($project.name) sample."
        parentNodeKey = $null
        x = 40
        y = 760
        metadataJson = "{""sampleKey"":""$($project.key)"",""source"":""cognitive-memory-dev-api-postgres-followup""}"
    }
    $nodeIds["source-documents"] = Get-PropertyValue $sourceNode @("id", "Id")

    $markdownPath = Join-Path $PSScriptRoot $project.documents.markdown
    $mindmapPath = Join-Path $PSScriptRoot $project.documents.mindmap

    foreach ($asset in @(
        @{ key = "markdown-document"; path = $markdownPath; contentType = "text/markdown"; title = "Source markdown: $($project.name)" },
        @{ key = "mermaid-mindmap"; path = $mindmapPath; contentType = "text/vnd.mermaid"; title = "Mermaid mindmap: $($project.name)" }
    )) {
        $assetResponse = Invoke-Api -Method POST -Path "/api/project-structure/projects/$projectId/assets" -Headers $headers -Body @{
            objectType = 10
            objectSubtype = "document"
            title = $asset["title"]
            subtitle = "Bundle source asset"
            notes = (Get-Content -Raw -Path $asset["path"])
            parentNodeKey = $nodeIds["source-documents"]
            media = New-AssetMedia -FilePath $asset["path"] -ContentType $asset["contentType"]
            metadataJson = "{""sampleKey"":""$($project.key)"",""assetKind"":""$($asset["key"])""}"
        }

        $nodeIds[$asset["key"]] = Get-PropertyValue $assetResponse @("nodeId", "NodeId", "id", "Id")
    }

    foreach ($node in $project.nodes) {
        $parentNodeKey = $null
        if (-not [string]::IsNullOrWhiteSpace($node.parentKey)) {
            $parentNodeKey = $nodeIds[$node.parentKey]
        }

        $metadataJson = if ([string]::IsNullOrWhiteSpace($node.metadataJson)) {
            "{""sampleKey"":""$($project.key)"",""sourceNodeKey"":""$($node.key)""}"
        } else {
            $node.metadataJson
        }

        $nodeResponse = Invoke-Api -Method POST -Path "/api/project-structure/projects/$projectId/nodes" -Headers $headers -Body @{
            objectType = $node.objectType
            objectSubtype = $node.objectSubtype
            title = $node.title
            subtitle = $node.subtitle
            notes = $node.notes
            parentNodeKey = $parentNodeKey
            x = $node.x
            y = $node.y
            metadataJson = $metadataJson
        }

        $nodeIds[$node.key] = Get-PropertyValue $nodeResponse @("id", "Id")
    }

    Invoke-Api -Method POST -Path "/api/project-structure/projects/$projectId/links" -Headers $headers -Body @{
        sourceNodeId = $nodeIds["markdown-document"]
        targetNodeId = $nodeIds["mermaid-mindmap"]
        kind = (Get-LinkKindValue -Kind "DerivedFrom")
    } | Out-Null

    foreach ($link in $project.links) {
        Invoke-Api -Method POST -Path "/api/project-structure/projects/$projectId/links" -Headers $headers -Body @{
            sourceNodeId = $nodeIds[$link.source]
            targetNodeId = $nodeIds[$link.target]
            kind = (Get-LinkKindValue -Kind $link.kind)
        } | Out-Null
    }

    $readback = Invoke-Api -Method POST -Path "/api/project-structure/projects/$projectId/structure/read" -Headers $headers -Body @{
        includeLinks = $true
        includeMetadata = $true
        includeNotes = $true
        includeAssets = $true
        take = 500
    }

    $ingestionResults = @()
    $cursor = $null
    do {
        $ingestBody = @{
            sourceKind = "WorkbenchProjectStructure"
            scopeId = $projectId
            projectId = $projectId
            take = 250
            idempotencyKey = "cognitive-memory-smoke:$($project.key):ingest:$([guid]::NewGuid().ToString("N"))"
        }
        if ($null -ne $cursor) {
            $ingestBody.cursor = $cursor
        }

        $ingest = Invoke-Api -Method POST -Path "/api/cognitive-memory/sources/ingest" -Headers $headers -Body $ingestBody
        $ingestionResults += $ingest
        $cursor = Get-PropertyValue $ingest @("nextCursor", "NextCursor")
        $hasMore = Get-PropertyValue $ingest @("hasMore", "HasMore")
    } while ($hasMore -eq $true)

    $consolidation = Invoke-Api -Method POST -Path "/api/cognitive-memory/consolidation/runs" -Headers $headers -Body @{
        projectId = $projectId
        mode = "IncrementalRecent"
        triggerKind = "Manual"
        idempotencyKey = "cognitive-memory-smoke:$($project.key):consolidate:$([guid]::NewGuid().ToString("N"))"
        profile = @{
            name = "developer-no-vector-projection"
            processSourceItems = $true
            detectContradictions = $true
            extractProcedures = $false
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
    }

    $snapshot = Invoke-Api -Method GET -Path "/api/cognitive-memory/snapshot?projectId=$projectId&take=20" -Headers $headers

    $createdProjects += [pscustomobject]@{
        key = $project.key
        projectId = $projectId
        nodeCount = @($readback.nodes).Count
        linkCount = @($readback.links).Count
        ingestionResults = $ingestionResults
        consolidation = $consolidation
        snapshotSummary = $snapshot.summary
    }
}

$finalSnapshot = Invoke-Api -Method GET -Path "/api/cognitive-memory/snapshot?take=30" -Headers $headers
$evidence = [pscustomobject]@{
    capturedAtUtc = [DateTimeOffset]::UtcNow
    baseUrl = $BaseUrl
    status = $status
    projects = $createdProjects
    finalSnapshotSummary = $finalSnapshot.summary
}

$evidencePath = Join-Path $EvidenceDirectory ("cognitive-memory-sample-load-{0:yyyyMMdd-HHmmss}.json" -f [DateTime]::UtcNow)
$evidence | ConvertTo-Json -Depth 64 | Set-Content -Encoding UTF8 -Path $evidencePath
Write-Host "Evidence written to $evidencePath"
