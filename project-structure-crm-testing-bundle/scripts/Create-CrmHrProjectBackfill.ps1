param(
    [Parameter(Mandatory = $true)]
    [string]$BaseUrl,

    [Parameter(Mandatory = $true)]
    [string]$Token,

    [string]$OutputPath = "C:\repositories\CanDoItAll\artifacts\project-structure-crm-testing\created-plan.json",

    [string]$SqliteDatabasePath = ""
)

$ErrorActionPreference = "Stop"

$agentHeaders = @{
    "X-CanDoItAll-Agent-Id" = "codex-crm-backfill"
    "X-CanDoItAll-Agent-Name" = "Codex CRM Testing Agent"
    "X-CanDoItAll-Agent-Machine" = $env:COMPUTERNAME
    "X-CanDoItAll-Agent-RepoRoot" = "C:\repositories\CanDoItAll"
    "X-CanDoItAll-Agent-Branch" = "local/testing"
    "X-CanDoItAll-Agent-Session" = ("crm-backfill-" + [guid]::NewGuid().ToString("N"))
    "X-CanDoItAll-Agent-Token" = $Token
}

$sourceBundleRoot = "C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final"
$sourceSubbundleRoot = Join-Path $sourceBundleRoot "subbundles"

function Invoke-ProjectStructureApi {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Method,

        [Parameter(Mandatory = $true)]
        [string]$Path,

        [object]$Body
    )

    $uri = "{0}{1}" -f $BaseUrl.TrimEnd("/"), $Path
    $requestParameters = @{
        Method = $Method
        Uri = $uri
        Headers = $agentHeaders
        ContentType = "application/json"
    }

    if ($null -ne $Body) {
        $requestParameters.Body = $Body | ConvertTo-Json -Depth 64
    }

    try {
        return Invoke-RestMethod @requestParameters
    } catch {
        $message = $_.Exception.Message
        if ($_.ErrorDetails -and -not [string]::IsNullOrWhiteSpace($_.ErrorDetails.Message)) {
            $message = "{0}`n{1}" -f $message, $_.ErrorDetails.Message
        }

        throw "Project-structure API call failed: $Method $uri`n$message"
    }
}

function Get-MarkdownSection {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Content,

        [Parameter(Mandatory = $true)]
        [string]$Heading
    )

    $escapedHeading = [regex]::Escape($Heading)
    $match = [regex]::Match(
        $Content,
        "(?ms)^##\s+$escapedHeading\s*\r?\n(?<body>.*?)(?=^##\s+|\z)")

    if (-not $match.Success) {
        return ""
    }

    return $match.Groups["body"].Value.Trim()
}

function Get-MarkdownBullets {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Content,

        [Parameter(Mandatory = $true)]
        [string]$Heading
    )

    $section = Get-MarkdownSection -Content $Content -Heading $Heading
    if ([string]::IsNullOrWhiteSpace($section)) {
        return @()
    }

    return @(
        $section
            -split "\r?\n"
            | ForEach-Object { $_.Trim() }
            | Where-Object { $_ -like "- *" }
            | ForEach-Object {
                $value = $_.Substring(2).Trim()
                $value = $value -replace "`0", ""
                $value = $value -replace "`r", ""
                $value = $value.Trim()
                if ($value.StartsWith('`') -and $value.EndsWith('`')) {
                    $value = $value.Trim('`')
                }

                $value
            }
    )
}

function New-OutlineNode {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Title,

        [string]$Notes = "",

        [object[]]$Children = @()
    )

    return @{
        title = $Title
        notes = $Notes
        children = $Children
    }
}

