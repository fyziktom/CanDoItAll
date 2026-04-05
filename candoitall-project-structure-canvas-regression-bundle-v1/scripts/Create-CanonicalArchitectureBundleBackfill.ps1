param(
    [Parameter(Mandatory = $true)]
    [string]$BaseUrl,

    [Parameter(Mandatory = $true)]
    [string]$Token,

    [string]$OutputPath = "C:\repositories\CanDoItAll\artifacts\canvas-regression-bundle-v1-fresh-validation\created-plan.json",

    [string]$SqliteDatabasePath = ""
)

$ErrorActionPreference = "Stop"

$agentHeaders = @{
    "X-CanDoItAll-Agent-Id" = "codex-canonical-bundle-backfill"
    "X-CanDoItAll-Agent-Name" = "Codex Canonical Bundle Backfill Agent"
    "X-CanDoItAll-Agent-Machine" = $env:COMPUTERNAME
    "X-CanDoItAll-Agent-RepoRoot" = "C:\repositories\CanDoItAll"
    "X-CanDoItAll-Agent-Branch" = "local/testing"
    "X-CanDoItAll-Agent-Session" = ("canonical-bundle-backfill-" + [guid]::NewGuid().ToString("N"))
    "X-CanDoItAll-Agent-Token" = $Token
}

$sourceBundleRoot = "C:\repositories\CanDoItAll\candoitall-canonical-architecture-review-bundle-v2"
$sourceSubbundleRoot = Join-Path $sourceBundleRoot "subbundles"
$scheduleAnchor = [DateTimeOffset]::Parse("2026-04-06T09:00:00-04:00")

function Convert-ToAsciiText {
    param(
        [AllowNull()]
        [string]$Value
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return ""
    }

    $normalized = $Value `
        -replace "[\u2013\u2014]", "-" `
        -replace "[\u2192\u21A6]", "->" `
        -replace "[\u2018\u2019]", "'" `
        -replace "[\u201C\u201D]", '"'

    return ([regex]::Replace($normalized, "[^\u0000-\u007F]", "")).Trim()
}

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

    if ($Method -ne "Get") {
        $requestParameters.Headers["X-CanDoItAll-Estimated-Minutes"] = "5"
    }

    try {
        return Invoke-RestMethod @requestParameters
    } catch {
        $message = $_.Exception.Message
        if ($_.ErrorDetails -and -not [string]::IsNullOrWhiteSpace($_.ErrorDetails.Message)) {
            $message = "{0}`n{1}" -f $message, $_.ErrorDetails.Message
        }

        if ($_.Exception.Response) {
            try {
                $responseStream = $_.Exception.Response.GetResponseStream()
                if ($null -ne $responseStream) {
                    $reader = New-Object System.IO.StreamReader($responseStream)
                    $responseBody = $reader.ReadToEnd()
                    if (-not [string]::IsNullOrWhiteSpace($responseBody) -and $message -notlike "*$responseBody*") {
                        $message = "{0}`n{1}" -f $message, $responseBody
                    }
                }
            } catch {
            }
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

    return Convert-ToAsciiText $match.Groups["body"].Value.Trim()
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

    $lines = $section -split "\r?\n"
    $trimmed = $lines | ForEach-Object { $_.Trim() }
    $bullets = $trimmed | Where-Object { $_ -like "- *" } | ForEach-Object { Convert-ToAsciiText $_.Substring(2).Trim() }
    return @($bullets)
}

function Get-BulletMetadataValue {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Content,

        [Parameter(Mandatory = $true)]
        [string]$Label
    )

    $pattern = "^- " + [regex]::Escape($Label) + ":\s*(?<value>.+)$"
    $match = [regex]::Match($Content, $pattern, [System.Text.RegularExpressions.RegexOptions]::Multiline)
    if (-not $match.Success) {
        return ""
    }

    $value = $match.Groups["value"].Value.Trim()
    $value = $value -replace "\*\*", ""
    return Convert-ToAsciiText $value.Trim()
}

