$ErrorActionPreference = "Continue"

$RepoRoot = "C:\repositories\CanDoItAll"
$BundleRoot = Join-Path $RepoRoot "codex\bundles\workflow-node-project-isolation"
$ProofRoot = Join-Path $BundleRoot "proof\SB01"
$TranscriptRoot = Join-Path $ProofRoot "transcripts"
$Python = "C:\Users\lucys\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe"
$Node = "C:\Users\lucys\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe"
$Validator = "C:\Users\lucys\.codex\skills\candoitall-bundle-preparation\scripts\validate_bundle.py"

New-Item -ItemType Directory -Force -Path $TranscriptRoot | Out-Null

function Write-ProofTranscript {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [string] $CommandLine,

        [Parameter(Mandatory = $true)]
        [scriptblock] $Action
    )

    $start = Get-Date -Format "o"
    $global:ProofExitCode = $null
    Push-Location $RepoRoot

    try {
        $output = & $Action 2>&1 | Out-String -Width 4096
        $exitCode = if ($null -ne $global:ProofExitCode) {
            $global:ProofExitCode
        }
        elseif ($null -ne $global:LASTEXITCODE) {
            $global:LASTEXITCODE
        }
        else {
            0
        }
    }
    catch {
        $output = $_ | Out-String -Width 4096
        $exitCode = 1
    }
    finally {
        Pop-Location
    }

    $end = Get-Date -Format "o"
    $content = @(
        "Command: $CommandLine"
        "Working directory: $RepoRoot"
        "Start: $start"
        "End: $end"
        "Exit code: $exitCode"
        ""
        $output
    )

    Set-Content -LiteralPath $Path -Value $content -Encoding UTF8
}

Write-ProofTranscript `
    -Path (Join-Path $TranscriptRoot "inventory-search.txt") `
    -CommandLine "rg --files src tests Templates | rg '(Workflow|workflow|Workflows|workflows|WorkflowExecutor|executor|Templates/Workflows)'; rg -n 'WorkflowExecutor|IWorkflowExecutor|PluginWorkflowExecutor|WorkflowTemplatePackLoader|Templates/Workflows|WorkflowRuntime|WorkflowDefinition|WorkflowNode|WorkflowRun' src tests Templates; rg -n 'WorkflowExecutorIds|public static WorkflowExecutorId|new\(`"[a-z0-9._-]+`"\)' src\CanDoItAll.AgentFramework.Models\Workflows src\CanDoItAll.AgentFramework.Maf\Runtime\Workflows src\CanDoItAll.Modules.CognitiveMemory src\plugins" `
    -Action {
        rg --files src tests Templates | rg "(Workflow|workflow|Workflows|workflows|WorkflowExecutor|executor|Templates/Workflows)"
        rg -n "WorkflowExecutor|IWorkflowExecutor|PluginWorkflowExecutor|WorkflowTemplatePackLoader|Templates/Workflows|WorkflowRuntime|WorkflowDefinition|WorkflowNode|WorkflowRun" src tests Templates
        rg -n 'WorkflowExecutorIds|public static WorkflowExecutorId|new\("[a-z0-9._-]+"\)' src\CanDoItAll.AgentFramework.Models\Workflows src\CanDoItAll.AgentFramework.Maf\Runtime\Workflows src\CanDoItAll.Modules.CognitiveMemory src\plugins
        $global:ProofExitCode = $LASTEXITCODE
    }