function Import-Outline {
    param(
        [Parameter(Mandatory = $true)]
        [guid]$ProjectId,

        [Parameter(Mandatory = $true)]
        [string]$Title,

        [Parameter(Mandatory = $true)]
        [object[]]$RootNodes,

        [string]$ContainerBlockSubtype = "delivery",

        [string]$LeafWorkItemSubtype = "task"
    )

    return Invoke-ProjectStructureApi -Method "Post" -Path "/api/project-structure-mcp/imports" -Body @{
        projectId = $ProjectId
        sourceKind = 3
        title = $Title
        sourceText = ($RootNodes | ConvertTo-Json -Depth 64 -Compress)
        containerBlockSubtype = $ContainerBlockSubtype
        leafWorkItemSubtype = $LeafWorkItemSubtype
    }
}

function Import-MermaidFlow {
    param(
        [Parameter(Mandatory = $true)]
        [guid]$ProjectId,

        [Parameter(Mandatory = $true)]
        [string]$Title,

        [Parameter(Mandatory = $true)]
        [string]$SourceText,

        [string]$ContainerBlockSubtype = "task-flow",

        [string]$LeafWorkItemSubtype = "task"
    )

    return Invoke-ProjectStructureApi -Method "Post" -Path "/api/project-structure-mcp/imports" -Body @{
        projectId = $ProjectId
        sourceKind = 0
        title = $Title
        sourceText = $SourceText
        containerBlockSubtype = $ContainerBlockSubtype
        leafWorkItemSubtype = $LeafWorkItemSubtype
    }
}

function New-Node {
    param(
        [Parameter(Mandatory = $true)]
        [guid]$ProjectId,

        [Parameter(Mandatory = $true)]
        [int]$ObjectType,

        [Parameter(Mandatory = $true)]
        [string]$Title,

        [string]$Subtitle = "",

        [string]$Notes = "",

        [string]$ParentNodeKey,

        [string]$ObjectSubtype = "",

        [string]$MetadataJson
    )

    $body = @{
        objectType = $ObjectType
        title = $Title
        subtitle = $Subtitle
        notes = $Notes
        parentNodeKey = $ParentNodeKey
        objectSubtype = $ObjectSubtype
    }

    if (-not [string]::IsNullOrWhiteSpace($MetadataJson)) {
        $body.metadataJson = $MetadataJson
    }

    return Invoke-ProjectStructureApi -Method "Post" -Path "/api/project-structure-mcp/projects/$ProjectId/nodes" -Body $body
}

function Convert-NodeIdToGuid {
    param(
        [Parameter(Mandatory = $true)]
        [string]$NodeId
    )

    if ($NodeId.StartsWith("custom:", [System.StringComparison]::OrdinalIgnoreCase)) {
        return [guid]$NodeId.Substring("custom:".Length)
    }

    return [guid]$NodeId
}

function Get-SubbundleData {
    $directories = Get-ChildItem -Path $sourceSubbundleRoot -Directory | Sort-Object Name
    $items = foreach ($directory in $directories) {
        $readmePath = Join-Path $directory.FullName "README.md"
        $content = Get-Content -Raw $readmePath
        $firstLine = (Get-Content $readmePath -TotalCount 1).Trim()
        $code = [regex]::Match($firstLine, "B\d{2}").Value
        $title = ($firstLine -replace "^#\s*", "").Trim()

        [PSCustomObject]@{
            Code = $code
            Title = $title
            DirectoryName = $directory.Name
            ReadmePath = $readmePath
            Objective = (Get-MarkdownBullets -Content $content -Heading "Objective" | Select-Object -First 1)
            CoveredInputs = Get-MarkdownBullets -Content $content -Heading "Covered Inputs"
            Prerequisites = Get-MarkdownBullets -Content $content -Heading "Prerequisites"
            Acceptance = Get-MarkdownBullets -Content $content -Heading "Acceptance Checklist"
            ProofRequired = Get-MarkdownBullets -Content $content -Heading "Proof Required"
            ValidationDepth = (Get-MarkdownBullets -Content $content -Heading "Validation Depth" | Select-Object -First 1)
            DependencyImpact = Get-MarkdownBullets -Content $content -Heading "Dependency Impact"
        }
    }

    return $items
}