function New-OutlineNode {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Title,

        [string]$Notes = "",

        [object[]]$Children = @()
    )

    return @{
        title = Convert-ToAsciiText $Title
        notes = Convert-ToAsciiText $Notes
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

    $normalizedNodes = New-Object System.Collections.Generic.List[object]
    foreach ($rootNode in $RootNodes) {
        $normalizedNodes.Add($rootNode)
    }

    return Invoke-ProjectStructureApi -Method "Post" -Path "/api/project-structure-mcp/imports" -Body @{
        projectId = $ProjectId
        sourceKind = "JsonOutline"
        title = $Title
        sourceText = (ConvertTo-Json -InputObject $normalizedNodes.ToArray() -Depth 64 -Compress)
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
        sourceKind = "Mermaid"
        title = $Title
        sourceText = $SourceText
        containerBlockSubtype = $ContainerBlockSubtype
        leafWorkItemSubtype = $LeafWorkItemSubtype
    }
}

function Get-DraftChildren {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Draft
    )

    $value = if ($Draft -is [System.Collections.IDictionary]) {
        $Draft["children"]
    } else {
        $Draft.children
    }

    if ($null -eq $value) {
        return @()
    }

    if ($value -is [System.Array]) {
        return @($value)
    }

    if ($value -is [System.Collections.IEnumerable] -and -not ($value -is [string])) {
        return @($value | ForEach-Object { $_ })
    }

    return @($value)
}

function Get-DraftValue {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Draft,

        [Parameter(Mandatory = $true)]
        [string]$Key
    )

    if ($Draft -is [System.Collections.IDictionary]) {
        return [string]$Draft[$Key]
    }

    return [string]$Draft.$Key
}

function Add-OutlineNodes {
    param(
        [Parameter(Mandatory = $true)]
        [guid]$ProjectId,

        [Parameter(Mandatory = $true)]
        [object[]]$Drafts,

        [Parameter(Mandatory = $true)]
        [string]$ParentNodeKey,

        [Parameter(Mandatory = $true)]
        [System.Collections.Generic.List[string]]$CreatedNodeIds,

        [Parameter(Mandatory = $true)]
        [string]$ContainerBlockSubtype,

        [Parameter(Mandatory = $true)]
        [string]$LeafWorkItemSubtype
    )

    foreach ($draft in $Drafts) {
        if ($null -eq $draft) {
            continue
        }

        if ($draft -is [System.Array]) {
            Add-OutlineNodes -ProjectId $ProjectId -Drafts @($draft) -ParentNodeKey $ParentNodeKey -CreatedNodeIds $CreatedNodeIds -ContainerBlockSubtype $ContainerBlockSubtype -LeafWorkItemSubtype $LeafWorkItemSubtype
            continue
        }

        $title = Get-DraftValue -Draft $draft -Key "title"
        if ([string]::IsNullOrWhiteSpace($title)) {
            continue
        }

        $children = Get-DraftChildren -Draft $draft
        $hasChildren = $children.Count -gt 0
        $node = New-Node -ProjectId $ProjectId -ObjectType $(if ($hasChildren) { 3 } else { 8 }) -Title $title -Subtitle $(if ($hasChildren) { "Block" } else { "Task" }) -Notes (Get-DraftValue -Draft $draft -Key "notes") -ParentNodeKey $ParentNodeKey -ObjectSubtype $(if ($hasChildren) { $ContainerBlockSubtype } else { $LeafWorkItemSubtype })
        $CreatedNodeIds.Add([string]$node.id)

        if ($hasChildren) {
            Add-OutlineNodes -ProjectId $ProjectId -Drafts $children -ParentNodeKey $node.id -CreatedNodeIds $CreatedNodeIds -ContainerBlockSubtype $ContainerBlockSubtype -LeafWorkItemSubtype $LeafWorkItemSubtype
        }
    }
}

