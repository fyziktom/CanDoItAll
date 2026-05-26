param(
    [switch] $RequireTypedContracts,
    [string] $OutputPath = ''
)

$ErrorActionPreference = 'Stop'

$manifestPath = Join-Path 'Templates' (Join-Path 'Processes' 'manifest.json')
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$rows = New-Object System.Collections.Generic.List[object]

function Resolve-PlannedMigration {
    param(
        [string] $TemplateKey,
        [bool] $HasTypedContract
    )

    if ($HasTypedContract) {
        return ''
    }

    if ($TemplateKey.StartsWith('blazor-', [System.StringComparison]::OrdinalIgnoreCase)) {
        return 'SB04'
    }

    if ($TemplateKey -in @(
        'customer-onboarding',
        'business-plan-development',
        'incident-response',
        'architecture-decision-governance',
        'release-readiness-and-deployment',
        'oss-intake-supply-chain-governance',
        'ai-assisted-change-delivery'
    )) {
        return 'SB08'
    }

    return 'SB06'
}

foreach ($processEntry in $manifest.Processes) {
    $definitionPath = Join-Path 'Templates' (Join-Path 'Processes' (Join-Path $processEntry.RelativePath 'definition.json'))
    $definition = Get-Content $definitionPath -Raw | ConvertFrom-Json

    foreach ($step in $definition.Steps) {
        $allowedOperations = @($step.AllowedOperations)
        $hasAllowedOperations = $allowedOperations.Count -gt 0
        $hasTargetScope = -not [string]::IsNullOrWhiteSpace([string]$step.OperationTargetScope)
        $hasTypedContract = $hasAllowedOperations -and $hasTargetScope
        $requiredArtifactCount = @($step.ArtifactExpectations | Where-Object { $_.IsRequired }).Count
        $plannedMigration = Resolve-PlannedMigration $processEntry.Key $hasTypedContract

        $rows.Add([pscustomobject]@{
            TemplateKey = $processEntry.Key
            StepKey = $step.Key
            StepKind = $step.StepKind
            HasAllowedOperations = $hasAllowedOperations
            AllowedOperations = ($allowedOperations -join ',')
            HasOperationTargetScope = $hasTargetScope
            OperationTargetScope = [string]$step.OperationTargetScope
            HasBranchOutcomes = @($step.BranchOutcomes).Count -gt 0
            RequiredArtifactCount = $requiredArtifactCount
            ArtifactInputCount = @($step.ArtifactInputs).Count
            HasExceptionPolicy = -not [string]::IsNullOrWhiteSpace([string]$step.ExceptionPolicySummary)
            StrictGovernanceReady = $hasTypedContract
            PlannedMigration = $plannedMigration
            SourcePath = "repo://$($definitionPath.Replace('\', '/'))"
        })
    }
}

$missingTyped = @($rows | Where-Object { -not $_.StrictGovernanceReady })
$missingPlan = @($missingTyped | Where-Object { [string]::IsNullOrWhiteSpace($_.PlannedMigration) })

if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add('# Process Template Governance Matrix')
    $lines.Add('')
    $lines.Add("Generated on 2026-05-26 from repo://$manifestPath.")
    $lines.Add('')
    $lines.Add("Templates: $(@($manifest.Processes).Count)")
    $lines.Add("Steps: $($rows.Count)")
    $lines.Add("Steps missing typed contracts: $($missingTyped.Count)")
    $lines.Add('')
    $lines.Add('| Template | Step | Kind | Allowed operations | Target scope | Branches | Required artifacts | Artifact inputs | Exception policy | Ready | Planned migration |')
    $lines.Add('| --- | --- | --- | --- | --- | --- | ---: | ---: | --- | --- | --- |')
    foreach ($row in $rows) {
        $ready = if ($row.StrictGovernanceReady) { 'Yes' } else { 'No' }
        $branches = if ($row.HasBranchOutcomes) { 'Yes' } else { 'No' }
        $policy = if ($row.HasExceptionPolicy) { 'Yes' } else { 'No' }
        $allowed = if ([string]::IsNullOrWhiteSpace($row.AllowedOperations)) { '-' } else { $row.AllowedOperations }
        $scope = if ([string]::IsNullOrWhiteSpace($row.OperationTargetScope)) { '-' } else { $row.OperationTargetScope }
        $migration = if ([string]::IsNullOrWhiteSpace($row.PlannedMigration)) { '-' } else { $row.PlannedMigration }
        $lines.Add("| $($row.TemplateKey) | $($row.StepKey) | $($row.StepKind) | $allowed | $scope | $branches | $($row.RequiredArtifactCount) | $($row.ArtifactInputCount) | $policy | $ready | $migration |")
    }

    $directory = Split-Path $OutputPath -Parent
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    Set-Content -Path $OutputPath -Value $lines -Encoding utf8
}

Write-Output "Templates: $(@($manifest.Processes).Count)"
Write-Output "Steps: $($rows.Count)"
Write-Output "Steps missing typed contracts: $($missingTyped.Count)"
Write-Output "Steps missing migration plan: $($missingPlan.Count)"

if ($missingPlan.Count -gt 0) {
    $missingPlan | Select-Object TemplateKey, StepKey, SourcePath | Format-Table -AutoSize | Out-String | Write-Output
    exit 1
}

if ($RequireTypedContracts -and $missingTyped.Count -gt 0) {
    $missingTyped | Select-Object TemplateKey, StepKey, PlannedMigration, SourcePath | Format-Table -AutoSize | Write-Output
    exit 1
}

exit 0
