param(
    [Parameter(Mandatory = $true)]
    [string] $RepoRoot,

    [Parameter(Mandatory = $true)]
    [string] $ReferenceRoot
)

$ErrorActionPreference = 'Stop'

$repoRootPath = (Resolve-Path -LiteralPath $RepoRoot).Path
$referenceRootPath = if (Test-Path -LiteralPath $ReferenceRoot) {
    (Resolve-Path -LiteralPath $ReferenceRoot).Path
} else {
    New-Item -ItemType Directory -Force -Path $ReferenceRoot | Out-Null
    (Resolve-Path -LiteralPath $ReferenceRoot).Path
}

$legacyRoot = Join-Path $referenceRootPath 'legacy'
$inventoriesRoot = Join-Path $referenceRootPath 'inventories'
New-Item -ItemType Directory -Force -Path $legacyRoot, $inventoriesRoot | Out-Null

$sourceRoots = @(
    'src/CanDoItAll.Modules.Processes',
    'src/CanDoItAll.Processes.Core',
    'src/CanDoItAll.Processes.Contracts',
    'src/CanDoItAll.Processes.Drivers.Abstractions',
    'src/CanDoItAll.Processes.Drivers.ArtifactEvidence',
    'src/CanDoItAll.Processes.Drivers.BusinessAnalysis',
    'src/CanDoItAll.Processes.Drivers.ObservationAggregation',
    'src/CanDoItAll.Processes.Drivers.OfficeEvidence',
    'src/CanDoItAll.Processes.Drivers.RuntimeEvidence',
    'src/CanDoItAll.Processes.Drivers.SoftwareDeliveryEvidence',
    'src/CanDoItAll.Processes.Drivers.TranscriptVerification',
    'src/CanDoItAll.Processes.Drivers.VerificationGateway'
)

$templateRoot = 'Templates/Processes'
$solutionFiles = @('CanDoItAll.slnx')
$processTerms = '(?i)(process|processes|processrun|processsteprun|processworkspace|liveprocesses|processobservation|processtemplate|processruntime|processdriver)'
$integrationTerms = '(?i)(CanDoItAll\.Modules\.Processes|CanDoItAll\.Processes\.|ProcessRun|ProcessStepRun|ProcessTemplate|LiveProcesses|ProcessObservation|ProcessDriver|/processes|Processes)'

function Invoke-GitLsFiles {
    param([string[]] $PathSpecs)

    $output = git -C $repoRootPath ls-files -- @PathSpecs
    if ($LASTEXITCODE -ne 0) {
        throw "git ls-files failed for $($PathSpecs -join ', ')"
    }

    return $output | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
}

function Invoke-RgList {
    param(
        [string] $Pattern,
        [string[]] $Roots
    )

    $output = rg -l -i $Pattern @Roots
    if ($LASTEXITCODE -notin @(0, 1)) {
        throw "rg failed for pattern $Pattern"
    }

    return $output | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
}

