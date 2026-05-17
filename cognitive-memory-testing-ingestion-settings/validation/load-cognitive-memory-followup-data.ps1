param(
    [string]$BaseUrl = "http://localhost:5032",
    [string]$StructurePath = "",
    [string]$EvidenceDirectory = "",
    [string]$BearerToken = "",
    [switch]$AllowNonPostgreSql
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Net.Http

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$bundleRoot = Split-Path -Parent $scriptRoot
if ([string]::IsNullOrWhiteSpace($StructurePath)) {
    $StructurePath = Join-Path $bundleRoot "sample-data\sample-projects.structure.json"
}

if ([string]::IsNullOrWhiteSpace($EvidenceDirectory)) {
    $EvidenceDirectory = Join-Path $scriptRoot "evidence"
}

$runId = Get-Date -Format "yyyyMMdd-HHmmss"
$runEvidenceDirectory = Join-Path $EvidenceDirectory $runId
New-Item -ItemType Directory -Path $runEvidenceDirectory -Force | Out-Null

$headers = @{
    "X-CanDoItAll-Agent-Id" = "codex-cognitive-memory-followup"
    "X-CanDoItAll-Agent-Name" = "Codex Cognitive Memory Followup Loader"
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

function Invoke-CdiaMultipartFile {
    param(
        [string]$Path,
        [string]$FilePath,
        [string]$ContentType,
        [string]$ProjectId,
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

        $form.Add([System.Net.Http.StringContent]::new("codex:cognitive-memory-followup"), "actorId")

        $response = $client.PostAsync($uri, $form).GetAwaiter().GetResult()
        $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        $payload = if ([string]::IsNullOrWhiteSpace($body)) { $null } else { $body | ConvertFrom-Json }

        Save-Evidence $EvidenceName @{
            ok = $response.IsSuccessStatusCode
            method = "POST"
            uri = $uri
            filePath = $FilePath
            statusCode = [int]$response.StatusCode
            response = $payload
        }

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

function Get-MarkdownProjectSection {
    param(
        [string]$Path,
        [string]$Heading
    )

    $content = [System.IO.File]::ReadAllText($Path)
    $pattern = "(?ms)^##\s+$([regex]::Escape($Heading))\s*\r?\n.*?(?=^##\s+|\z)"
    $match = [regex]::Match($content, $pattern)
    if (-not $match.Success) {
        throw "Markdown section '$Heading' was not found in '$Path'."
    }

    return "$($match.Value.Trim())`r`n"
}

function Get-LeadingSpaceCount {
    param([string]$Value)

    if ([string]::IsNullOrEmpty($Value)) {
        return 0
    }

    $index = 0
    while ($index -lt $Value.Length -and [char]::IsWhiteSpace($Value[$index])) {
        $index++
    }

    return $index
}

function Get-MermaidProjectBranch {
    param(
        [string]$Path,
        [string]$Branch
    )

    $lines = [System.IO.File]::ReadAllLines($Path)
    $branchIndex = -1
    for ($index = 0; $index -lt $lines.Count; $index++) {
        if ($lines[$index].Trim() -eq $Branch) {
            $branchIndex = $index
            break
        }
    }

    if ($branchIndex -lt 0) {
        throw "Mermaid branch '$Branch' was not found in '$Path'."
    }

    $branchIndent = Get-LeadingSpaceCount $lines[$branchIndex]
    $branchLines = New-Object System.Collections.Generic.List[string]
    for ($index = $branchIndex; $index -lt $lines.Count; $index++) {
        if ($index -gt $branchIndex -and -not [string]::IsNullOrWhiteSpace($lines[$index])) {
            $indent = Get-LeadingSpaceCount $lines[$index]
            if ($indent -le $branchIndent) {
                break
            }
        }

        $branchLines.Add($lines[$index])
    }

    $output = New-Object System.Collections.Generic.List[string]
    $output.Add("mindmap")
    $output.Add("  root((Cognitive Memory Follow-Up Sources))")
    foreach ($line in $branchLines) {
        $output.Add($line)
    }

    return ($output -join "`r`n") + "`r`n"
}

function New-ProjectSourceFiles {
    param(
        [object]$ProjectSpec,
        [string]$SourceDocumentPath,
        [string]$SourceMindmapPath,
        [string]$OutputDirectory
    )

    $documentHeading = Get-PropertyValue $ProjectSpec "documentHeading" $ProjectSpec.name
    $mindmapBranch = Get-PropertyValue $ProjectSpec "mindmapBranch" $ProjectSpec.name
    $sourceInputDirectory = Join-Path $OutputDirectory "source-inputs"
    New-Item -ItemType Directory -Path $sourceInputDirectory -Force | Out-Null

    $documentPath = Join-Path $sourceInputDirectory "$($ProjectSpec.key).md"
    $mindmapPath = Join-Path $sourceInputDirectory "$($ProjectSpec.key).mmd"
    Get-MarkdownProjectSection $SourceDocumentPath $documentHeading |
        Set-Content -LiteralPath $documentPath -Encoding UTF8
    Get-MermaidProjectBranch $SourceMindmapPath $mindmapBranch |
        Set-Content -LiteralPath $mindmapPath -Encoding UTF8

    return [pscustomobject]@{
        documentPath = $documentPath
        mindmapPath = $mindmapPath
        documentHeading = $documentHeading
        mindmapBranch = $mindmapBranch
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
        "Contains" { throw "Project-structure Contains links are system-managed. Use parent on the node, or choose a user-authored link kind." }
        "DependsOn" { return 1 }
        "Uses" { return 2 }
        "Validates" { return 3 }
        "Tests" { return 4 }
        "Blocks" { return 5 }
        "DerivedFrom" { return 6 }
        "BelongsTo" { throw "Project-structure BelongsTo links are system-managed. Use parent on the node, or choose a user-authored link kind." }
        default { throw "Unsupported project-structure link kind '$Kind'." }
    }
}

$manifest = Get-Content -Path $StructurePath -Raw | ConvertFrom-Json
$sampleDataRoot = Split-Path -Parent $StructurePath
$sourceDocumentPath = Join-Path $sampleDataRoot $manifest.sourceDocument
$sourceMindmapPath = Join-Path $sampleDataRoot $manifest.sourceMindmap

$accessStatus = Invoke-CdiaJson "GET" "/api/access/status" $null "00-access-status"
$databaseSelection = Invoke-CdiaJson "GET" "/api/cognitive-memory/database/selection" $null "01-database-selection"
$memoryStatus = Invoke-CdiaJson "GET" "/api/cognitive-memory/status" $null "02-cognitive-memory-status"

if (-not $AllowNonPostgreSql -and $memoryStatus.isPostgreSql -ne $true) {
    throw "Cognitive Memory follow-up data load requires PostgreSQL. Active provider is '$($memoryStatus.providerKindName)'."
}

$settings = Invoke-CdiaJson "PUT" "/api/cognitive-memory/settings" @{
    scheduleMode = "ScheduledMoments"
    nightlyLocalTime = "02:00"
    idleMinutes = 30
    scheduledLocalTimes = @("03:00", "16:30")
    autoIngestProjectStructure = $true
    autoIngestProcessRuntime = $true
    autoConsolidateAfterIngestion = $true
    actorId = "codex:cognitive-memory-followup"
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
}

$projectIndex = 0
foreach ($projectSpec in $manifest.projects) {
    $projectIndex++
    $prefix = "{0:D2}-{1}" -f $projectIndex, $projectSpec.key
    $projectSourceFiles = New-ProjectSourceFiles $projectSpec $sourceDocumentPath $sourceMindmapPath $runEvidenceDirectory
    $sourceDocumentMedia = ConvertTo-MediaPayload $projectSourceFiles.documentPath "text/markdown"
    $sourceMindmapMedia = ConvertTo-MediaPayload $projectSourceFiles.mindmapPath "text/plain"
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
        reason = "Load Cognitive Memory follow-up source data."
        durationMinutes = 30
    } "$prefix-02-lease"

    $nodeIds = @{}
    $rootNodeKey = "project:$($project.id)"

    $docNode = Invoke-CdiaJson "POST" "/api/project-structure/projects/$($project.id)/nodes" @{
        objectType = "File"
        title = "Follow-up sample source document"
        subtitle = "Markdown source"
        notes = "Loaded from cognitive-memory-testing-ingestion-settings/sample-data/sample-projects.md section '$($projectSourceFiles.documentHeading)'."
        parentNodeKey = $rootNodeKey
        x = 40
        y = 700
        objectSubtype = "markdown"
        media = $sourceDocumentMedia
        metadataJson = (@{
            sourceKind = "bundle-followup-document"
            projectKey = $projectSpec.key
        } | ConvertTo-Json -Compress)
        leaseToken = $lease.leaseToken
    } "$prefix-03-source-document"
    $nodeIds["source-document"] = $docNode.id

    $mindmapNode = Invoke-CdiaJson "POST" "/api/project-structure/projects/$($project.id)/nodes" @{
        objectType = "File"
        title = "Follow-up sample Mermaid mindmap"
        subtitle = "Mindmap source"
        notes = "Loaded from cognitive-memory-testing-ingestion-settings/sample-data/sample-projects.mmd branch '$($projectSourceFiles.mindmapBranch)'."
        parentNodeKey = $rootNodeKey
        x = 360
        y = 700
        objectSubtype = "mermaid"
        media = $sourceMindmapMedia
        metadataJson = (@{
            sourceKind = "bundle-followup-mindmap"
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

        $node = Invoke-CdiaJson "POST" "/api/project-structure/projects/$($project.id)/nodes" @{
            objectType = $nodeSpec.type
            title = $nodeSpec.title
            subtitle = $nodeSpec.subtitle
            notes = $nodeSpec.notes
            parentNodeKey = $parentNodeKey
            x = $nodeSpec.x
            y = $nodeSpec.y
            objectSubtype = $nodeSpec.subtype
            metadataJson = (@{
                sampleProjectKey = $projectSpec.key
                sampleNodeKey = $nodeSpec.key
                sourceBundle = "cognitive-memory-testing-ingestion-settings"
                purpose = "manual cognitive memory behavior validation"
            } | ConvertTo-Json -Compress)
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

    $externalDocument = Invoke-CdiaMultipartFile "/api/cognitive-memory/external-sources/files" $projectSourceFiles.documentPath "text/markdown" "$($project.id)" "$prefix-08-external-document"
    $externalMindmap = Invoke-CdiaMultipartFile "/api/cognitive-memory/external-sources/files" $projectSourceFiles.mindmapPath "text/plain" "$($project.id)" "$prefix-09-external-mindmap"

    $ingest = Invoke-CdiaJson "POST" "/api/cognitive-memory/ingestion/project-structure" @{
        scopeId = $project.id
        projectId = $project.id
        take = 250
        idempotencyKey = "cognitive-memory-followup:$($projectSpec.key):$($project.id):project-structure:v2"
    } "$prefix-10-ingest-project-structure"

    $consolidation = Invoke-CdiaJson "POST" "/api/cognitive-memory/consolidation/runs" @{
        projectId = $project.id
        mode = "IncrementalRecent"
        triggerKind = "Manual"
        idempotencyKey = "cognitive-memory-followup:$($projectSpec.key):$($project.id):consolidate:v2"
        profile = @{
            name = "developer-no-vector-projection"
            processSourceItems = $true
            detectContradictions = $true
            extractProcedures = $true
            rebuildProjections = $false
            createHumanReviewItems = $true
            maxItems = 100
        }
        policy = @{
            actorId = "codex:cognitive-memory-followup"
            accessLevel = "Restricted"
            policyProfileId = "developer-api"
            riskLevel = "Low"
            allowRestrictedContent = $true
        }
    } "$prefix-11-consolidation"

    $snapshot = Invoke-CdiaJson "GET" "/api/cognitive-memory/snapshot?projectId=$($project.id)&take=30" $null "$prefix-12-snapshot"

    $leaseRelease = Invoke-CdiaJson "POST" "/api/project-structure/leases/release" @{
        scopeKind = "Project"
        scopeKey = "$($project.id)"
        leaseToken = $lease.leaseToken
    } "$prefix-13-lease-release"

    $summary.projects += [ordered]@{
        key = $projectSpec.key
        projectId = $project.id
        nodeCount = $readback.nodes.Count
        linkCount = $readback.links.Count
        sourceDocumentFile = $projectSourceFiles.documentPath
        sourceMindmapFile = $projectSourceFiles.mindmapPath
        externalDocumentOperationId = $externalDocument.operationId
        externalMindmapOperationId = $externalMindmap.operationId
        ingest = $ingest
        consolidation = $consolidation
        snapshotSummary = $snapshot.summary
        leaseRelease = $leaseRelease
    }
}

$processIngest = Invoke-CdiaJson "POST" "/api/cognitive-memory/ingestion/processes" @{
    take = 250
    idempotencyKey = "cognitive-memory-followup:$runId:process-runtime:v2"
} "90-process-ingestion" -AllowFailure

$finalSnapshot = Invoke-CdiaJson "GET" "/api/cognitive-memory/snapshot?take=50" $null "91-final-snapshot"
$finalStatus = Invoke-CdiaJson "GET" "/api/cognitive-memory/status" $null "92-final-status"

$summary.processIngest = $processIngest
$summary.finalSnapshot = $finalSnapshot
$summary.finalStatus = $finalStatus
Save-Evidence "99-summary" $summary
$summary | ConvertTo-Json -Depth 80
