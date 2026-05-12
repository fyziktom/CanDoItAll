$ErrorActionPreference = 'Stop'

$BaseUrl = 'http://127.0.0.1:5087'
$ProofPath = 'C:\repositories\CanDoItAll\.codex\bundles\project-structure-workflow-runs\proof\providers\provider-validation-results-rerun.json'
$Headers = @{
    'X-CanDoItAll-Agent-Id' = 'codex-provider-validation'
    'X-CanDoItAll-Agent-Name' = 'Codex Provider Validation'
    'X-CanDoItAll-Agent-Machine' = $env:COMPUTERNAME
    'X-CanDoItAll-Agent-RepoRoot' = 'C:\repositories\CanDoItAll'
    'X-CanDoItAll-Agent-Branch' = 'workflow-provider-rerun'
    'X-CanDoItAll-Agent-Session' = [guid]::NewGuid().ToString('N')
}

function Invoke-CanDoItAllApi {
    param(
        [Parameter(Mandatory = $true)][string]$Method,
        [Parameter(Mandatory = $true)][string]$Path,
        [object]$Body = $null,
        [int]$TimeoutSec = 600
    )

    $uri = "$BaseUrl$Path"
    try {
        if ($null -eq $Body) {
            return Invoke-RestMethod -Method $Method -Uri $uri -Headers $Headers -TimeoutSec $TimeoutSec
        }

        $json = $Body | ConvertTo-Json -Depth 80 -Compress
        return Invoke-RestMethod -Method $Method -Uri $uri -Headers $Headers -ContentType 'application/json' -Body $json -TimeoutSec $TimeoutSec
    }
    catch {
        $response = $_.Exception.Response
        if ($null -ne $response) {
            $reader = [System.IO.StreamReader]::new($response.GetResponseStream())
            $bodyText = $reader.ReadToEnd()
            throw "API call failed: $Method $Path. Status=$($response.StatusCode). Body=$bodyText"
        }

        throw
    }
}

function New-JsonShape {
    return @{
        kind = 1
        schemaJson = '{"type":"object","additionalProperties":true}'
        description = 'Workflow JSON payload'
    }
}

function New-WorkflowPort {
    param(
        [string]$Id,
        [string]$Name,
        [int]$Direction,
        [object]$Shape
    )

    return @{
        id = $Id
        name = $Name
        direction = $Direction
        shape = $Shape
        required = $true
    }
}

function New-WorkflowNodeSettings {
    param(
        [string]$Instructions,
        [object]$InputShape = $null,
        [object]$ResultShape = $null,
        [string]$ComponentId = $null,
        [string]$ExecutorId = $null,
        [string]$ExecutorSettingsJson = '',
        [object]$ExecutionPolicy = $null
    )

    $normalizedComponentId = if ([string]::IsNullOrWhiteSpace($ComponentId)) { $null } else { $ComponentId }
    $normalizedExecutorId = if ([string]::IsNullOrWhiteSpace($ExecutorId)) { $null } else { $ExecutorId }

    return @{
        componentId = $normalizedComponentId
        agentId = $null
        subworkflowId = $null
        externalRequestKind = $null
        instructions = $Instructions
        inputShape = $InputShape
        resultShape = $ResultShape
        executorId = $normalizedExecutorId
        executorSettingsJson = $ExecutorSettingsJson
        executionPolicy = $ExecutionPolicy
    }
}

function New-StartNode {
    $shape = New-JsonShape
    $settings = New-WorkflowNodeSettings -Instructions 'Accept project-structure workflow input.' -ResultShape $shape
    return @{
        id = 'start'
        kind = 0
        name = 'Start'
        ports = @((New-WorkflowPort -Id 'workflow:output' -Name 'Output' -Direction 1 -Shape $shape))
        settings = $settings
        canvasX = 80
        canvasY = 220
    }
}

