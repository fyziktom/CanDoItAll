$ErrorActionPreference = "Stop"

$RepoRoot = "C:\repositories\CanDoItAll"
$BundleRoot = Join-Path $RepoRoot "codex\bundles\workflow-node-project-isolation"
$Output = Join-Path $BundleRoot "proof\SB01\changed-file-hashes.txt"

$existingBundleFiles = @(
    "codex/bundles/workflow-node-project-isolation/README.md",
    "codex/bundles/workflow-node-project-isolation/reviews/01-execution-report.md",
    "codex/bundles/workflow-node-project-isolation/inventories/02-workflow-source-inventory.md",
    "codex/bundles/workflow-node-project-isolation/inventories/03-executor-inventory.md",
    "codex/bundles/workflow-node-project-isolation/inventories/04-test-and-validation-inventory.md",
    "codex/bundles/workflow-node-project-isolation/inventories/workflow-node-project-isolation-map.xlsx",
    "codex/bundles/workflow-node-project-isolation/architecture/02-project-map-and-adoption-boundary.md",
    "codex/bundles/workflow-node-project-isolation/traceability/01-requirement-traceability.md",
    "codex/bundles/workflow-node-project-isolation/subbundles/01-workflow-boundary-inventory-and-project-graph/README.md",
    "codex/bundles/workflow-node-project-isolation/subbundles/06-executor-abstractions-and-shared-helpers/README.md",
    "codex/bundles/workflow-node-project-isolation/subbundles/09-executor-refactoring-hardening-checkpoint/README.md",
    "codex/bundles/workflow-node-project-isolation/subbundles/10-workflow-template-and-descriptor-loading/README.md",
    "codex/bundles/workflow-node-project-isolation/subbundles/12-api-ui-workbench-adoption/README.md",
    "codex/bundles/workflow-node-project-isolation/subbundles/13-adoption-refactoring-hardening-checkpoint/README.md",
    "codex/bundles/workflow-node-project-isolation/subbundles/14-regression-proof-cleanup-and-docs/README.md",
    "codex/bundles/workflow-node-project-isolation/templates/subbundle-readme-template.md"
)

$newProofFiles = @(
    "codex/bundles/workflow-node-project-isolation/proof/SB01/manifest.md",
    "codex/bundles/workflow-node-project-isolation/proof/SB01/semantic-invariants.md",
    "codex/bundles/workflow-node-project-isolation/proof/SB01/transcripts/anti-stub-audit.txt",
    "codex/bundles/workflow-node-project-isolation/proof/SB01/transcripts/inventory-search.txt",
    "codex/bundles/workflow-node-project-isolation/proof/SB01/transcripts/prepared-validator.txt",
    "codex/bundles/workflow-node-project-isolation/proof/SB01/transcripts/semantic-surface-check.txt",
    "codex/bundles/workflow-node-project-isolation/proof/SB01/transcripts/source-assertions.txt",
    "codex/bundles/workflow-node-project-isolation/proof/SB01/transcripts/workbook-render.txt",
    "codex/bundles/workflow-node-project-isolation/proof/SB01/workbook-previews/error-states.png",
    "codex/bundles/workflow-node-project-isolation/proof/SB01/workbook-previews/executor-categories.png",
    "codex/bundles/workflow-node-project-isolation/proof/SB01/workbook-previews/formula-error-scan.ndjson",
    "codex/bundles/workflow-node-project-isolation/proof/SB01/workbook-previews/performance-signals.png",
    "codex/bundles/workflow-node-project-isolation/proof/SB01/workbook-previews/plugin-consequences.png",
    "codex/bundles/workflow-node-project-isolation/proof/SB01/workbook-previews/project-targets.png",
    "codex/bundles/workflow-node-project-isolation/proof/SB01/workbook-previews/sheet-list.ndjson",
    "codex/bundles/workflow-node-project-isolation/proof/SB01/workbook-previews/source-map.png",
    "codex/bundles/workflow-node-project-isolation/proof/SB01/workbook-previews/subbundles.png",
    "codex/bundles/workflow-node-project-isolation/proof/SB01/workbook-previews/summary.png",
    "codex/bundles/workflow-node-project-isolation/proof/SB01/workbook-previews/validation-matrix.png",
    "codex/bundles/workflow-node-project-isolation/proof/SB01/workbook-previews/workbook-summary.ndjson",
    "codex/bundles/workflow-node-project-isolation/proof/SB01/tools/inspect-workbook.mjs",
    "codex/bundles/workflow-node-project-isolation/proof/SB01/tools/inspect-workbook-ranges.mjs",
    "codex/bundles/workflow-node-project-isolation/proof/SB01/tools/update-workbook-sb01.mjs",
    "codex/bundles/workflow-node-project-isolation/proof/SB01/tools/write-sb01-hashes.ps1",
    "codex/bundles/workflow-node-project-isolation/proof/SB01/tools/write-sb01-transcripts.ps1"
)

$lines = New-Object System.Collections.Generic.List[string]

foreach ($relativePath in $existingBundleFiles) {
    $fullPath = Join-Path $RepoRoot $relativePath
    $hash = (Get-FileHash -LiteralPath $fullPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $lines.Add("repo://$relativePath | before=(bundle file ignored by git; pre-SB01 hash unavailable) | after=$hash")
}

foreach ($relativePath in $newProofFiles) {
    $fullPath = Join-Path $RepoRoot $relativePath
    $hash = (Get-FileHash -LiteralPath $fullPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $lines.Add("repo://$relativePath | before=(new file) | after=$hash")
}

Set-Content -LiteralPath $Output -Value $lines -Encoding UTF8
Write-Output "Wrote $($lines.Count) hash rows to $Output"