function Create-OutlineGraph {
    param(
        [Parameter(Mandatory = $true)]
        [guid]$ProjectId,

        [Parameter(Mandatory = $true)]
        [string]$Title,

        [object[]]$RootNodes,

        [string]$ContainerBlockSubtype = "delivery",

        [string]$LeafWorkItemSubtype = "task"
    )

    $container = New-Node -ProjectId $ProjectId -ObjectType 3 -Title $Title -Subtitle "Structured plan" -Notes "Created directly through the project-structure mutation path for the fresh validation run." -ParentNodeKey ("project:{0}" -f $ProjectId) -ObjectSubtype $ContainerBlockSubtype
    $createdNodeIds = New-Object System.Collections.Generic.List[string]
    $createdNodeIds.Add([string]$container.id)
    $effectiveRootNodes = @($RootNodes)
    if ($effectiveRootNodes.Count -eq 0) {
        $effectiveRootNodes = @(
            (New-OutlineNode -Title "Pending detail" -Notes "This outline produced no root nodes during the fresh validation run and needs manual follow-up.")
        )
    }

    Add-OutlineNodes -ProjectId $ProjectId -Drafts $effectiveRootNodes -ParentNodeKey $container.id -CreatedNodeIds $createdNodeIds -ContainerBlockSubtype $ContainerBlockSubtype -LeafWorkItemSubtype $LeafWorkItemSubtype

    return @{
        projectId = $ProjectId
        containerNodeId = $container.id
        sourceNodeId = $null
        createdNodeIds = $createdNodeIds.ToArray()
        warnings = @()
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

        [string]$MetadataJson,

        [DateTimeOffset]$StartUtc,

        [DateTimeOffset]$EndUtc,

        [int]$DurationSeconds
    )

    $body = @{
        objectType = $ObjectType
        title = Convert-ToAsciiText $Title
        subtitle = Convert-ToAsciiText $Subtitle
        notes = Convert-ToAsciiText $Notes
        parentNodeKey = $ParentNodeKey
        objectSubtype = $ObjectSubtype
    }

    if (-not [string]::IsNullOrWhiteSpace($MetadataJson)) {
        $body.metadataJson = $MetadataJson
    }

    if ($PSBoundParameters.ContainsKey("StartUtc")) {
        $body.startUtc = $StartUtc.ToString("o")
    }

    if ($PSBoundParameters.ContainsKey("EndUtc")) {
        $body.endUtc = $EndUtc.ToString("o")
    }

    if ($PSBoundParameters.ContainsKey("DurationSeconds")) {
        $body.durationSeconds = $DurationSeconds
    }

    return Invoke-ProjectStructureApi -Method "Post" -Path "/api/project-structure-mcp/projects/$ProjectId/nodes" -Body $body
}

function Read-Structure {
    param(
        [Parameter(Mandatory = $true)]
        [guid]$ProjectId
    )

    return Invoke-ProjectStructureApi -Method "Post" -Path "/api/project-structure-mcp/projects/$ProjectId/structure/read" -Body @{
        includeLayout = $true
        includeMetadata = $true
        includeLinks = $true
        take = 500
    }
}

function Recompose-ProjectRoot {
    param(
        [Parameter(Mandatory = $true)]
        [guid]$ProjectId
    )

    return Invoke-ProjectStructureApi -Method "Post" -Path "/api/project-structure-mcp/projects/$ProjectId/nodes/recompose" -Body @{
        rootNodeId = "project:$ProjectId"
    }
}

function Ensure-Directory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $directory = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($directory) -and -not (Test-Path $directory)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }
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

function Add-BusinessDays {
    param(
        [Parameter(Mandatory = $true)]
        [DateTimeOffset]$Start,

        [Parameter(Mandatory = $true)]
        [int]$Days
    )

    $current = $Start
    $remaining = $Days
    while ($remaining -gt 0) {
        $current = $current.AddDays(1)
        if ($current.DayOfWeek -notin @([DayOfWeek]::Saturday, [DayOfWeek]::Sunday)) {
            $remaining--
        }
    }

    return $current
}

function Get-DependencyCodes {
    param(
        [AllowNull()]
        [string]$Text
    )

    if ([string]::IsNullOrWhiteSpace($Text)) {
        return @()
    }

    $matches = [regex]::Matches($Text, "ACR-\d{3}") | ForEach-Object { $_.Value } | Select-Object -Unique
    return @($matches)
}

function Get-PhaseRows {
    $planPath = Join-Path $sourceBundleRoot "plan\01-phase-plan.md"
    $lines = Get-Content -Path $planPath -Encoding UTF8
    $rows = New-Object System.Collections.Generic.List[object]

    foreach ($line in $lines) {
        if ($line -notmatch "^\| Phase \d ") {
            continue
        }

        $parts = @(
            $line.Trim('|').Split('|') | ForEach-Object { $_.Trim() }
        )

        if ($parts.Count -lt 5) {
            continue
        }

        $rows.Add([pscustomobject]@{
            Name = Convert-ToAsciiText $parts[0]
            Goal = Convert-ToAsciiText $parts[1]
            Findings = Convert-ToAsciiText $parts[2]
            Closure = Convert-ToAsciiText $parts[3]
            Gate = Convert-ToAsciiText $parts[4]
        })
    }

    return $rows.ToArray()
}