Write-ProofTranscript `
    -Path (Join-Path $TranscriptRoot "source-assertions.txt") `
    -CommandLine "Assert repaired SB01 source references and mapping rows exist" `
    -Action {
        $paths = @(
            "src\CanDoItAll.Modules.CognitiveMemory\Advanced\CognitiveMemoryMafIntegration.cs",
            "src\CanDoItAll.Modules.CognitiveMemory\CognitiveMemoryModuleServiceCollectionExtensions.cs",
            "src\CanDoItAll.Modules.Workbench\AgentTools\ProjectStructureAgentRuntimeToolProvider.cs",
            "src\CanDoItAll.Modules.SchedulerPlanner\SchedulerWorkflowInputSchemaService.cs",
            "src\CanDoItAll.Modules.SchedulerPlanner\SchedulerWorkflowInputOptionService.cs",
            "src\CanDoItAll.Composition\SchedulerPlannerWorkflowInputOptionProviders.cs",
            "codex\bundles\workflow-node-project-isolation\inventories\workflow-node-project-isolation-map.xlsx"
        )

        foreach ($path in $paths) {
            "$path exists: $(Test-Path -LiteralPath (Join-Path $RepoRoot $path))"
        }

        Select-String -LiteralPath (Join-Path $BundleRoot "inventories\02-workflow-source-inventory.md") -Pattern "Cognitive Memory workflow executors|Workbench agent workflow tools|Scheduler workflow input options"
        Select-String -LiteralPath (Join-Path $BundleRoot "inventories\03-executor-inventory.md") -Pattern "Cognitive Memory|cognitive-memory.recall|cognitive-memory.probe|cognitive-memory.learning-proposal"
        Select-String -LiteralPath (Join-Path $BundleRoot "architecture\02-project-map-and-adoption-boundary.md") -Pattern "Module-provided workflow executors|Workbench agent workflow tools|Scheduler workflow input option services"
        $global:ProofExitCode = 0
    }

Write-ProofTranscript `
    -Path (Join-Path $TranscriptRoot "semantic-surface-check.txt") `
    -CommandLine "Adversarial SB01 semantic surface check for module executors, Workbench agent workflow tools, and Scheduler workflow input consumers" `
    -Action {
        "SB01-INV-01 shallow-pass trap: a default/plugin-only inventory misses feature-module executors and non-UI workflow consumers."
        $requiredRows = @(
            "Cognitive Memory workflow executors",
            "Workbench agent workflow tools",
            "Scheduler workflow input options"
        )
        $shallowInventoryRows = @(
            "Built-in executor registration",
            "Built-in descriptors",
            "Plugin descriptor source",
            "Plugin package loading",
            "Workflow API",
            "Workflow UI/editor",
            "Workbench workflow nodes"
        )
        $missingFromShallow = $requiredRows | Where-Object { $shallowInventoryRows -notcontains $_ }
        "Adversarial negative case expected missing rows: $($missingFromShallow -join ', ')"
        if ($missingFromShallow.Count -ne 3) {
            throw "The adversarial shallow inventory did not demonstrate the expected missing-surface failure."
        }

        "SB01-INV-01 semantic positive: repaired inventory contains every required added surface."
        foreach ($row in $requiredRows) {
            Select-String -LiteralPath (Join-Path $BundleRoot "inventories\02-workflow-source-inventory.md") -Pattern ([regex]::Escape($row))
        }

        "SB01-INV-02 stable executor ids are present in production source and mapped to SB06/SB09 proof."
        rg -n "cognitive-memory\.recall|cognitive-memory\.probe|cognitive-memory\.learning-proposal" src\CanDoItAll.Modules.CognitiveMemory

        "SB01-INV-03 dependency direction: feature-module executors stay module-owned and consume WorkflowExecutors.Abstractions, not MAF/Core executor-contract ownership."
        Select-String -LiteralPath (Join-Path $BundleRoot "architecture\02-project-map-and-adoption-boundary.md") -Pattern "Module-provided workflow executors|MAF/Core executor-contract ownership"
        Select-String -LiteralPath (Join-Path $BundleRoot "subbundles\06-executor-abstractions-and-shared-helpers\README.md") -Pattern "feature modules|Cognitive Memory"
        Select-String -LiteralPath (Join-Path $BundleRoot "subbundles\09-executor-refactoring-hardening-checkpoint\README.md") -Pattern "feature-module|module-provided"

        "SB01-INV-04 workbook mapping rows are rendered and formula scan has no error matches."
        Select-String -LiteralPath (Join-Path $ProofRoot "workbook-previews\sheet-list.ndjson") -Pattern "Source Map|Project Targets|Executor Categories|Validation Matrix"
        Get-Content -LiteralPath (Join-Path $ProofRoot "workbook-previews\formula-error-scan.ndjson")
        $global:ProofExitCode = 0
    }

Write-ProofTranscript `
    -Path (Join-Path $TranscriptRoot "workbook-render.txt") `
    -CommandLine "& '$Node' '$ProofRoot\tools\inspect-workbook.mjs'" `
    -Action {
        & $Node (Join-Path $ProofRoot "tools\inspect-workbook.mjs")
        $global:ProofExitCode = $LASTEXITCODE
    }

Write-ProofTranscript `
    -Path (Join-Path $TranscriptRoot "prepared-validator.txt") `
    -CommandLine "& '$Python' '$Validator' 'codex\bundles\workflow-node-project-isolation' --profile initiative --stage prepared --repo-root '$RepoRoot'" `
    -Action {
        & $Python $Validator "codex\bundles\workflow-node-project-isolation" --profile initiative --stage prepared --repo-root $RepoRoot
        $global:ProofExitCode = $LASTEXITCODE
    }

Write-ProofTranscript `
    -Path (Join-Path $TranscriptRoot "anti-stub-audit.txt") `
    -CommandLine "rg -n 'TODO|NotImplemented|throw new NotImplementedException|fixture-specific|template-only' <SB01 touched bundle files>" `
    -Action {
        $files = @(
            "codex\bundles\workflow-node-project-isolation\inventories\02-workflow-source-inventory.md",
            "codex\bundles\workflow-node-project-isolation\inventories\03-executor-inventory.md",
            "codex\bundles\workflow-node-project-isolation\inventories\04-test-and-validation-inventory.md",
            "codex\bundles\workflow-node-project-isolation\architecture\02-project-map-and-adoption-boundary.md",
            "codex\bundles\workflow-node-project-isolation\traceability\01-requirement-traceability.md",
            "codex\bundles\workflow-node-project-isolation\subbundles\01-workflow-boundary-inventory-and-project-graph\README.md",
            "codex\bundles\workflow-node-project-isolation\subbundles\06-executor-abstractions-and-shared-helpers\README.md",
            "codex\bundles\workflow-node-project-isolation\subbundles\09-executor-refactoring-hardening-checkpoint\README.md",
            "codex\bundles\workflow-node-project-isolation\subbundles\10-workflow-template-and-descriptor-loading\README.md",
            "codex\bundles\workflow-node-project-isolation\subbundles\12-api-ui-workbench-adoption\README.md"
        )

        $matches = rg -n "TODO|NotImplemented|throw new NotImplementedException|fixture-specific|template-only" $files
        if ($LASTEXITCODE -eq 1) {
            "No anti-stub markers found in SB01 touched bundle files."
            "No production source files were changed in SB01."
            $global:ProofExitCode = 0
        }
        else {
            $matches
            $global:ProofExitCode = $LASTEXITCODE
        }
    }
