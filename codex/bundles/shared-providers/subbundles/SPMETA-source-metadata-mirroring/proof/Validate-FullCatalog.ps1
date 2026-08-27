param([string[]] $UiRunLabels = @('full-catalog-ui-6', 'full-catalog-ui-repeat'))
$ErrorActionPreference = 'Stop'
$repoRoot = (git rev-parse --show-toplevel).Trim()
$bundleRoot = Join-Path $repoRoot 'codex/bundles/shared-providers'
Start-Transcript -Path (Join-Path $PSScriptRoot 'transcripts/full-catalog-closure.txt') -Force | Out-Null
try {
    Write-Output "FULL-SET FULL-ISOLATION FULL-UI FULL-RUN FULL-HANDOFF: completed-stage artifact/source gate; cwd=$repoRoot"
    Write-Output "Command: Validate-FullCatalog.ps1 -UiRunLabels $($UiRunLabels -join ',')"
    function Assert-Proof([bool] $Condition, [string] $Message) {
        if (-not $Condition) {
            throw $Message
        }
        Write-Output "PASS $Message"
    }
    $expected = [ordered]@{
        'full-catalog-unit-2' = 52
        'full-catalog-integration' = 52
        'full-catalog-components' = 24
        'full-catalog-agent-save-2' = 39
        'full-catalog-simple-chats-probe-2' = 1
    }
    foreach ($label in $UiRunLabels) {
        $expected.Add($label, 1)
    }
    foreach ($entry in $expected.GetEnumerator()) {
        [xml] $run = Get-Content (Join-Path $PSScriptRoot "transcripts/$($entry.Key).trx") -Raw
        $counts = $run.TestRun.ResultSummary.Counters
        Assert-Proof ([int]$counts.total -eq $entry.Value -and [int]$counts.passed -eq $entry.Value -and [int]$counts.notExecuted -eq 0) "$($entry.Key): $($entry.Value) passed, no skips"
        $transcript = Get-Content (Join-Path $PSScriptRoot "transcripts/$($entry.Key).txt") -Raw
        Assert-Proof ($transcript.Contains("Discovered tests: $($entry.Value);") -and $transcript.Contains('Exit code: 0')) "$($entry.Key): nonzero discovery and successful command"
    }
    [xml] $red = Get-Content (Join-Path $PSScriptRoot 'transcripts/full-catalog-red.trx') -Raw
    Assert-Proof ([int]$red.TestRun.ResultSummary.Counters.failed -eq 1) 'FULL-SET: genuine failing-first regression'
    [xml] $saveRed = Get-Content (Join-Path $PSScriptRoot 'transcripts/full-catalog-unpriced-red-2.trx') -Raw
    Assert-Proof ([int]$saveRed.TestRun.ResultSummary.Counters.failed -eq 4) 'FULL-RUN: four failing-first shared-model save cases'
    $providerRoot = Join-Path $repoRoot 'src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement'
    $mapper = Get-Content (Join-Path $providerRoot 'RuntimeProjection/PersistedProviderProfileMapper.cs') -Raw
    $reader = Get-Content (Join-Path $providerRoot 'Administration/SharedProviderProfilePublicationMetadata.cs') -Raw
    Assert-Proof ($mapper.Contains('ProviderModelCatalogPolicy.Resolve(') -and $reader.Contains('ProviderModelCatalogPolicy.Resolve(')) 'FULL-SET: production source and publisher use one catalog policy'
    Assert-Proof (-not $mapper.Contains('ResolvePersistedProviderSuggestedModels(') -and $reader.Contains('including built-in models.')) 'FULL-ISOLATION: old duplicate policy removed; expanded limit enforced'
    $factory = Get-Content (Join-Path $repoRoot 'src/MAF/Common/CanDoItAll.AgentFramework.Core/Agents/AgentDefinitionFactory.cs') -Raw
    Assert-Proof ($factory.Contains('IsSourceManaged: true') -and $factory.Contains('ProviderModelSelectionPolicy.EnsureAllowed(') -and $factory.Contains('requires a model price row')) 'FULL-RUN: source constraint enforced; local price prerequisite retained'
    foreach ($label in $UiRunLabels) {
        foreach ($profile in @('resynced', 'ollama-final')) {
            $metadata = Get-Content (Join-Path $PSScriptRoot "browser/$label/metadata-$profile-parity.json") -Raw | ConvertFrom-Json
            Assert-Proof (-not (Compare-Object $metadata.Source.Models $metadata.Client.Models)) "FULL-UI: $label/$profile source and client model sets match"
            $minimum = if ($profile -eq 'resynced') { 12 } else { 3 }
            Assert-Proof ($metadata.Client.Models.Count -ge $minimum) "FULL-SET: $label/$profile has $minimum or more choices"
        }
        $runtime = Get-Content (Join-Path $PSScriptRoot "transcripts/$label-runtime.txt") -Raw
        Assert-Proof ($runtime.Contains('PASS production ledger counts: 10|10|1') -and $runtime.Contains('Exit code: 0')) "FULL-RUN: $label ten complete central successes including image"
        foreach ($model in @('gpt-4.1-mini', 'e2e-ollama-secondary', 'gpt-5.4-mini', 'e2e-ollama-vision')) {
            Assert-Proof ($runtime.Contains("PASS selected non-default model recorded by central ledger: $model")) "FULL-RUN: $label invoked $model"
        }
        $auth = Get-Content (Join-Path $PSScriptRoot "browser/$label/simple-chat-auth.json") -Raw | ConvertFrom-Json
        Assert-Proof ($auth.ApiStatus -eq 200 -and $auth.Scope.Split(' ').Count -eq 3) "FULL-UI: $label authenticated Simple Chats browser and API"
    }
    $index = Get-Content (Join-Path $PSScriptRoot 'full-catalog-changed-files.json') -Raw | ConvertFrom-Json
    Assert-Proof ($index.baseline -eq '0ecb6307823576e80f79074187668771b166609a' -and $index.baseline -eq (git rev-parse HEAD).Trim()) 'Current baseline identity unchanged'
    foreach ($file in $index.files) {
        $path = if ($file.path.StartsWith('repo://')) {
            Join-Path $repoRoot $file.path.Substring(7)
        } else {
            Join-Path $bundleRoot $file.path.Substring(9)
        }
        $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        Assert-Proof ($hash -eq $file.after) "Artifact hash: $($file.path)"
        if ($file.path.StartsWith('repo://src/')) {
            $content = Get-Content -LiteralPath $path -Raw
            Assert-Proof ($content -notmatch 'TODO|NotImplementedException|e2e-|deterministic fixture|fullcatalog-2026') "Production anti-stub audit: $($file.path)"
        }
    }
    Assert-Proof (-not $index.skillFilesChanged) 'No skill modifications'
    $boundary = Get-Content (Join-Path $PSScriptRoot 'full-catalog-boundary.json') -Raw | ConvertFrom-Json
    Assert-Proof ($boundary.passed -and $boundary.violations.Count -eq 0) 'Production provider boundary scan passes'
    Assert-Proof ((Get-Content (Join-Path $bundleRoot 'STATUS.md') -Raw).Contains('| SB07 | `BLOCKED`')) 'Historical SB07 remains blocked'
    git diff --check
    Assert-Proof ($LASTEXITCODE -eq 0) 'Whitespace gate'
    Write-Output 'Completed-stage gate passed. Exit code: 0'
} finally {
    Stop-Transcript | Out-Null
}