function New-SubprojectStructure {
    param(
        [Parameter(Mandatory = $true)]
        [guid]$ProjectId,

        [Parameter(Mandatory = $true)]
        [pscustomobject]$Subbundle
    )

    $outline = @(
        (New-OutlineNode -Title "Objective" -Notes $Subbundle.Objective),
        (New-OutlineNode -Title "Acceptance scope" -Children (
            $Subbundle.Acceptance | Select-Object -First 6 | ForEach-Object {
                New-OutlineNode -Title $_
            }
        )),
        (New-OutlineNode -Title "Validation proof" -Children (
            $Subbundle.ProofRequired | Select-Object -First 4 | ForEach-Object {
                New-OutlineNode -Title $_
            }
        )),
        (New-OutlineNode -Title "Prerequisites" -Children (
            $Subbundle.Prerequisites | Select-Object -First 5 | ForEach-Object {
                New-OutlineNode -Title $_
            }
        ))
    )

    if ($Subbundle.DependencyImpact.Count -gt 0) {
        $outline += New-OutlineNode -Title "Dependency impact" -Children (
            $Subbundle.DependencyImpact | Select-Object -First 4 | ForEach-Object {
                New-OutlineNode -Title $_
            }
        )
    }

    return Import-Outline -ProjectId $ProjectId -Title "Backfilled execution plan" -RootNodes $outline -ContainerBlockSubtype "delivery" -LeafWorkItemSubtype "task"
}

