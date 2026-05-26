$ErrorActionPreference = 'Stop'

$templateKeys = @(
    'blazor-app-delivery',
    'blazor-app-repair-fix',
    'blazor-backend-feature',
    'blazor-frontend-feature',
    'blazor-fullstack-feature'
)

$violations = New-Object System.Collections.Generic.List[string]

function Has-Operation {
    param(
        [object] $Step,
        [string] $Operation
    )

    return @($Step.AllowedOperations) -contains $Operation
}

function Is-MutatingStep {
    param([object] $Step)

    return (Has-Operation $Step 'MutateProductTarget') -or
        [string]$Step.OperationTargetScope -eq 'ExternalProductTargetMutable'
}

function Add-Violation {
    param(
        [string] $TemplateKey,
        [object] $Step,
        [string] $Message
    )

    $violations.Add("$TemplateKey/$($Step.Key): $Message")
}

foreach ($templateKey in $templateKeys) {
    $definitionPath = "Templates/Processes/processes/$templateKey/definition.json"
    $definition = Get-Content $definitionPath -Raw | ConvertFrom-Json

    foreach ($step in $definition.Steps) {
        $mutates = Is-MutatingStep $step

        if ($step.Key -eq 'resolve-blazor-contract') {
            if ($mutates) {
                Add-Violation $templateKey $step 'contract resolution must be read-only.'
            }

            if (-not (Has-Operation $step 'ReadProjectStructure')) {
                Add-Violation $templateKey $step 'contract resolution must read project structure.'
            }
        }

        if ($step.Key -in @('implement-blazor-change', 'repair-blazor-findings')) {
            if (-not $mutates) {
                Add-Violation $templateKey $step 'implementation and repair steps must be the only product-mutation steps.'
            }
        } elseif ($mutates) {
            Add-Violation $templateKey $step 'only implementation and repair steps may mutate product targets.'
        }

        if ($step.Key -in @('validate-blazor-runtime', 'revalidate-blazor-repair')) {
            foreach ($operation in @('RunValidation', 'LaunchRuntime', 'CaptureRuntimeProof', 'WriteManagedProcessArtifacts')) {
                if (-not (Has-Operation $step $operation)) {
                    Add-Violation $templateKey $step "validation/revalidation step is missing $operation."
                }
            }

            if ([string]$step.OperationTargetScope -ne 'ExternalProductTargetReadOnly') {
                Add-Violation $templateKey $step 'validation/revalidation step must target ExternalProductTargetReadOnly.'
            }
        }

        if ($step.Key -in @('record-blazor-results', 'record-blazor-results-after-repair')) {
            foreach ($operation in @('ExecuteExternalAction', 'WriteManagedProcessArtifacts')) {
                if (-not (Has-Operation $step $operation)) {
                    Add-Violation $templateKey $step "writeback step is missing $operation."
                }
            }

            if ($mutates) {
                Add-Violation $templateKey $step 'writeback step must not mutate product source files.'
            }
        }

        if ($step.Key -eq 'escalate-blazor-unresolved-repair') {
            foreach ($operation in @('EscalateOrDecide', 'WriteManagedProcessArtifacts')) {
                if (-not (Has-Operation $step $operation)) {
                    Add-Violation $templateKey $step "escalation step is missing $operation."
                }
            }

            if ($mutates) {
                Add-Violation $templateKey $step 'escalation step must not mutate product source files.'
            }
        }
    }
}

if ($violations.Count -gt 0) {
    $violations | Sort-Object | Write-Output
    Write-Output "ViolationCount: $($violations.Count)"
    exit 1
}

Write-Output "Blazor template boundary audit passed for $($templateKeys.Count) templates."
Write-Output 'ViolationCount: 0'
exit 0