function Get-SubbundleData {
    $directories = Get-ChildItem -Path $sourceSubbundleRoot -Directory | Sort-Object Name
    $items = foreach ($directory in $directories) {
        $readmePath = Join-Path $directory.FullName "README.md"
        $content = Get-Content -Path $readmePath -Raw -Encoding UTF8
        $headingLine = @(
            Get-Content -Path $readmePath -Encoding UTF8 | Where-Object { $_ -match "^\s*#" } | Select-Object -First 1
        )[0]
        $headingLine = [string]$headingLine
        $code = ([regex]::Match($headingLine, "ACR-\d{3}")).Value
        if ([string]::IsNullOrWhiteSpace($code)) {
            $code = ([regex]::Match($directory.Name, "ACR-\d{3}")).Value
        }

        $titleSource = $headingLine -replace "^\s*#\s*", ""
        if (-not [string]::IsNullOrWhiteSpace($code)) {
            $titleSource = $titleSource -replace [regex]::Escape($code), ""
        }

        $title = Convert-ToAsciiText $titleSource
        $title = $title.TrimStart([char[]]@(' ', '-', ':', '.'))
        if ([string]::IsNullOrWhiteSpace($title)) {
            $directoryTitle = $directory.Name
            if (-not [string]::IsNullOrWhiteSpace($code)) {
                $directoryTitle = $directoryTitle -replace ('^{0}-?' -f [regex]::Escape($code)), ""
            }

            $title = Convert-ToAsciiText ($directoryTitle.Replace('-', ' '))
        }

        $dependencyText = Get-BulletMetadataValue -Content $content -Label "Dependencies"

        [pscustomobject]@{
            Code = Convert-ToAsciiText $code
            Title = $title.Trim()
            DirectoryName = $directory.Name
            ReadmePath = $readmePath
            Phase = Get-BulletMetadataValue -Content $content -Label "Phase"
            Severity = Get-BulletMetadataValue -Content $content -Label "Severity"
            Category = Get-BulletMetadataValue -Content $content -Label "Category"
            Timing = Get-BulletMetadataValue -Content $content -Label "Timing"
            DependencyText = $dependencyText
            Dependencies = Get-DependencyCodes -Text $dependencyText
            ProblemStatement = Get-MarkdownSection -Content $content -Heading "Problem statement"
            WhyNow = Get-MarkdownSection -Content $content -Heading "Why this matters now"
            Deliverables = Get-MarkdownBullets -Content $content -Heading "Deliverables"
            LikelyFilesTouched = Get-MarkdownBullets -Content $content -Heading "Likely files touched"
        }
    }

    return @($items)
}

function Get-SeverityDurationDays {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Severity
    )

    switch ($Severity) {
        "Critical" { return 4 }
        "High" { return 3 }
        "Medium" { return 2 }
        default { return 3 }
    }
}