function Add-CrmAiLane {
    param(
        [Parameter(Mandatory = $true)]
        [guid]$ProjectId
    )

    $lane = New-Node -ProjectId $ProjectId -ObjectType 3 -Title "CRM AI assurance lane" -Subtitle "Agentic ownership added for the backward plan" -Notes "Adds ideal AI-agent roles that would help steer CRM delivery, consistency, and validation." -ParentNodeKey ("project:{0}" -f $ProjectId) -ObjectSubtype "task-flow"

    $domainStewardMetadata = @{
        participant = @{
            participantKind = "aiAgent"
            role = "Unified account and contact model steward"
            organization = "CanDoItAll CRM backfill"
            email = "crm-domain-steward@local"
        }
    } | ConvertTo-Json -Depth 10 -Compress

    $relationshipMapperMetadata = @{
        participant = @{
            participantKind = "aiAgent"
            role = "Stakeholder and relationship mapping reviewer"
            organization = "CanDoItAll CRM backfill"
            email = "relationship-mapper@local"
        }
    } | ConvertTo-Json -Depth 10 -Compress

    $followUpGuardianMetadata = @{
        participant = @{
            participantKind = "aiAgent"
            role = "Interaction and overdue follow-up regression pilot"
            organization = "CanDoItAll CRM backfill"
            email = "follow-up-guardian@local"
        }
    } | ConvertTo-Json -Depth 10 -Compress

    $domainSteward = New-Node -ProjectId $ProjectId -ObjectType 7 -Title "CRM Domain Steward" -Subtitle "Model and route consistency" -Notes "Owns canonical projection checks for account and contact surfaces." -ParentNodeKey $lane.id -ObjectSubtype "ai-agent" -MetadataJson $domainStewardMetadata
    $relationshipMapper = New-Node -ProjectId $ProjectId -ObjectType 7 -Title "Relationship Mapper" -Subtitle "Stakeholder and participant fidelity" -Notes "Checks relationship roles, participant carry-through, and shared party semantics." -ParentNodeKey $lane.id -ObjectSubtype "ai-agent" -MetadataJson $relationshipMapperMetadata
    $followUpGuardian = New-Node -ProjectId $ProjectId -ObjectType 7 -Title "Follow-up Guardian" -Subtitle "Workflow regression and overdue action checks" -Notes "Exercises interaction logging, next-action ownership, and visibility of overdue work." -ParentNodeKey $lane.id -ObjectSubtype "ai-agent" -MetadataJson $followUpGuardianMetadata

    $tasks = @(
        @{
            Title = "Validate unified account and contact projections"
            Subtitle = "Task"
            Notes = "Verify that CRM routes reflect the unified party model without duplicate or split ownership behavior."
            Subtype = "task"
            Metadata = @{
                workItem = @{
                    workItemKind = "task"
                    assigneeParticipantArtifactId = (Convert-NodeIdToGuid -NodeId $domainSteward.id)
                    assigneePartyName = $domainSteward.title
                    description = "Model-level review of CRM account and contact projections."
                }
            }
        },
        @{
            Title = "Probe stakeholder role and interaction participant mapping"
            Subtitle = "Feedback"
            Notes = "Review stakeholder-role integrity, interaction participant persistence, and party linkage clarity."
            Subtype = "feedback"
            Metadata = @{
                workItem = @{
                    workItemKind = "feedback"
                    assigneeParticipantArtifactId = (Convert-NodeIdToGuid -NodeId $relationshipMapper.id)
                    assigneePartyName = $relationshipMapper.title
                    description = "Surface relationship ambiguities before they become user-facing data drift."
                }
            }
        },
        @{
            Title = "Exercise overdue follow-up and next-action workflow regressions"
            Subtitle = "Issue"
            Notes = "Run regression thinking against follow-up ownership, overdue surfacing, and operational route readiness."
            Subtype = "issue"
            Metadata = @{
                workItem = @{
                    workItemKind = "issue"
                    assigneeParticipantArtifactId = (Convert-NodeIdToGuid -NodeId $followUpGuardian.id)
                    assigneePartyName = $followUpGuardian.title
                    description = "Catch route or workflow gaps around next actions and overdue work."
                }
            }
        }
    )

    $createdTasks = foreach ($task in $tasks) {
        $metadataJson = $task.Metadata | ConvertTo-Json -Depth 10 -Compress
        New-Node -ProjectId $ProjectId -ObjectType 8 -Title $task.Title -Subtitle $task.Subtitle -Notes $task.Notes -ParentNodeKey $lane.id -ObjectSubtype $task.Subtype -MetadataJson $metadataJson
    }

    return @{
        Lane = $lane
        Participants = @($domainSteward, $relationshipMapper, $followUpGuardian)
        Tasks = $createdTasks
    }
}

$subbundles = Get-SubbundleData

$waveOutline = @(
    (New-OutlineNode -Title "Wave A - foundation" -Children @(
        (New-OutlineNode -Title "B01 Foundation"),
        (New-OutlineNode -Title "B02 Shell and core pages")
    )),
    (New-OutlineNode -Title "Wave B - directory, workforce, and agent base" -Children @(
        (New-OutlineNode -Title "B03 Directory detail and dedup"),
        (New-OutlineNode -Title "B06 Workforce and delivery units"),
        (New-OutlineNode -Title "B09 AI agent profiles")
    )),
    (New-OutlineNode -Title "Wave C - CRM and workbench integration" -Children @(
        (New-OutlineNode -Title "B04 CRM accounts and follow-ups"),
        (New-OutlineNode -Title "B10 Project and Workbench integration")
    )),
    (New-OutlineNode -Title "Wave D - conversion, staffing, and recruiting" -Children @(
        (New-OutlineNode -Title "B05 Opportunities and conversion"),
        (New-OutlineNode -Title "B07 Skills and allocations"),
        (New-OutlineNode -Title "B08 Recruiting lifecycle")
    )),
    (New-OutlineNode -Title "Wave E - integration, hardening, and rollout" -Children @(
        (New-OutlineNode -Title "B11 Cross-module integration"),
        (New-OutlineNode -Title "B12 Security and privacy"),
        (New-OutlineNode -Title "B13 Validation and rollout")
    )),
    (New-OutlineNode -Title "Control questions" -Children @(
        (New-OutlineNode -Title "Can the whole bundle be controlled from wave and dependency views?"),
        (New-OutlineNode -Title "Are CRM, HR, AI, and integration seams visible enough to manage risk?"),
        (New-OutlineNode -Title "Would later subbundles have enough structure to start safely from this plan?")
    ))
)