function Test-IsUnderAnyPath {
    param(
        [string] $RelativePath,
        [string[]] $Roots
    )

    $normalized = $RelativePath.Replace('\', '/')
    foreach ($root in $Roots) {
        $normalizedRoot = $root.TrimEnd('/').Replace('\', '/')
        if ($normalized -eq $normalizedRoot -or $normalized.StartsWith("$normalizedRoot/", [StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }

    return $false
}

function Get-ArchiveRelativePath {
    param(
        [string] $SourcePath,
        [string] $Kind
    )

    $normalized = $SourcePath.Replace('\', '/')
    if ($Kind -eq 'integration-reference') {
        return "legacy/integration-snippets/$normalized"
    }

    return "legacy/$normalized"
}

function Copy-ArchiveFile {
    param(
        [string] $SourcePath,
        [string] $ArchiveRelativePath
    )

    $sourceFullPath = Join-Path $repoRootPath $SourcePath
    if (-not (Test-Path -LiteralPath $sourceFullPath -PathType Leaf)) {
        throw "Source file does not exist: $SourcePath"
    }

    $targetFullPath = Join-Path $referenceRootPath $ArchiveRelativePath
    $targetDirectory = Split-Path -Parent $targetFullPath
    New-Item -ItemType Directory -Force -Path $targetDirectory | Out-Null
    Copy-Item -LiteralPath $sourceFullPath -Destination $targetFullPath -Force
}

function Get-LineCount {
    param([string] $Path)

    try {
        $count = 0
        [System.IO.File]::ReadLines($Path) | ForEach-Object { $count++ }
        return $count
    } catch {
        return 0
    }
}

function Get-ArchiveCategory {
    param(
        [string] $SourcePath,
        [string] $Kind
    )

    $path = $SourcePath.Replace('\', '/')
    if ($Kind -eq 'integration-reference') {
        return 'integration-reference'
    }

    if ($path -eq 'CanDoItAll.slnx') {
        return 'solution-reference'
    }

    if ($path.StartsWith('Templates/Processes/', [StringComparison]::OrdinalIgnoreCase)) {
        return 'template-input'
    }

    if ($path.StartsWith('tests/', [StringComparison]::OrdinalIgnoreCase)) {
        if ($path.Contains('/TestData/', [StringComparison]::OrdinalIgnoreCase)) {
            return 'test-data'
        }

        return 'test-source'
    }

    if ($path.Contains('.Drivers.Abstractions/', [StringComparison]::OrdinalIgnoreCase)) {
        return 'driver-abstraction'
    }

    if ($path.Contains('.Drivers.', [StringComparison]::OrdinalIgnoreCase)) {
        return 'driver-implementation'
    }

    if ($path.StartsWith('src/CanDoItAll.Processes.Contracts/', [StringComparison]::OrdinalIgnoreCase)) {
        return 'contract-model'
    }

    if ($path.StartsWith('src/CanDoItAll.Processes.Core/', [StringComparison]::OrdinalIgnoreCase)) {
        return 'core-rule'
    }

    if ($path.Contains('/Automation/Dispatch/', [StringComparison]::OrdinalIgnoreCase)) {
        return 'runtime-dispatch'
    }

    if ($path.Contains('/Runtime/', [StringComparison]::OrdinalIgnoreCase)) {
        return 'runtime-model'
    }

    if ($path.Contains('/Persistence/', [StringComparison]::OrdinalIgnoreCase)) {
        return 'persistence-model'
    }

    if ($path.Contains('/Components/', [StringComparison]::OrdinalIgnoreCase) -or $path.Contains('/Pages/', [StringComparison]::OrdinalIgnoreCase) -or $path.Contains('/wwwroot/', [StringComparison]::OrdinalIgnoreCase)) {
        return 'ui-surface'
    }

    if ($path.Contains('/Templates/', [StringComparison]::OrdinalIgnoreCase)) {
        return 'template-service'
    }

    if ($path.Contains('/Canvas/', [StringComparison]::OrdinalIgnoreCase)) {
        return 'canvas-service'
    }

    return 'process-module-source'
}

function Get-ArchiveDecision {
    param([string] $Category)

    switch ($Category) {
        'template-input' { return 'migrate-template-input' }
        'template-service' { return 'replace-with-new-architecture' }
        'test-source' { return 'port-after-redesign' }
        'test-data' { return 'keep-as-reference' }
        'integration-reference' { return 'adapt-concepts' }
        'solution-reference' { return 'keep-as-reference' }
        'contract-model' { return 'adapt-concepts' }
        'driver-abstraction' { return 'adapt-concepts' }
        'driver-implementation' { return 'adapt-concepts' }
        'core-rule' { return 'adapt-concepts' }
        'runtime-dispatch' { return 'replace-with-new-architecture' }
        default { return 'replace-with-new-architecture' }
    }
}

function Get-DecisionReason {
    param(
        [string] $Category,
        [string] $Decision
    )

    switch ($Decision) {
        'migrate-template-input' { return 'Template files are preserved as migration input; the canonical schema will be redesigned in later subbundles.' }
        'port-after-redesign' { return 'The test captures required behavior but depends on old contracts and must be ported after the new architecture exists.' }
        'keep-as-reference' { return 'The file is retained as reference evidence and must not be used as production source.' }
        'adapt-concepts' { return "The $Category concepts are useful, but the implementation shape must be adapted to the target architecture." }
        default { return "The $Category implementation is archived for evidence and will be replaced by the target architecture." }
    }
}

function Get-RelatedRequirements {
    param([string] $Category)

    $requirements = [System.Collections.Generic.List[string]]::new()
    $requirements.Add('REQ-048')
    $requirements.Add('REQ-049')

    switch ($Category) {
        'template-input' {
            'REQ-031', 'REQ-032', 'REQ-033', 'REQ-034', 'REQ-035', 'REQ-036', 'REQ-037' | ForEach-Object { $requirements.Add($_) }
        }
        'template-service' {
            'REQ-031', 'REQ-032', 'REQ-033', 'REQ-034', 'REQ-035', 'REQ-036', 'REQ-037' | ForEach-Object { $requirements.Add($_) }
        }
        'driver-abstraction' {
            'REQ-006', 'REQ-007', 'REQ-008', 'REQ-009' | ForEach-Object { $requirements.Add($_) }
        }
        'driver-implementation' {
            'REQ-006', 'REQ-007', 'REQ-008', 'REQ-009' | ForEach-Object { $requirements.Add($_) }
        }
        'runtime-dispatch' {
            'REQ-002', 'REQ-003', 'REQ-015', 'REQ-020', 'REQ-021', 'REQ-022', 'REQ-023', 'REQ-026' | ForEach-Object { $requirements.Add($_) }
        }
        'runtime-model' {
            'REQ-002', 'REQ-003', 'REQ-026', 'REQ-027', 'REQ-028', 'REQ-029', 'REQ-030' | ForEach-Object { $requirements.Add($_) }
        }
        'persistence-model' {
            'REQ-015', 'REQ-016', 'REQ-017', 'REQ-018', 'REQ-019', 'REQ-026' | ForEach-Object { $requirements.Add($_) }
        }
        'ui-surface' {
            'REQ-030', 'REQ-051', 'REQ-052' | ForEach-Object { $requirements.Add($_) }
        }
        'integration-reference' {
            'REQ-039', 'REQ-040', 'REQ-051', 'REQ-052', 'REQ-055' | ForEach-Object { $requirements.Add($_) }
        }
    }

    return $requirements.ToArray() | Select-Object -Unique
}

function Get-RelatedFutureTests {
    param([string] $Category)

    switch ($Category) {
        'template-input' { return @('template-migration-chain-tests', 'template-compatibility-report-tests') }
        'template-service' { return @('canonical-template-schema-tests', 'sidecar-projection-hash-tests') }
        'driver-abstraction' { return @('driver-capability-catalog-tests', 'driver-contract-boundary-tests') }
        'driver-implementation' { return @('layered-driver-slice-tests', 'driver-redaction-policy-tests') }
        'runtime-dispatch' { return @('runtime-transition-integrity-tests', 'dispatcher-claim-idempotency-tests') }
        'runtime-model' { return @('runtime-state-machine-tests', 'run-history-projection-tests') }
        'persistence-model' { return @('event-store-outbox-replay-tests', 'artifact-ledger-projection-tests') }
        'ui-surface' { return @('process-ui-component-tests', 'process-ui-playwright-proof') }
        'test-source' { return @('ported-user-story-regression-tests') }
        'integration-reference' { return @('integration-boundary-compatibility-tests', 'process-api-compatibility-tests') }
        default { return @('architecture-boundary-regression-tests') }
    }
}

$archiveItems = [ordered]@{}

foreach ($sourceRoot in $sourceRoots) {
    foreach ($file in Invoke-GitLsFiles -PathSpecs @($sourceRoot)) {
        $archiveItems[$file] = [pscustomobject]@{
            SourcePath = $file
            Kind = 'source'
            ArchiveRelativePath = Get-ArchiveRelativePath -SourcePath $file -Kind 'source'
        }
    }
}

foreach ($file in Invoke-GitLsFiles -PathSpecs @($templateRoot)) {
    $archiveItems[$file] = [pscustomobject]@{
        SourcePath = $file
        Kind = 'template'
        ArchiveRelativePath = Get-ArchiveRelativePath -SourcePath $file -Kind 'template'
    }
}

foreach ($file in Invoke-GitLsFiles -PathSpecs $solutionFiles) {
    $archiveItems[$file] = [pscustomobject]@{
        SourcePath = $file
        Kind = 'solution'
        ArchiveRelativePath = Get-ArchiveRelativePath -SourcePath $file -Kind 'solution'
    }
}

$trackedTests = Invoke-GitLsFiles -PathSpecs @('tests')
foreach ($file in $trackedTests | Where-Object { $_ -match $processTerms }) {
    $archiveItems[$file] = [pscustomobject]@{
        SourcePath = $file
        Kind = 'test'
        ArchiveRelativePath = Get-ArchiveRelativePath -SourcePath $file -Kind 'test'
    }
}

$integrationRoots = @('src', 'tests', 'tools', 'Templates')
$coveredRoots = @($sourceRoots + @($templateRoot))
$integrationFiles = Invoke-RgList -Pattern $integrationTerms -Roots $integrationRoots
foreach ($file in $integrationFiles) {
    $normalized = $file.Replace('\', '/')
    if ($archiveItems.Contains($normalized)) {
        continue
    }

    if ($normalized.StartsWith('tests/', [StringComparison]::OrdinalIgnoreCase) -and $normalized -match $processTerms) {
        continue
    }

    $archiveItems[$normalized] = [pscustomobject]@{
        SourcePath = $normalized
        Kind = 'integration-reference'
        ArchiveRelativePath = Get-ArchiveRelativePath -SourcePath $normalized -Kind 'integration-reference'
    }
}

$entries = [System.Collections.Generic.List[object]]::new()
foreach ($item in $archiveItems.Values) {
    Copy-ArchiveFile -SourcePath $item.SourcePath -ArchiveRelativePath $item.ArchiveRelativePath
    $archiveFullPath = Join-Path $referenceRootPath $item.ArchiveRelativePath
    $sourceFullPath = Join-Path $repoRootPath $item.SourcePath
    $hash = (Get-FileHash -LiteralPath $archiveFullPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $sourceHash = (Get-FileHash -LiteralPath $sourceFullPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($hash -ne $sourceHash) {
        throw "Hash mismatch for $($item.SourcePath)"
    }

    $category = Get-ArchiveCategory -SourcePath $item.SourcePath -Kind $item.Kind
    $decision = Get-ArchiveDecision -Category $category
    $entries.Add([pscustomobject]@{
        sourcePath = $item.SourcePath
        archivePath = $item.ArchiveRelativePath
        sha256 = $hash
        fileSizeBytes = (Get-Item -LiteralPath $archiveFullPath).Length
        lineCount = Get-LineCount -Path $archiveFullPath
        category = $category
        decision = $decision
        reason = Get-DecisionReason -Category $category -Decision $decision
        relatedRequirements = @(Get-RelatedRequirements -Category $category)
        relatedFutureTests = @(Get-RelatedFutureTests -Category $category)
    })
}

$entries = $entries | Sort-Object sourcePath
$generatedAt = (Get-Date).ToUniversalTime().ToString('o')
$manifest = [pscustomobject]@{
    schemaVersion = '1.0'
    generatedUtc = $generatedAt
    sourceCommit = (git -C $repoRootPath rev-parse HEAD)
    sourceBranch = (git -C $repoRootPath branch --show-current)
    sourceSnapshot = 'snap-20260615171018-d225a84b'
    archiveRoot = 'codex/bundles/process-module-rewrite-reference-v1'
    entryCount = @($entries).Count
    entries = @($entries)
}

$manifestJsonPath = Join-Path $referenceRootPath 'manifest.json'
$manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $manifestJsonPath -Encoding UTF8

$categorySummary = $entries | Group-Object category | Sort-Object Name
$decisionSummary = $entries | Group-Object decision | Sort-Object Name
$kindSummary = $entries | ForEach-Object {
    [pscustomobject]@{
        kind = $_.archivePath.Split('/')[1]
        entry = $_
    }
} | Group-Object kind | Sort-Object Name

$manifestMarkdown = [System.Collections.Generic.List[string]]::new()
$manifestMarkdown.Add('# Process Module Rewrite Reference Archive v1')
$manifestMarkdown.Add('')
$manifestMarkdown.Add("Generated UTC: $generatedAt")
$manifestMarkdown.Add('')
$manifestMarkdown.Add("Source branch: ``$($manifest.sourceBranch)``")
$manifestMarkdown.Add('')
$manifestMarkdown.Add("Source commit: ``$($manifest.sourceCommit)``")
$manifestMarkdown.Add('')
$manifestMarkdown.Add("CodeAnalytics snapshot: ``$($manifest.sourceSnapshot)``")
$manifestMarkdown.Add('')
$manifestMarkdown.Add("Entry count: $($manifest.entryCount)")
$manifestMarkdown.Add('')
$manifestMarkdown.Add('## Category Summary')
$manifestMarkdown.Add('')
$manifestMarkdown.Add('| Category | Files |')
$manifestMarkdown.Add('| --- | ---: |')
foreach ($group in $categorySummary) {
    $manifestMarkdown.Add("| $($group.Name) | $($group.Count) |")
}

$manifestMarkdown.Add('')
$manifestMarkdown.Add('## Decision Summary')
$manifestMarkdown.Add('')
$manifestMarkdown.Add('| Decision | Files |')
$manifestMarkdown.Add('| --- | ---: |')
foreach ($group in $decisionSummary) {
    $manifestMarkdown.Add("| $($group.Name) | $($group.Count) |")
}

$manifestMarkdown.Add('')
$manifestMarkdown.Add('## Archive Area Summary')
$manifestMarkdown.Add('')
$manifestMarkdown.Add('| Area | Files |')
$manifestMarkdown.Add('| --- | ---: |')
foreach ($group in $kindSummary) {
    $manifestMarkdown.Add("| $($group.Name) | $($group.Count) |")
}

$manifestMarkdown.Add('')
$manifestMarkdown.Add('## Completeness Notes')
$manifestMarkdown.Add('')
$manifestMarkdown.Add('- Complete tracked source files were copied for the legacy Process module, Process core/contracts, and Process driver projects.')
$manifestMarkdown.Add('- Complete tracked files under `Templates/Processes` were copied as migration input.')
$manifestMarkdown.Add('- Process-named tracked tests and test data were copied.')
$manifestMarkdown.Add('- Integration touchpoints were copied as separate snippets when they were outside the complete source/template/test archive scope.')
$manifestMarkdown.Add('- Hashes were computed from archived files and checked against the source files during generation.')
$manifestMarkdown.Add('')
$manifestMarkdown.Add('## Manifest Fields')
$manifestMarkdown.Add('')
$manifestMarkdown.Add('Each `manifest.json` entry contains source path, archive path, SHA-256, file size, line count, category, reuse decision, reason, related requirements, and related future tests.')

$manifestMarkdownPath = Join-Path $referenceRootPath 'manifest.md'
$manifestMarkdown | Set-Content -LiteralPath $manifestMarkdownPath -Encoding UTF8

function Write-Inventory {
    param(
        [string] $Name,
        [object[]] $Rows,
        [string] $Description
    )

    $path = Join-Path $inventoriesRoot $Name
    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add("# $($Name -replace '\.md$', '' -replace '-', ' ')")
    $lines.Add('')
    $lines.Add($Description)
    $lines.Add('')
    $lines.Add("| Source | Archive | Category | Decision | Lines |")
    $lines.Add("| --- | --- | --- | --- | ---: |")
    foreach ($row in ($Rows | Sort-Object sourcePath)) {
        $lines.Add("| `$($row.sourcePath)` | `$($row.archivePath)` | $($row.category) | $($row.decision) | $($row.lineCount) |")
    }

    $lines | Set-Content -LiteralPath $path -Encoding UTF8
}

Write-Inventory -Name 'source-inventory.md' -Rows @($entries | Where-Object { $_.sourcePath -like 'src/CanDoItAll.Modules.Processes/*' -or $_.sourcePath -like 'src/CanDoItAll.Processes.*/*' }) -Description 'Complete tracked Process source archive inventory.'
Write-Inventory -Name 'test-inventory.md' -Rows @($entries | Where-Object { $_.sourcePath -like 'tests/*' }) -Description 'Process-related tests and test data archived before active removal.'
Write-Inventory -Name 'template-pack-inventory.md' -Rows @($entries | Where-Object { $_.sourcePath -like 'Templates/Processes/*' }) -Description 'Legacy template pack files preserved as migration input.'
Write-Inventory -Name 'integration-reference-inventory.md' -Rows @($entries | Where-Object { $_.category -eq 'integration-reference' }) -Description 'Process integration touchpoints outside the complete Process source/template archive scope.'

Write-Output "Reference archive generated: $referenceRootPath"
Write-Output "Manifest entries: $($manifest.entryCount)"
foreach ($group in $categorySummary) {
    Write-Output ("Category {0}: {1}" -f $group.Name, $group.Count)
}
