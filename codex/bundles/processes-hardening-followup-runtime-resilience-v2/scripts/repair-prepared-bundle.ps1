$ErrorActionPreference = 'Stop'

$bundle = Split-Path -Parent $PSScriptRoot

New-Item -ItemType Directory -Force -Path (Join-Path $bundle 'inventories') | Out-Null

@'
# Inventory

## Source Inventory

- Process dispatch runtime: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch`
- Process runtime services: `repo://src/CanDoItAll.Modules.Processes/Runtime`
- Process launch/services: `repo://src/CanDoItAll.Modules.Processes/Services`, `repo://src/CanDoItAll.Modules.Processes/Launch`
- Process definition editor/linter: `repo://src/CanDoItAll.Modules.Processes/Definitions`
- Integration tests: `repo://tests/CanDoItAll.Tests.Integration`

## Validation Inventory

- Prepared bundle validator.
- Focused process dispatch and linter integration tests.
- Unit test project.
- Solution build.
- PostgreSQL-only source audit.
'@ | Set-Content (Join-Path $bundle 'inventories/01-source-inventory.md') -Encoding utf8

Copy-Item (Join-Path $bundle 'inputs/01-reviewed-source-observations.md') (Join-Path $bundle 'inputs/01-source-artifacts.md') -Force
Copy-Item (Join-Path $bundle 'architecture/01-target-runtime-architecture.md') (Join-Path $bundle 'architecture/01-target-solution.md') -Force
Copy-Item (Join-Path $bundle 'reviews/00-preparation-self-review.md') (Join-Path $bundle 'reviews/00-bundle-self-review.md') -Force

$readmePath = Join-Path $bundle 'README.md'
$readme = Get-Content $readmePath -Raw
if ($readme -notmatch 'Subbundle gate review:') {
    $readme = $readme -replace '- Execution status: `Not executed`', "- Execution status: ``Not executed```r`n- Subbundle gate review: ``Not started``"
}
Set-Content $readmePath $readme -Encoding utf8

$planPath = Join-Path $bundle 'plan/01-phase-plan.md'
$plan = Get-Content $planPath -Raw
if ($plan -notmatch '## Execution Order') {
    $order = @'
## Execution Order

1. `01-explicit-step-operation-contract-and-classifier-hardening`
2. `02-tool-policy-boundary-enforcement-and-metadata-no-autopromotion`
3. `03-manager-recovery-lineage-and-recovery-artifact-validation`
4. `04-workflow-subprocess-artifact-adapters-and-parent-versioning`
5. `05-upstream-materialization-unblock-and-resume-lifecycle`
6. `06-disposition-routing-guardrails`
7. `07-storage-backed-artifact-validation-and-explicit-modes`
8. `08-no-progress-retry-and-active-run-adoption-hardening`
9. `09-process-definition-lint-integration-and-template-quality-gates`
10. `10-generic-red-team-validation-suite`

'@
    $plan = $plan -replace '## Subbundle Dependency Map', ($order + '## Subbundle Dependency Map')
}

$criticalBullets = @'
- SB01 is critical because operation boundaries must be explicit before tool enforcement.
- SB02 is critical because metadata must not auto-promote read-only targets.
- SB03 is critical because recovery artifacts must carry valid recovery lineage.
- SB04 is critical because workflow/subprocess outputs must be typed before finalizer validation.
- SB05 is critical because missing upstream materialization must unblock downstream steps.
- SB06 is critical because branch routing must not mask missing artifact production.
- SB07 is critical because artifact validation must use storage-backed content and explicit modes.
- SB08 is critical because no-progress retries and active execution adoption affect runtime correctness.
- SB10 is critical because generic red-team validation proves software and non-software behavior.
- SB09 is required before closure and may run after urgent runtime fixes.
'@
$plan = $plan -replace 'All subbundles are critical except SB09 can be executed slightly later if runtime fixes are urgent\. SB09 is still required before closure\.', $criticalBullets
Set-Content $planPath $plan -Encoding utf8

Get-ChildItem (Join-Path $bundle 'subbundles') -Directory | ForEach-Object {
    $path = Join-Path $_.FullName 'README.md'
    $content = Get-Content $path -Raw
    foreach ($heading in @('Covered Inputs', 'Scope', 'Dependency Impact', 'Validation Depth', 'Browser Validation Logging', 'Progression Gate')) {
        $content = [regex]::Replace(
            $content,
            "(?ms)(## $heading\r?\n\r?\n)(?!- )([^#\r\n][^\r\n]*)",
            { param($m) $m.Groups[1].Value + '- ' + $m.Groups[2].Value.Trim() })
    }

    $content = $content -replace 'repo://src/CanDoItAll.Modules.Processes/Services/ProcessesService\*\.cs', 'repo://src/CanDoItAll.Modules.Processes/Services/ProcessesService.cs'
    $content = $content -replace 'repo://src/CanDoItAll.Infrastructure.Storage/\*\*', 'repo://src/CanDoItAll.Infrastructure/Storage/WorkspaceStorage.cs'
    $content = $content -replace 'repo://src/CanDoItAll.Modules.Processes/Pages/\*\*', 'repo://src/CanDoItAll.Modules.Processes/Pages/ProcessesPage.razor'
    Set-Content $path $content -Encoding utf8
}

@'
# Execution Report

## Status

Not executed.

## Subbundle Gate Results

| Subbundle | Entry gate | Closure gate | Downstream dependencies checked | Progression result | Notes |
| --- | --- | --- | --- | --- | --- |
| SB01 | Pending | Pending | Pending | Pending | Explicit operation contract |
| SB02 | Pending | Pending | Pending | Pending | Tool policy and metadata |
| SB03 | Pending | Pending | Pending | Pending | Recovery lineage |
| SB04 | Pending | Pending | Pending | Pending | Workflow/subprocess adapters |
| SB05 | Pending | Pending | Pending | Pending | Upstream unblock |
| SB06 | Pending | Pending | Pending | Pending | Disposition guardrails |
| SB07 | Pending | Pending | Pending | Pending | Artifact validation |
| SB08 | Pending | Pending | Pending | Pending | Retry/adoption |
| SB09 | Pending | Pending | Pending | Pending | Lint integration |
| SB10 | Pending | Pending | Pending | Pending | Red-team suite |

## Browser Validation Analytics

| Subbundle | Route | Viewport | Playwright MCP evidence | Screenshots | Result |
| --- | --- | --- | --- | --- | --- |
| SB10 | Pending | Pending | Pending | Pending | Pending |

## Analytics Review

Pending.

## Raw Note Closure

| Raw note | Status | Proof |
| --- | --- | --- |
| N001 | Pending | Runtime source review and proof |
| N002 | Pending | Runtime source review and proof |
| N003 | Pending | Red-team proof |
| N004 | Pending | Runtime source review and proof |
| N005 | Pending | Boundary and validation proof |
| N006 | Pending | Recovery/lint proof |
| N007 | Pending | Lint and closure proof |
'@ | Set-Content (Join-Path $bundle 'reviews/01-execution-report.md') -Encoding utf8