$dependencyFlowchart = @"
flowchart TD
B01[B01 Foundation] --> B02[B02 Shell and Core Pages]
B01[B01 Foundation] --> B03[B03 Directory Detail and Dedup]
B01[B01 Foundation] --> B06[B06 Workforce and Delivery Units]
B01[B01 Foundation] --> B09[B09 AI Agent Profiles]
B02[B02 Shell and Core Pages] --> B03[B03 Directory Detail and Dedup]
B02[B02 Shell and Core Pages] --> B06[B06 Workforce and Delivery Units]
B02[B02 Shell and Core Pages] --> B09[B09 AI Agent Profiles]
B03[B03 Directory Detail and Dedup] --> B04[B04 CRM Accounts and Follow-ups]
B03[B03 Directory Detail and Dedup] --> B10[B10 Project and Workbench Integration]
B03[B03 Directory Detail and Dedup] --> B08[B08 Recruiting Lifecycle]
B03[B03 Directory Detail and Dedup] --> B11[B11 Cross-module Integration]
B04[B04 CRM Accounts and Follow-ups] --> B05[B05 Opportunities and Conversion]
B04[B04 CRM Accounts and Follow-ups] --> B11[B11 Cross-module Integration]
B06[B06 Workforce and Delivery Units] --> B10[B10 Project and Workbench Integration]
B06[B06 Workforce and Delivery Units] --> B07[B07 Skills and Allocations]
B06[B06 Workforce and Delivery Units] --> B08[B08 Recruiting Lifecycle]
B09[B09 AI Agent Profiles] --> B10[B10 Project and Workbench Integration]
B10[B10 Project and Workbench Integration] --> B05[B05 Opportunities and Conversion]
B10[B10 Project and Workbench Integration] --> B07[B07 Skills and Allocations]
B10[B10 Project and Workbench Integration] --> B11[B11 Cross-module Integration]
B11[B11 Cross-module Integration] --> B12[B12 Security and Privacy]
B12[B12 Security and Privacy] --> B13[B13 Validation and Rollout]
B05[B05 Opportunities and Conversion] --> B13[B13 Validation and Rollout]
B07[B07 Skills and Allocations] --> B13[B13 Validation and Rollout]
B08[B08 Recruiting Lifecycle] --> B13[B13 Validation and Rollout]
B09[B09 AI Agent Profiles] --> B13[B13 Validation and Rollout]
"@

$controlOutline = @(
    (New-OutlineNode -Title "Critical foundations" -Children @(
        (New-OutlineNode -Title "B01 primary schema and identity foundation"),
        (New-OutlineNode -Title "B02 route and shell proof foundation"),
        (New-OutlineNode -Title "B03 directory fidelity foundation"),
        (New-OutlineNode -Title "B10 integration foundation")
    )),
    (New-OutlineNode -Title "Closure gates" -Children @(
        (New-OutlineNode -Title "Build and targeted test proof"),
        (New-OutlineNode -Title "Browser validation on live routes"),
        (New-OutlineNode -Title "Cross-module accountability and privacy review"),
        (New-OutlineNode -Title "Final rollout and regression confidence")
    ))
)

$umbrellaProject = Invoke-ProjectStructureApi -Method "Post" -Path "/api/project-structure-mcp/projects" -Body @{
    name = "CRM/HR Bundle Backfill Control Plan"
    description = "Backward-added project hierarchy reconstructed from the completed CanDoItAll_CrmHr_CodexBundle_Final bundle."
    objective = "Prove that delivered CRM/HR work can be represented as a manager-usable project structure, including wave control, dependencies, AI ownership, and later-bundle guidance."
    currentPhase = "Backward plan reconstruction and validation"
}