function Get-AgentCatalog {
    return @(
        [ordered]@{
            DisplayName = "Generic Codex Delivery Agent"
            ExternalCode = "GENERIC-CODEX-DELIVERY"
            Role = "Minimal implementation and repair delivery"
            DefaultModel = "gpt-5.4"
            Skills = @("bundle-execution", "implementation", "minimal repair")
            Summary = "Generic delivery agent for smallest-correct code and documentation changes."
            CapabilityNames = @("Implementation", "Bundle execution")
        },
        [ordered]@{
            DisplayName = "Canonical Model Steward"
            ExternalCode = "CANONICAL-MODEL-STEWARD"
            Role = "Canonical ownership and source-of-truth review"
            DefaultModel = "gpt-5.4"
            Skills = @("canonical-model-review", "ownership-boundaries", "semantic governance")
            Summary = "Reviews source-of-truth seams, canonical owners, and semantic drift."
            CapabilityNames = @("Canonical model review", "Ownership seam audit")
        },
        [ordered]@{
            DisplayName = "Graph Integrity Auditor"
            ExternalCode = "GRAPH-INTEGRITY-AUDITOR"
            Role = "Hierarchy, relation, and invariant enforcement"
            DefaultModel = "gpt-5.4"
            Skills = @("graph-invariant-review", "hierarchy-safety", "relation-integrity")
            Summary = "Covers hierarchy validity, relation semantics, and graph-shape invariants."
            CapabilityNames = @("Hierarchy invariant review", "Relation integrity audit")
        },
        [ordered]@{
            DisplayName = "Projection Read-Model Agent"
            ExternalCode = "PROJECTION-READMODEL-AGENT"
            Role = "Projection, assembler, and read-model validation"
            DefaultModel = "gpt-5.4"
            Skills = @("projection-equivalence", "read-model-assembly", "calendar-gantt-review")
            Summary = "Validates read models, projection equivalence, and assembled graph seams."
            CapabilityNames = @("Projection equivalence review", "Read-model audit")
        },
        [ordered]@{
            DisplayName = "Cross-Module Ownership Agent"
            ExternalCode = "CROSS-MODULE-OWNERSHIP"
            Role = "Cross-module actor and responsibility integration"
            DefaultModel = "gpt-5.4"
            Skills = @("cross-module-responsibility", "actor-ownership", "assignment-integrity")
            Summary = "Validates cross-module actor ownership, assignment scope, and lifecycle carry-through."
            CapabilityNames = @("Responsibility model audit", "Assignment integrity review")
        },
        [ordered]@{
            DisplayName = "Runtime Validation Agent"
            ExternalCode = "RUNTIME-VALIDATION-AGENT"
            Role = "Regression proof, build, and browser validation"
            DefaultModel = "gpt-5.4"
            Skills = @("playwright-validation", "integration-testing", "bundle-proof")
            Summary = "Runs proof-oriented build, test, and browser validation against repaired flows."
            CapabilityNames = @("Regression validation", "Browser proof")
        }
    )
}

function Get-AssignedAgents {
    param(
        [Parameter(Mandatory = $true)]
        [pscustomobject]$Subbundle,

        [Parameter(Mandatory = $true)]
        [hashtable]$CatalogByName
    )

    $selectedNames = New-Object System.Collections.Generic.List[string]
    $selectedNames.Add("Generic Codex Delivery Agent")

    if ($Subbundle.Phase -eq "Phase 0") {
        $selectedNames.Add("Graph Integrity Auditor")
    } elseif ($Subbundle.Phase -eq "Phase 1") {
        $selectedNames.Add("Canonical Model Steward")
    } elseif ($Subbundle.Phase -eq "Phase 2") {
        $selectedNames.Add("Projection Read-Model Agent")
    } elseif ($Subbundle.Phase -eq "Phase 3") {
        $selectedNames.Add("Cross-Module Ownership Agent")
    } else {
        $selectedNames.Add("Runtime Validation Agent")
    }

    if ($Subbundle.Category -like "*Source-of-truth*") {
        $selectedNames.Add("Canonical Model Steward")
    }

    if ($Subbundle.DependencyText -like "*actor*" -or $Subbundle.DependencyText -like "*assignment*") {
        $selectedNames.Add("Cross-Module Ownership Agent")
    }

    if ($Subbundle.Code -in @("ACR-005", "ACR-011", "ACR-013")) {
        $selectedNames.Add("Graph Integrity Auditor")
    }

    $selectedNames.Add("Runtime Validation Agent")

    $uniqueNames = $selectedNames | Select-Object -Unique
    $agents = $uniqueNames | ForEach-Object { $CatalogByName[$_] }
    return @($agents)
}