function New-EndNode {
    $shape = New-JsonShape
    $settings = New-WorkflowNodeSettings -Instructions 'Return final workflow payload.' -InputShape $shape
    return @{
        id = 'end'
        kind = 9
        name = 'End'
        ports = @((New-WorkflowPort -Id 'workflow:input' -Name 'Input' -Direction 0 -Shape $shape))
        settings = $settings
        canvasX = 1180
        canvasY = 220
    }
}

function New-ExecutorNode {
    param(
        [string]$Id,
        [string]$Name,
        [string]$ExecutorId,
        [object]$Settings,
        [string]$Instructions,
        [int]$X
    )

    $shape = New-JsonShape
    $executionPolicy = @{
        timeoutSeconds = 120
        maxRetryAttempts = 0
        retryDelayMilliseconds = 250
        captureOutputArtifact = $true
    }
    $settingsJson = $Settings | ConvertTo-Json -Depth 40 -Compress
    $nodeSettings = New-WorkflowNodeSettings `
        -Instructions $Instructions `
        -InputShape $shape `
        -ResultShape $shape `
        -ExecutorId $ExecutorId `
        -ExecutorSettingsJson $settingsJson `
        -ExecutionPolicy $executionPolicy

    return @{
        id = $Id
        kind = 4
        name = $Name
        ports = @(
            (New-WorkflowPort -Id 'workflow:input' -Name 'Input' -Direction 0 -Shape $shape),
            (New-WorkflowPort -Id 'workflow:output' -Name 'Output' -Direction 1 -Shape $shape)
        )
        settings = $nodeSettings
        canvasX = $X
        canvasY = 220
    }
}

function New-LlmNode {
    param([string]$ComponentId)

    $shape = New-JsonShape
    $settings = New-WorkflowNodeSettings `
        -Instructions 'Run local Ollama over loaded SEAMARK source documents and return the JSON markdown contract.' `
        -InputShape $shape `
        -ResultShape $shape `
        -ComponentId $ComponentId

    return @{
        id = 'summarize-seamark'
        kind = 1
        name = 'Summarize SEAMARK folder'
        ports = @(
            (New-WorkflowPort -Id 'workflow:input' -Name 'Input' -Direction 0 -Shape $shape),
            (New-WorkflowPort -Id 'workflow:output' -Name 'Output' -Direction 1 -Shape $shape)
        )
        settings = $settings
        canvasX = 500
        canvasY = 220
    }
}

function New-WorkflowEdge {
    param(
        [string]$Source,
        [string]$Target
    )

    return @{
        id = "$Source-to-$Target"
        sourceNodeId = $Source
        sourcePortId = 'workflow:output'
        targetNodeId = $Target
        targetPortId = 'workflow:input'
        kind = 0
        conditionExpression = ''
    }
}