$waveImport = Import-Outline -ProjectId $umbrellaProject.id -Title "Wave scope map" -RootNodes $waveOutline -ContainerBlockSubtype "delivery" -LeafWorkItemSubtype "task"
$dependencyImport = Import-MermaidFlow -ProjectId $umbrellaProject.id -Title "Wave dependency map" -SourceText $dependencyFlowchart -ContainerBlockSubtype "task-flow" -LeafWorkItemSubtype "task"
$controlImport = Import-Outline -ProjectId $umbrellaProject.id -Title "Management controls" -RootNodes $controlOutline -ContainerBlockSubtype "task-flow" -LeafWorkItemSubtype "task"

$subprojectResults = New-Object System.Collections.Generic.List[object]

foreach ($subbundle in $subbundles) {
    $project = Invoke-ProjectStructureApi -Method "Post" -Path "/api/project-structure-mcp/projects" -Body @{
        name = $subbundle.Title
        description = "Backfilled subproject reconstructed from $($subbundle.Code) in CanDoItAll_CrmHr_CodexBundle_Final."
        objective = $subbundle.Objective
        currentPhase = "Backward plan reconstruction"
    }

    [void](Invoke-ProjectStructureApi -Method "Post" -Path "/api/project-structure-mcp/projects/$($umbrellaProject.id)/subprojects" -Body @{
        childProjectId = $project.id
    })

    $detailImport = New-SubprojectStructure -ProjectId $project.id -Subbundle $subbundle

    $subprojectEntry = [ordered]@{
        code = $subbundle.Code
        title = $subbundle.Title
        projectId = $project.id
        detailImport = $detailImport
        crmAiLane = $null
    }

    if ($subbundle.Code -eq "B04") {
        $subprojectEntry.crmAiLane = Add-CrmAiLane -ProjectId $project.id
    }

    $subprojectResults.Add([pscustomobject]$subprojectEntry)
}

$umbrellaStructure = Invoke-ProjectStructureApi -Method "Post" -Path "/api/project-structure-mcp/projects/$($umbrellaProject.id)/structure/read" -Body @{
    includeLinks = $true
    includeLayout = $true
    includeMetadata = $true
    includeNotes = $true
}

$analytics = Invoke-ProjectStructureApi -Method "Post" -Path "/api/project-structure-mcp/analytics/query" -Body @{
    projectId = $umbrellaProject.id
    take = 200
}

$outputDirectory = Split-Path -Parent $OutputPath
if (-not (Test-Path $outputDirectory)) {
    New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
}

$result = [ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow
    baseUrl = $BaseUrl
    umbrellaProjectId = $umbrellaProject.id
    umbrellaProjectName = $umbrellaProject.name
    waveScopeImport = $waveImport
    dependencyImport = $dependencyImport
    controlImport = $controlImport
    subprojects = $subprojectResults
    umbrellaStructureNodeCount = @($umbrellaStructure.nodes).Count
    umbrellaStructureLinkCount = @($umbrellaStructure.links).Count
    analyticsCount = @($analytics.entries).Count
}

$result | ConvertTo-Json -Depth 64 | Set-Content -Encoding utf8 -Path $OutputPath

if (-not [string]::IsNullOrWhiteSpace($SqliteDatabasePath) -and (Test-Path $SqliteDatabasePath)) {
    $repairScriptPath = Join-Path $PSScriptRoot "Repair-CrmHrAiAgents.ps1"
    $repairOutputPath = Join-Path (Split-Path -Parent $OutputPath) "crm-ai-agent-repair.json"
    & $repairScriptPath -DatabasePath $SqliteDatabasePath -CreatedPlanPath $OutputPath -OutputPath $repairOutputPath | Out-Null
}

Get-Content -Raw $OutputPath