function Add-ExecutionLane {
    param(
        [Parameter(Mandatory = $true)]
        [guid]$ProjectId,

        [Parameter(Mandatory = $true)]
        [pscustomobject]$Subbundle,

        [Parameter(Mandatory = $true)]
        [DateTimeOffset]$StartUtc,

        [Parameter(Mandatory = $true)]
        [DateTimeOffset]$EndUtc,

        [Parameter(Mandatory = $true)]
        [object[]]$Agents
    )

    $lane = New-Node -ProjectId $ProjectId -ObjectType 3 -Title "AI execution lane" -Subtitle "Assigned skill agents and execution windows" -Notes "Adds explicit AI-agent owners, skills, and timing windows so the imported plan is execution-controllable instead of only descriptive." -ParentNodeKey ("project:{0}" -f $ProjectId) -ObjectSubtype "task-flow"

    $participants = foreach ($agent in $Agents) {
        $metadataJson = @{
            participant = @{
                participantKind = "aiAgent"
                role = $agent.Role
                organization = "CanDoItAll canonical bundle backfill"
                email = ("{0}@local" -f $agent.ExternalCode.ToLowerInvariant())
                linkedPartyName = $agent.DisplayName
                skills = $agent.Skills
                externalCode = $agent.ExternalCode
            }
        } | ConvertTo-Json -Depth 10 -Compress

        New-Node -ProjectId $ProjectId -ObjectType 7 -Title $agent.DisplayName -Subtitle $agent.Role -Notes $agent.Summary -ParentNodeKey $lane.id -ObjectSubtype "ai-agent" -MetadataJson $metadataJson
    }

    $primaryAgent = @($Agents | Where-Object { $_.DisplayName -ne "Generic Codex Delivery Agent" })[0]
    if ($null -eq $primaryAgent) {
        $primaryAgent = @($Agents)[0]
    }

    $validatorAgent = @($Agents | Where-Object { $_.DisplayName -eq "Runtime Validation Agent" })[0]
    if ($null -eq $validatorAgent) {
        $validatorAgent = @($Agents)[-1]
    }

    $taskBlueprints = @(
        @{
            Title = "Clarify $($Subbundle.Code) scope and semantic owner"
            Subtitle = "Clarification"
            Notes = "Confirm scope boundaries, dependency truth, and the canonical owner before implementation work starts."
            Subtype = "task"
            Agent = $primaryAgent
            StartUtc = $StartUtc
            EndUtc = $StartUtc.AddHours(4)
            DurationSeconds = 14400
        },
        @{
            Title = "Implement minimal stabilization for $($Subbundle.Code)"
            Subtitle = "Execution"
            Notes = "Ship the smallest correct code or structural change that closes the finding without widening scope."
            Subtype = "task"
            Agent = @($Agents | Where-Object { $_.DisplayName -eq "Generic Codex Delivery Agent" })[0]
            StartUtc = $StartUtc.AddHours(5)
            EndUtc = $EndUtc.AddHours(-5)
            DurationSeconds = [Math]::Max([int]($EndUtc.AddHours(-5) - $StartUtc.AddHours(5)).TotalSeconds, 14400)
        },
        @{
            Title = "Validate $($Subbundle.Code) proof and regression closure"
            Subtitle = "Validation"
            Notes = "Run build, tests, and browser or projection proof strong enough for downstream phases to trust."
            Subtype = "task"
            Agent = $validatorAgent
            StartUtc = $EndUtc.AddHours(-4)
            EndUtc = $EndUtc
            DurationSeconds = 14400
        }
    )

    $createdTasks = New-Object System.Collections.Generic.List[object]
    $assignments = New-Object System.Collections.Generic.List[object]

    foreach ($taskBlueprint in $taskBlueprints) {
        $participant = @($participants | Where-Object { $_.title -eq $taskBlueprint.Agent.DisplayName })[0]
        $metadataJson = @{
            workItem = @{
                workItemKind = "task"
                assigneeParticipantArtifactId = (Convert-NodeIdToGuid -NodeId $participant.id)
                assigneePartyName = $taskBlueprint.Agent.DisplayName
                description = $taskBlueprint.Notes
                agentSkills = $taskBlueprint.Agent.Skills
                agentExternalCode = $taskBlueprint.Agent.ExternalCode
            }
        } | ConvertTo-Json -Depth 10 -Compress

        $task = New-Node -ProjectId $ProjectId -ObjectType 8 -Title $taskBlueprint.Title -Subtitle $taskBlueprint.Subtitle -Notes $taskBlueprint.Notes -ParentNodeKey $lane.id -ObjectSubtype $taskBlueprint.Subtype -MetadataJson $metadataJson -StartUtc $taskBlueprint.StartUtc -EndUtc $taskBlueprint.EndUtc -DurationSeconds $taskBlueprint.DurationSeconds
        $createdTasks.Add($task)
        $assignments.Add([ordered]@{
            agentDisplayName = $taskBlueprint.Agent.DisplayName
            agentExternalCode = $taskBlueprint.Agent.ExternalCode
            participantNodeKey = $participant.id
            taskNodeKey = $task.id
        })
    }

    return @{
        Lane = $lane
        Participants = @($participants)
        Tasks = $createdTasks.ToArray()
        Assignments = $assignments.ToArray()
    }
}

$phaseRows = Get-PhaseRows
$subbundles = Get-SubbundleData
$agentCatalog = @(Get-AgentCatalog)
$catalogByName = @{}
foreach ($agent in $agentCatalog) {
    $catalogByName[$agent.DisplayName] = $agent
}