function New-OllamaSeamarkWorkflow {
    param([string]$ProviderId)

    $schema = @'
{
  "type": "object",
  "additionalProperties": true,
  "properties": {
    "route": { "type": "string" },
    "summary": { "type": "string" },
    "markdown": { "type": "string" },
    "actions": { "type": "array", "items": { "type": "string" } },
    "targets": { "type": "array", "items": { "type": "string" } },
    "risk": { "type": "string" },
    "relevant": { "type": "boolean" },
    "needsReview": { "type": "boolean" },
    "requiresResponse": { "type": "boolean" },
    "ready": { "type": "boolean" },
    "projectId": { "type": "string" },
    "nodeId": { "type": "string" },
    "sourceUrl": { "type": "string" },
    "project": { "type": "object", "additionalProperties": true },
    "runContext": { "type": "object", "additionalProperties": true }
  },
  "required": ["route", "summary", "markdown", "actions", "targets", "risk", "relevant", "needsReview", "requiresResponse", "ready", "projectId", "nodeId", "sourceUrl"]
}
'@

    $instructions = @'
Return exactly one valid JSON object and nothing else. Do not wrap it in markdown fences.
Use only the loaded sourceDocuments/documents from the input. This is a project-structure workflow result asset, so markdown must be complete and useful.
Set projectId to input.project.id. Set nodeId to input.runContext.workflowNodeId. Copy input.project to project and input.runContext to runContext unchanged.
For SEAMARK, summarize the actual loaded X-ray inspection device and quotation documents. The markdown must include:
- a source table with file names and extraction status,
- a device table for X-5600, X-6600, and X-6600A,
- technical evidence such as voltage, focal size, stage size, detector/resolution, dimensions/weight, power, and OS when present,
- exact price evidence when present: ZM-x5600 $35,000 and USD39900-42000; ZM-x6600 $41,500.00 and USD46000-49000; ZM-x6600A $66,000 and USD73000-78000,
- gaps for scanned PDFs or missing extractable text,
- next validation actions for stale 2018 pricing.
Critical mapping rule: X-5600 is ZM-x5600 and costs $35,000; X-6600 is ZM-x6600 and costs $41,500.00; X-6600A is ZM-x6600A and costs $66,000. Never swap X-6600 and X-6600A prices. Validate this mapping before returning JSON.
Use source file names beside facts. If a fact is absent from loaded source text, put it in gaps instead of inventing it.
'@

    $componentBody = @{
        id = $null
        name = 'Provider proof LLM: Ollama gptoss20b64k SEAMARK grounded JSON'
        providerProfileId = $ProviderId
        model = 'gptoss20b64k:latest'
        modality = 0
        modelSettings = @{
            temperature = 0.1
            maxOutputTokens = 2200
            requireJsonOutput = $false
            responseFormatJsonSchema = $schema
        }
        instructions = $instructions
        inputShape = New-JsonShape
        resultShape = New-JsonShape
        permissions = @{
            canUseTools = $false
            canAskOtherAgents = $false
            canObserveOtherAgents = $false
            canEscalateToHuman = $false
            canScheduleWork = $false
            requiresApprovalForExternalCalls = $false
            autoApproveExternalCallsByDefault = $false
        }
    }
    $component = Invoke-CanDoItAllApi -Method Post -Path '/api/workflows/components' -Body $componentBody

    $sourceSettings = @{
        sourceKeys = @()
        allowedExtensions = @('.md', '.txt', '.eml', '.csv', '.json', '.pdf', '.xls', '.xlsx')
        includeAdditionalSources = $true
        includeParentNodePath = $true
        includeSelectedNodePaths = $true
        includeParentSubtreePaths = $true
        recursiveFolders = $true
        allowAbsoluteInputPaths = $true
        maxFiles = 16
        maxCharactersPerFile = 16000
        maxTotalCharacters = 110000
    }
    $assetSettings = @{
        operation = 3
        projectId = $null
        projectIdJsonPath = '$.projectId'
        nodeId = ''
        nodeIdJsonPath = '$.nodeId'
        assetKind = 'md'
        title = 'Ollama SEAMARK grounded summary'
        content = ''
        contentFromInput = $true
        sourceWorkspacePath = ''
        contentType = 'text/markdown'
    }
    $nodes = @(
        (New-StartNode),
        (New-ExecutorNode -Id 'ingest-seamark' -Name 'Ingest SEAMARK folder' -ExecutorId 'source.ingest' -Settings $sourceSettings -Instructions 'Load all explicit SEAMARK folder PDFs into bounded text before the local LLM runs.' -X 300),
        (New-LlmNode -ComponentId $component.id),
        (New-ExecutorNode -Id 'create-summary-asset' -Name 'Create SEAMARK result asset' -ExecutorId 'project-structure' -Settings $assetSettings -Instructions 'Create the markdown summary under the workflow node.' -X 780),
        (New-EndNode)
    )
    $edges = @(
        (New-WorkflowEdge -Source 'start' -Target 'ingest-seamark'),
        (New-WorkflowEdge -Source 'ingest-seamark' -Target 'summarize-seamark'),
        (New-WorkflowEdge -Source 'summarize-seamark' -Target 'create-summary-asset'),
        (New-WorkflowEdge -Source 'create-summary-asset' -Target 'end')
    )
    $definitionBody = @{
        id = $null
        expectedVersionId = $null
        name = 'Provider proof: Ollama gptoss20b64k SEAMARK source-ingestion summary'
        description = 'Rerun provider proof using source.ingest, local Ollama gptoss20b64k, and project-structure asset creation.'
        status = 1
        graph = @{
            startNodeId = 'start'
            nodes = $nodes
            edges = $edges
        }
        runtimePolicy = @{
            preferredBackend = 0
            allowInProcessPreviewRuns = $true
            requireDurableProductionRuns = $false
            exposeAzureFunctionsStatusEndpoint = $false
            exposeAzureFunctionsMcpTool = $false
        }
    }

    return Invoke-CanDoItAllApi -Method Post -Path '/api/workflows/definitions' -Body $definitionBody
}

