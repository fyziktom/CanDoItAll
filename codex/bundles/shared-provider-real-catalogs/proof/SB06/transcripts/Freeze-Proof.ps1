$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path '.').Path
$bundleRoot = 'codex/bundles/shared-provider-real-catalogs'
$proofRoot = Join-Path $bundleRoot 'proof/SB06'
$indexPath = Join-Path $proofRoot 'proof-artifacts.csv'
$artifacts = @(
    Get-ChildItem (Join-Path $bundleRoot 'proof/SB05'), $proofRoot -File -Recurse |
        Where-Object Name -ne 'proof-artifacts.csv'
    Get-ChildItem (Join-Path $bundleRoot 'subbundles/05-compact-provider-administration'),
        (Join-Path $bundleRoot 'subbundles/06-token-lifecycle-and-fresh-handoff') -File -Recurse
    Get-Item (Join-Path $bundleRoot 'README.md'),
        (Join-Path $bundleRoot 'architecture/06-administration-boundaries.md'),
        (Join-Path $bundleRoot 'inputs/05-compact-provider-and-token-administration.md'),
        (Join-Path $bundleRoot 'requirements/01-normalized-requirements.md'),
        (Join-Path $bundleRoot 'plan/01-phase-plan.md'),
        (Join-Path $bundleRoot 'traceability/01-requirement-traceability.md'),
        (Join-Path $bundleRoot 'reviews/01-execution-report.md'),
        (Join-Path $bundleRoot 'reviews/02-final-verifier.md'),
        (Join-Path $bundleRoot 'reviews/csharp-architecture-gate.md'),
        (Join-Path $bundleRoot 'subbundles/04-avatar-and-fresh-client/compose.yaml'),
        (Join-Path $bundleRoot 'subbundles/04-avatar-and-fresh-client/HANDOFF.md')
) | Sort-Object FullName -Unique
$rows = foreach ($artifact in $artifacts) {
    $relativePath = $artifact.FullName.Substring($repositoryRoot.Length + 1).Replace('\', '/')
    [pscustomobject] @{
        Path = $relativePath
        SHA256 = (Get-FileHash -LiteralPath $artifact.FullName -Algorithm SHA256).Hash
    }
}
$rows | Export-Csv -LiteralPath $indexPath -NoTypeInformation -Encoding UTF8
foreach ($row in Import-Csv -LiteralPath $indexPath) {
    if ((Get-FileHash -LiteralPath $row.Path -Algorithm SHA256).Hash -ne $row.SHA256) {
        throw "Artifact hash verification failed: $($row.Path)"
    }
}
Write-Output "Frozen and verified $($rows.Count) artifact hashes. Index excludes itself."