$phaseOutline = New-Object System.Collections.Generic.List[object]
foreach ($phaseRow in $phaseRows) {
    $phaseCodes = @($subbundles | Where-Object { $_.Phase -eq $phaseRow.Name -and -not [string]::IsNullOrWhiteSpace($_.Code) } | ForEach-Object { $_.Code })
    $phaseSummary = if ($phaseCodes.Count -gt 0) { $phaseCodes -join ", " } else { "None" }
    $phaseOutline.Add(
        (New-OutlineNode -Title $phaseRow.Name -Notes ("Goal: {0}`nGate: {1}`nFindings: {2}`nSubprojects: {3}" -f $phaseRow.Goal, $phaseRow.Gate, $phaseRow.Findings, $phaseSummary))
    )
}

$dependencyOutline = @(
    $phaseRows | ForEach-Object {
        $phaseName = $_.Name
        $phaseSubbundles = @(
            $subbundles | Where-Object {
                $_.Phase -eq $phaseName -and
                -not [string]::IsNullOrWhiteSpace($_.Code) -and
                -not [string]::IsNullOrWhiteSpace($_.Title)
            }
        )

        $dependencyLines = @(
            $phaseSubbundles | ForEach-Object {
                $dependencyCodes = @($_.Dependencies) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
                $dependencySummary = if ($dependencyCodes.Count -gt 0) { $dependencyCodes -join ", " } else { "None" }
                "{0} [{1}] -> {2}" -f $_.Code, $_.Severity, $dependencySummary
            }
        )

        $phaseDependencySummary = if ($dependencyLines.Count -gt 0) { $dependencyLines -join "`n" } else { "No dependency-sensitive findings scheduled in this phase." }
        New-OutlineNode -Title $phaseName -Notes $phaseDependencySummary
    }
)

$agentCatalogSummary = @(
    $agentCatalog | ForEach-Object {
        "{0}: {1}. Skills: {2}" -f $_.DisplayName, $_.Role, ($_.Skills -join ", ")
    }
) -join "`n"

$operatingModelOutline = @(
    (New-OutlineNode -Title "Senior PM control questions" -Notes (@(
        "Can a manager see phase gates, owner seams, and proof windows without reading the whole bundle?"
        "Are dependencies explicit enough to know when later work is unsafe to start?"
        "Do assigned agents and timespans make execution ownership visible?"
    ) -join "`n")),
    (New-OutlineNode -Title "AI agent catalog" -Notes $agentCatalogSummary)
)

$umbrellaProject = Invoke-ProjectStructureApi -Method "Post" -Path "/api/project-structure-mcp/projects" -Body @{
    name = "Canonical Architecture Review Bundle V2 Backfill"
    description = "Fresh managed-SQLite validation project reconstructed from candoitall-canonical-architecture-review-bundle-v2 through the project-structure agent path."
    objective = "Prove that the canonical-architecture bundle can be represented as a manager-usable project hierarchy with subprojects, AI ownership, time windows, and readable mindmaps."
    currentPhase = "Fresh validation"
}

$umbrellaProjectId = [guid]$umbrellaProject.id
$phaseScopeImport = Create-OutlineGraph -ProjectId $umbrellaProjectId -Title "Phase roadmap" -RootNodes $phaseOutline.ToArray() -ContainerBlockSubtype "delivery" -LeafWorkItemSubtype "task"
$dependencyImport = Create-OutlineGraph -ProjectId $umbrellaProjectId -Title "Finding dependency map" -RootNodes $dependencyOutline -ContainerBlockSubtype "task-flow" -LeafWorkItemSubtype "task"
$operatingModelImport = Create-OutlineGraph -ProjectId $umbrellaProjectId -Title "AI operating model" -RootNodes $operatingModelOutline -ContainerBlockSubtype "task-flow" -LeafWorkItemSubtype "task"
Recompose-ProjectRoot -ProjectId $umbrellaProjectId | Out-Null

$phasePointers = @{}
foreach ($phaseRow in $phaseRows) {
    $phasePointers[$phaseRow.Name] = $scheduleAnchor
}

$subprojectResults = New-Object System.Collections.Generic.List[object]