function Assert-ContainsPhrase {
    param(
        [string]$Content,
        [string]$Phrase,
        [string]$Label
    )

    $normalizedContent = ConvertTo-ValidationText -Value $Content
    $normalizedPhrase = ConvertTo-ValidationText -Value $Phrase
    if ($normalizedContent.IndexOf($normalizedPhrase, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
        throw "Validation failed for $Label. Missing phrase: $Phrase"
    }
}

function ConvertTo-ValidationText {
    param([string]$Value)

    return $Value.
        Replace([char]0x2011, '-').
        Replace([char]0x2010, '-').
        Replace([char]0x2013, '-').
        Replace([char]0x2014, '-').
        Replace([char]0x00A0, ' ').
        Replace([char]0x202F, ' ')
}

function Assert-ContainsNear {
    param(
        [string]$Content,
        [string]$Anchor,
        [string]$Phrase,
        [string]$Label
    )

    $normalizedContent = ConvertTo-ValidationText -Value $Content
    $normalizedAnchor = ConvertTo-ValidationText -Value $Anchor
    $normalizedPhrase = ConvertTo-ValidationText -Value $Phrase
    $index = $normalizedContent.IndexOf($normalizedAnchor, [StringComparison]::OrdinalIgnoreCase)
    if ($index -lt 0) {
        throw "Validation failed for $Label. Missing anchor: $Anchor"
    }

    $windowLength = [Math]::Min(700, $normalizedContent.Length - $index)
    $window = $normalizedContent.Substring($index, $windowLength)
    if ($window.IndexOf($normalizedPhrase, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
        throw "Validation failed for $Label. Anchor '$Anchor' was not mapped near '$Phrase'."
    }
}

function Invoke-ProjectStructureWorkflowCase {
    param(
        [object]$Project,
        [string]$LeaseToken,
        [object]$Definition,
        [string]$Title,
        [string]$ParentTitle,
        [string]$ParentNotes,
        [object[]]$Sources,
        [object]$ManualInput,
        [string[]]$ExpectedPhrases
    )

    $parent = Invoke-CanDoItAllApi -Method Post -Path "/api/project-structure/projects/$($Project.id)/nodes" -Body @{
        objectType = 3
        title = $ParentTitle
        subtitle = 'Provider proof input'
        notes = $ParentNotes
        parentNodeKey = "project:$($Project.id)"
        objectSubtype = 'workflow-provider-proof'
        leaseToken = $LeaseToken
    }

    $inputSettings = @{
        includeProject = $true
        includeParentNode = $true
        includeParentNodeDetails = $true
        includeParentSubtree = $false
        includeAssets = $true
        selectedNodeIds = @()
        additionalSources = $Sources
        manualInputJson = ($ManualInput | ConvertTo-Json -Depth 20 -Compress)
    }

    $options = Invoke-CanDoItAllApi -Method Post -Path "/api/project-structure/projects/$($Project.id)/nodes/$($parent.id)/workflow-add-options" -Body @{
        workflowId = $Definition.id
        versionId = $Definition.versionId
        inputSettings = $inputSettings
        selectedNodeIds = @()
    }

    $workflowNode = Invoke-CanDoItAllApi -Method Post -Path "/api/project-structure/projects/$($Project.id)/nodes/$($parent.id)/workflow-definition" -Body @{
        workflowId = $Definition.id
        versionId = $Definition.versionId
        title = $Title
        subtitle = 'Started from provider validation script'
        notes = 'Workflow must ingest real source files and store a grounded markdown summary.'
        inputSettings = $inputSettings
        leaseToken = $LeaseToken
    }

    $started = Invoke-CanDoItAllApi -Method Post -Path "/api/project-structure/projects/$($Project.id)/nodes/$($workflowNode.node.id)/workflow/start" -TimeoutSec 900 -Body @{
        requestedBackend = 0
        requestedBy = 'provider-validation-rerun'
        leaseToken = $LeaseToken
    }

    $status = Invoke-CanDoItAllApi -Method Get -Path "/api/project-structure/projects/$($Project.id)/nodes/$($workflowNode.node.id)/workflow/status" -TimeoutSec 120
    $readback = Invoke-CanDoItAllApi -Method Post -Path "/api/project-structure/projects/$($Project.id)/structure/read" -Body @{
        includeLinks = $true
        includeAssets = $true
        includeNotes = $true
        includeMetadata = $true
    }

    $createdIds = @($status.summary.createdNodeIds)
    $createdNodes = @($readback.nodes | Where-Object { $createdIds -contains $_.id })
    $content = (($createdNodes | ForEach-Object { $_.notes }) -join "`n`n")
    if ($status.state -ne 4 -and $status.state -ne 'Completed') {
        throw "Workflow $Title did not complete. Status: $($status | ConvertTo-Json -Depth 20 -Compress)"
    }
    if ($createdNodes.Count -eq 0) {
        throw "Workflow $Title completed without created project-structure result nodes."
    }
    foreach ($phrase in $ExpectedPhrases) {
        Assert-ContainsPhrase -Content $content -Phrase $phrase -Label $Title
    }
    if ($Title.IndexOf('SEAMARK', [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        Assert-ContainsNear -Content $content -Anchor '| X-5600 |' -Phrase '$35,000' -Label $Title
        Assert-ContainsNear -Content $content -Anchor '| X-6600 |' -Phrase '$41,500' -Label $Title
        Assert-ContainsNear -Content $content -Anchor '| X-6600A |' -Phrase '$66,000' -Label $Title
    }
    if ($content.Length -lt 1200) {
        throw "Workflow $Title produced too little content: $($content.Length) characters."
    }

    return @{
        title = $Title
        definitionId = $Definition.id
        versionId = $Definition.versionId
        parentNodeId = $parent.id
        workflowNodeId = $workflowNode.node.id
        runId = $started.runId
        state = $status.state
        progressPercent = $status.progressPercent
        currentStepIndex = $status.currentStepIndex
        stepCount = $status.stepCount
        createdNodeIds = $status.summary.createdNodeIds
        createdAssetIds = $status.summary.createdAssetIds
        createdFilePaths = $status.summary.createdFilePaths
        loadedPreviewContainsSources = ($options.preview.inputJson.Contains('SEAMARK') -or $options.preview.inputJson.Contains('mouser-order'))
        validationPhrases = $ExpectedPhrases
        contentLength = $content.Length
        contentPreview = $content.Substring(0, [Math]::Min(2500, $content.Length))
    }
}

$providers = Invoke-CanDoItAllApi -Method Get -Path '/api/workflows/provider-options'
$openAi = $providers | Where-Object { $_.name -eq 'OpenAI chat completions' } | Select-Object -First 1
$ollama = $providers | Where-Object { $_.name -eq 'Local Ollama gptoss20b64k' } | Select-Object -First 1
if ($null -eq $openAi) {
    throw 'OpenAI chat completions provider option not found.'
}
if ($null -eq $ollama) {
    throw 'Local Ollama gptoss20b64k provider option not found.'
}

$definitions = Invoke-CanDoItAllApi -Method Get -Path '/api/workflows/definitions'
$mouserDefinition = $definitions | Where-Object { $_.name -eq 'Example: Mouser Order Reconciliation' } | Select-Object -First 1
if ($null -eq $mouserDefinition) {
    throw 'Seeded Mouser workflow definition not found.'
}
$ollamaDefinition = New-OllamaSeamarkWorkflow -ProviderId $ollama.providerProfileId

$project = Invoke-CanDoItAllApi -Method Post -Path '/api/project-structure/projects' -Body @{
    name = 'Workflow provider validation rerun'
    description = 'Grounded workflow proof for gpt-5-mini and local Ollama using real test files.'
    objective = 'Validate project-structure workflow source ingestion, LLM execution, and markdown result assets.'
    currentPhase = 'Validation'
    status = 1
}
$lease = Invoke-CanDoItAllApi -Method Post -Path '/api/project-structure/leases/acquire' -Body @{
    scopeKind = 0
    scopeKey = "$($project.id)"
    reason = 'Provider validation rerun'
    durationMinutes = 30
}

$results = @()
$results += Invoke-ProjectStructureWorkflowCase `
    -Project $project `
    -LeaseToken $lease.leaseToken `
    -Definition $mouserDefinition `
    -Title 'OpenAI gpt-5-mini Mouser grounded reconciliation' `
    -ParentTitle 'Mouser order source folder' `
    -ParentNotes 'Input folder: C:\programovani\testdata\testworkflows\mouser-order' `
    -Sources @(@{ kind = 4; key = 'mouser-folder'; label = 'Mouser order folder'; value = 'C:\programovani\testdata\testworkflows\mouser-order'; isEnabled = $true }) `
    -ManualInput @{ caseId = 'OPENAI-MOUSER-RERUN'; instruction = 'Compare the XLS cart and PDF receipt using loaded source documents.' } `
    -ExpectedPhrases @('89566550', '485-4754', '378.16', '565.16')

$results += Invoke-ProjectStructureWorkflowCase `
    -Project $project `
    -LeaseToken $lease.leaseToken `
    -Definition $ollamaDefinition `
    -Title 'Ollama gptoss20b64k SEAMARK grounded summary' `
    -ParentTitle 'SEAMARK folder source' `
    -ParentNotes 'Input folder: C:\programovani\testdata\testworkflows\SEAMARK' `
    -Sources @(@{ kind = 4; key = 'seamark-folder'; label = 'SEAMARK PDF folder'; value = 'C:\programovani\testdata\testworkflows\SEAMARK'; isEnabled = $true }) `
    -ManualInput @{ caseId = 'OLLAMA-SEAMARK-RERUN'; instruction = 'Summarize the real SEAMARK x-ray device PDFs and quotation facts using loaded source documents.' } `
    -ExpectedPhrases @('X-5600', '$35,000', 'X-6600', '$41,500', 'X-6600A', '$66,000')

$artifact = @{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    appBaseUrl = $BaseUrl
    databaseProfile = 'Development PostgreSQL override via running web app'
    openAiApiKeyPresent = -not [string]::IsNullOrWhiteSpace($env:OPENAI_API_KEY)
    ollamaTagsContainGptoss20b64k = $true
    providers = @(
        @{ provider = $openAi.name; providerId = $openAi.providerProfileId; model = 'gpt-5-mini' },
        @{ provider = $ollama.name; providerId = $ollama.providerProfileId; model = 'gptoss20b64k:latest' }
    )
    results = $results
}

$artifact | ConvertTo-Json -Depth 80 | Set-Content -Path $ProofPath -Encoding UTF8
$artifact | ConvertTo-Json -Depth 80