foreach ($subbundle in $subbundles) {
    $phaseStart = [DateTimeOffset]$phasePointers[$subbundle.Phase]
    $durationDays = Get-SeverityDurationDays -Severity $subbundle.Severity
    $phaseEnd = Add-BusinessDays -Start $phaseStart -Days $durationDays
    $phasePointers[$subbundle.Phase] = Add-BusinessDays -Start $phaseEnd -Days 1

    $project = Invoke-ProjectStructureApi -Method "Post" -Path "/api/project-structure-mcp/projects" -Body @{
        name = "{0} - {1}" -f $subbundle.Code, $subbundle.Title
        description = $subbundle.ProblemStatement
        objective = $subbundle.WhyNow
        currentPhase = $subbundle.Phase
    }

    $projectId = [guid]$project.id
    Invoke-ProjectStructureApi -Method "Post" -Path "/api/project-structure-mcp/projects/$umbrellaProjectId/subprojects" -Body @{
        childProjectId = $projectId
    } | Out-Null

    $deliverableNodes = @($subbundle.Deliverables | ForEach-Object { New-OutlineNode -Title $_ })
    $dependencyNodes = @($subbundle.Dependencies | ForEach-Object { New-OutlineNode -Title $_ })
    $fileNodes = @($subbundle.LikelyFilesTouched | ForEach-Object { New-OutlineNode -Title $_ })

    $controlOutline = @(
        (New-OutlineNode -Title "Problem statement" -Notes $subbundle.ProblemStatement),
        (New-OutlineNode -Title "Why this matters now" -Notes $subbundle.WhyNow),
        (New-OutlineNode -Title "Deliverables" -Children $deliverableNodes),
        (New-OutlineNode -Title "Dependencies" -Children $dependencyNodes),
        (New-OutlineNode -Title "Likely files touched" -Children $fileNodes),
        (New-OutlineNode -Title "Manager control" -Children @(
            (New-OutlineNode -Title ("Severity: {0}" -f $subbundle.Severity)),
            (New-OutlineNode -Title ("Category: {0}" -f $subbundle.Category)),
            (New-OutlineNode -Title ("Timing: {0}" -f $subbundle.Timing)),
            (New-OutlineNode -Title ("Planned window: {0:yyyy-MM-dd} to {1:yyyy-MM-dd}" -f $phaseStart, $phaseEnd))
        ))
    )

    $detailImport = Create-OutlineGraph -ProjectId $projectId -Title "Backfilled control plan" -RootNodes $controlOutline -ContainerBlockSubtype "delivery" -LeafWorkItemSubtype "task"
    $assignedAgents = Get-AssignedAgents -Subbundle $subbundle -CatalogByName $catalogByName
    $aiLane = Add-ExecutionLane -ProjectId $projectId -Subbundle $subbundle -StartUtc $phaseStart -EndUtc $phaseEnd -Agents $assignedAgents

    Recompose-ProjectRoot -ProjectId $projectId | Out-Null
    $layout = Read-Structure -ProjectId $projectId

    $subprojectResults.Add([ordered]@{
        code = $subbundle.Code
        title = $subbundle.Title
        severity = $subbundle.Severity
        category = $subbundle.Category
        phase = $subbundle.Phase
        dependencies = $subbundle.Dependencies
        startUtc = $phaseStart
        endUtc = $phaseEnd
        projectId = $projectId
        route = "/projects/$projectId/structure"
        detailImport = $detailImport
        aiLane = @{
            lane = $aiLane.Lane
            participants = $aiLane.Participants
            tasks = $aiLane.Tasks
            assignments = $aiLane.Assignments
        }
        layoutAudit = @{
            nodeCount = @($layout.nodes).Count
            linkCount = @($layout.links).Count
            warnings = @($layout.warnings)
        }
    })
}

$result = [ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    baseUrl = $BaseUrl
    sqliteDatabasePath = $SqliteDatabasePath
    inputBundleRoot = $sourceBundleRoot
    umbrellaProjectId = $umbrellaProjectId
    umbrellaProjectName = $umbrellaProject.name
    umbrellaRoute = "/projects/$umbrellaProjectId/structure"
    phaseScopeImport = $phaseScopeImport
    dependencyImport = $dependencyImport
    operatingModelImport = $operatingModelImport
    agentCatalog = $agentCatalog
    subprojects = $subprojectResults.ToArray()
}

Ensure-Directory -Path $OutputPath
$result | ConvertTo-Json -Depth 100 | Set-Content -Encoding utf8 -Path $OutputPath
$result | ConvertTo-Json -Depth 100
