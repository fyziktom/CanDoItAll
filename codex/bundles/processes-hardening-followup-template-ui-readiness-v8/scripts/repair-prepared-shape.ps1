$ErrorActionPreference = 'Stop'

$BundleRoot = Split-Path -Parent $PSScriptRoot

function Write-BundleFile {
    param(
        [Parameter(Mandatory = $true)]
        [string] $RelativePath,
        [Parameter(Mandatory = $true)]
        [string] $Content
    )

    $path = Join-Path $BundleRoot $RelativePath
    $directory = Split-Path -Parent $path
    if (-not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    Set-Content -LiteralPath $path -Value $Content.TrimStart() -Encoding utf8
}

function Get-Section {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Content,
        [Parameter(Mandatory = $true)]
        [string] $Heading
    )

    $pattern = "(?ms)^## $([regex]::Escape($Heading))\s*(.*?)(?=^## |\z)"
    $match = [regex]::Match($Content, $pattern)
    if (-not $match.Success) {
        return ''
    }

    return $match.Groups[1].Value.Trim()
}

function Get-SubbundleNumber {
    param([Parameter(Mandatory = $true)][string] $Name)

    return $Name.Substring(0, 2)
}

function Get-SubbundleKey {
    param([Parameter(Mandatory = $true)][string] $Name)

    return "SB$($Name.Substring(0, 2))"
}

function Get-CoveredInputs {
    param([Parameter(Mandatory = $true)][string] $SubbundleKey)

    switch ($SubbundleKey) {
        'SB01' { return @('RQ01 compile/build integrity', 'F01 potential missing ProcessStepRecoveryOption.None') }
        'SB02' { return @('RQ05 API/tool/skill parity', 'F04 Processes API governance surface') }
        'SB03' { return @('RQ02 typed template operation contracts', 'F03 mixed template migration state') }
        'SB04' { return @('RQ03 Blazor boundary correctness', 'F02 Blazor validation/revalidation mutation drift') }
        'SB05' { return @('RQ04 Tetris WASM PWA readiness', 'F02 Blazor template boundary dependency') }
        'SB06' { return @('RQ02 typed template operation contracts', 'RQ03 Blazor boundary correctness') }
        'SB07' { return @('RQ06 project-structure tool governance', 'F05 project-structure writeback tool classification') }
        'SB08' { return @('RQ02 typed template operation contracts', 'F03 non-Blazor template migration') }
        'SB09' { return @('RQ08 workflow/subprocess mappings') }
        'SB10' { return @('RQ07 unified artifact validation', 'F06 manual/API transition validation weakness') }
        'SB11' { return @('RQ07 unified artifact validation', 'RQ08 workflow/subprocess mappings') }
        'SB12' { return @('RQ05 API/tool/skill parity', 'RQ07 unified artifact validation') }
        'SB13' { return @('RQ05 API/tool/skill parity', 'F04 Processes API skill is shallow') }
        'SB14' { return @('RQ02 typed template operation contracts', 'RQ04 Tetris WASM PWA readiness') }
        'SB15' { return @('RQ03 Blazor boundary correctness', 'RQ04 Tetris WASM PWA readiness') }
        'SB16' { return @('RQ09 PostgreSQL-only generic core', 'RQ10 red-team closure') }
        default { return @('RQ09 PostgreSQL-only generic core') }
    }
}

function Get-SourceReferences {
    param([Parameter(Mandatory = $true)][string] $SubbundleKey)

    switch ($SubbundleKey) {
        'SB01' { return @('repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionEnums.cs', 'repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs') }
        'SB02' { return @('repo://src/CanDoItAll.Web/Api/ProcessesApi.cs', 'repo://codex/skills/candoitall-api-processes/SKILL.md') }
        'SB03' { return @('repo://Templates/Processes/manifest.json', 'repo://Templates/Processes/processes') }
        'SB04' { return @('repo://Templates/Processes/processes/blazor-app-delivery', 'repo://tests/CanDoItAll.Tests.Integration/ProcessDefinitionLinterTests.cs') }
        'SB05' { return @('repo://Templates/Processes/processes/blazor-app-delivery', 'repo://Templates/Processes/seed-catalog/baseline-scenarios.json') }
        'SB06' { return @('repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessStepOperationContractState.cs', 'repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.OperationContractResolver.cs') }
        'SB07' { return @('repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs', 'repo://tests/CanDoItAll.Tests.Unit/AgentToolInvocationPolicyTests.cs') }
        'SB08' { return @('repo://Templates/Processes/processes/customer-onboarding', 'repo://Templates/Processes/processes/business-plan-development', 'repo://Templates/Processes/processes/incident-response') }
        'SB09' { return @('repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/WorkflowSubprocessArtifactMapper.cs', 'repo://src/CanDoItAll.Modules.Processes/Components/ProcessArtifactExpectationEditor.razor') }
        'SB10' { return @('repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.StepTransitions.cs', 'repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs') }
        'SB11' { return @('repo://src/CanDoItAll.Modules.Processes/Runtime', 'repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch') }
        'SB12' { return @('repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessBlockStateClassifier.cs', 'repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs') }
        'SB13' { return @('repo://codex/skills/candoitall-api-processes/SKILL.md', 'repo://Templates/Processes/README.md') }
        'SB14' { return @('repo://Templates/Processes/seed-catalog/baseline-scenarios.json', 'repo://src/CanDoItAll.Modules.Processes/Templates/ProcessTemplatePackScenarios.cs') }
        'SB15' { return @('repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunsTab.razor', 'repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunStepsDialog.razor') }
        'SB16' { return @('repo://CanDoItAll.slnx', 'repo://Templates/Processes', 'repo://codex/bundles/processes-hardening-followup-template-ui-readiness-v8/reviews/01-execution-report.md') }
        default { return @('repo://src/CanDoItAll.Modules.Processes') }
    }
}

function Format-Bullets {
    param([Parameter(Mandatory = $true)][string[]] $Values)

    return ($Values | ForEach-Object { "- $_" }) -join [Environment]::NewLine
}

Write-BundleFile 'inputs/00-original-request.md' @'
# Original Request

Architect prepared `C:\repositories\CanDoItAll\codex\bundles\processes-hardening-followup-template-ui-readiness-v8` and requested Codex execute the bundle fully with implementation, validation, and tests.

The branch context captured by the prepared bundle is `process-hardening` / connector-visible `processes-hardening`, reviewed at `phase7` / `ca898eccf32664b60e996bf806a035067675c11e`.
'@

Write-BundleFile 'inputs/01-source-artifacts.md' @'
# Source Artifacts

- `bundle://README.md`
- `bundle://analysis/01-current-state.md`
- `bundle://analysis/02-verified-findings.md`
- `bundle://analysis/03-template-review-notes.md`
- `bundle://requirements/01-normalized-requirements.md`
- `bundle://requirements/02-runtime-invariants.md`
- `bundle://plan/01-phase-plan.md`
- `bundle://traceability/01-requirement-traceability.md`
- `bundle://subbundles/01-build-breaker-and-compile-integrity/README.md`
- `bundle://subbundles/02-process-api-tool-openapi-contract-parity/README.md`
- `bundle://subbundles/03-template-inventory-and-governance-matrix/README.md`
- `bundle://subbundles/04-blazor-template-boundary-corrections/README.md`
- `bundle://subbundles/05-tetris-wasm-pwa-template-readiness/README.md`
- `bundle://subbundles/06-refactor-checkpoint-a-contract-normalization/README.md`
- `bundle://subbundles/07-project-structure-tool-classification-and-policy/README.md`
- `bundle://subbundles/08-nonsoftware-template-migration/README.md`
- `bundle://subbundles/09-workflow-subprocess-output-mapping-hardening/README.md`
- `bundle://subbundles/10-unified-artifact-validation-for-api-transitions/README.md`
- `bundle://subbundles/11-refactor-checkpoint-b-runtime-validation-services/README.md`
- `bundle://subbundles/12-block-recovery-health-and-dashboard-readiness/README.md`
- `bundle://subbundles/13-process-skill-and-documentation-update/README.md`
- `bundle://subbundles/14-template-baseline-scenarios-and-seed-pack/README.md`
- `bundle://subbundles/15-ui-test-preflight-for-tetris-process-run/README.md`
- `bundle://subbundles/16-final-red-team-and-closure/README.md`
'@

Write-BundleFile 'inputs/02-structured-input.md' @'
# Structured Input

| Raw note | Exact wording | Normalized requirements | Owning subbundles | Planned proof |
| --- | --- | --- | --- | --- |
| F01 | Potential compile breaker: `ProcessRuntimeViewModels.cs` references `ProcessStepRecoveryOption.None`, while `ProcessDefinitionEnums.cs` currently shows `ProcessStepRecoveryOption` without `None`. | RQ01 | SB01 | Failing build or source assertion, enum/read-model fix, build and targeted regression proof. |
| F02 | Several Blazor template steps still grant `MutateProductTarget` / `ExternalProductTargetMutable` to review, revalidation, writeback, or escalation-style steps where product mutation is not appropriate. | RQ02, RQ03, RQ04 | SB03, SB04, SB05, SB15 | Template audit, negative tests for forbidden mutation, template validation, UI preflight proof. |
| F03 | Non-Blazor templates remain behind the new typed operation-contract model. | RQ02 | SB03, SB08, SB14 | Manifest-wide template contract audit and migration tests. |
| F04 | The Processes API skill exists, but it is still too shallow for the new governance model. | RQ05 | SB02, SB13 | API/tool round-trip tests, skill/docs source assertions, examples. |
| F05 | Project-structure writeback tools appear in process template instructions, but the generic tool policy registration/enforcement surface does not visibly classify `project_structure_*` mutation tools. | RQ06 | SB07 | Tool policy red-team tests and source assertions. |
| F06 | Manual/API step transitions still need proof that they use finalizer-grade artifact validation, not a lighter kind/title/trust check. | RQ07 | SB10, SB11, SB12 | Shared validator implementation, failing-first weak artifact transition test, passing finalizer-equivalent proof. |
| F07 | Template pack metadata still says software process template pack while the pack contains non-software templates. | RQ02, RQ09 | SB08, SB13, SB16 | Template metadata/docs audit and final red-team closure. |
'@

Write-BundleFile 'architecture/01-target-solution.md' @'
# Target Solution

## Architecture Intent

- Keep the Processes runtime generic and PostgreSQL-only; Tetris and Blazor WASM PWA details belong in templates, baseline scenarios, launch profiles, documentation, and tests.
- Treat typed operation contracts as the authoritative policy surface for process steps, API/tool payloads, template projection, dispatch metadata, and validation.
- Route manual/API step completion through the same artifact-validation semantics used by automation finalization.
- Make project-structure mutation tools explicit governed external actions, not ambient product mutation.
- Keep Blazor UI components focused on orchestration and visibility; move non-trivial runtime validation logic into services.

## Boundaries

- UI: Blazor components and pages under `repo://src/CanDoItAll.Modules.Processes/Components` and `repo://src/CanDoItAll.Modules.Processes/Pages`.
- Application/runtime services: process services, dispatch services, validation services, template services, and read-model projectors under `repo://src/CanDoItAll.Modules.Processes`.
- Domain contracts: definitions, operation contracts, target scopes, recovery enums, and artifact mapping fields under `repo://src/CanDoItAll.Modules.Processes/Definitions`.
- Infrastructure: EF persistence, PostgreSQL migrations, API endpoints, MAF tools, and skill/documentation surfaces.

## Validation Strategy

- Start with a failing-first or adversarial proof for each behavior change.
- Close each critical subbundle with `bundle://proof/SBxx/manifest.md`, `bundle://proof/SBxx/semantic-invariants.md`, command transcripts, changed-file hashes, source assertions, anti-stub audit, and downstream smoke proof when required.
- Use Playwright/browser proof only for UI-visible changes and the Tetris UI preflight; template-only subbundles record N/A browser analytics unless they change rendered UI.
'@

Write-BundleFile 'analysis/02-assumptions-and-risks.md' @'
# Assumptions And Risks

## Working Assumptions

- The bundle scope is a follow-up hardening pass after phase7, not a request to run the full Tetris UI scenario end to end.
- The repository already contains PostgreSQL migrations and no SQLite runtime path should be introduced.
- `Templates/Processes/manifest.json` is the authoritative template inventory.
- Existing process API, MAF tool, template, and runtime tests should be extended before adding new test projects.

## Critical Path Risks

- SB01 compile integrity blocks meaningful downstream proof if the solution cannot build.
- SB04, SB06, SB07, and SB10 are critical foundations because later template, UI, and API tests rely on their policy semantics.
- SB13 can affect active Codex skill behavior; if it changes skills, active skill-root synchronization proof is required before dependent validation is trusted.

## Validation Risks

- Source-text assertions alone can pass while runtime behavior remains wrong; behavior-changing subbundles need positive and negative execution proof.
- Template audits can pass on exact fixture names while missing new templates; manifest-driven enumeration is required.
- Browser screenshots without action assertions do not prove the Tetris preflight can be debugged.

## Reopen Triggers

- Reopen SB01 if any later build or enum/default assertion fails.
- Reopen SB04 or SB06 if a later template step can mutate product files outside implementation or repair roles.
- Reopen SB07 if project-structure mutation tools bypass `ExecuteExternalAction`.
- Reopen SB10 or SB11 if API/manual transitions can complete a required-artifact step with weak or unrelated artifacts.
- Reopen SB15 if UI preflight cannot expose enough run, artifact, block, or recovery data to debug the planned Tetris process run.
'@

Write-BundleFile 'README.md' @'
# processes-hardening-followup-template-ui-readiness-v8

## Validation Summary

- Bundle preparation status: `Repaired for current validator`
- Bundle readiness gate: `Pending prepared-stage rerun`
- Execution status: `Not started`
- Subbundle gate review: `Not started`
- Final closure gate: `Not started`
- Browser validation analytics: `Not started`

## Reviewed branch context

- Repository: `fyziktom/CanDoItAll`
- User branch name: `process-hardening`
- GitHub connector-visible branch: `processes-hardening`
- Reviewed head: `phase7` / `ca898eccf32664b60e996bf806a035067675c11e`
- PostgreSQL-only requirement remains active.

## Purpose

This bundle verifies whether the phase7 API/read-model/template work is production-ready and prepares the Processes module for the next planned UI test: creating a simple **Tetris Blazor WASM PWA** through the process runtime.

## Most Important Current Findings

1. Potential compile breaker: `ProcessRuntimeViewModels.cs` references `ProcessStepRecoveryOption.None`, while `ProcessDefinitionEnums.cs` currently shows `ProcessStepRecoveryOption` without `None`.
2. Several Blazor template steps still grant `MutateProductTarget` / `ExternalProductTargetMutable` to review, revalidation, writeback, or escalation-style steps where product mutation is not appropriate.
3. Non-Blazor templates remain behind the new typed operation-contract model.
4. The Processes API skill exists, but it is still too shallow for the new governance model.
5. Project-structure writeback tools appear in process template instructions, but the generic tool policy registration/enforcement surface does not visibly classify `project_structure_*` mutation tools.
6. Manual/API step transitions still need proof that they use finalizer-grade artifact validation, not a lighter kind/title/trust check.

## Expected Execution Style

Execute subbundles in order. Run each refactor checkpoint before continuing. Do not stop after only fixing the compile issue; the next planned UI test depends on template quality and API/skill clarity.
'@

Write-BundleFile 'plan/01-phase-plan.md' @'
# Phase Plan

## Execution Order

1. SB01 compile/build integrity.
2. SB02 API/tool/OpenAPI parity.
3. SB03 template inventory matrix.
4. SB04 Blazor template boundary corrections.
5. SB05 Tetris WASM PWA readiness.
6. SB06 refactor checkpoint A.
7. SB07 project-structure tool policy.
8. SB08 non-software template migration.
9. SB09 workflow/subprocess output mapping hardening.
10. SB10 unified artifact validation for API transitions.
11. SB11 refactor checkpoint B.
12. SB12 block/recovery health readiness.
13. SB13 process skill and documentation update.
14. SB14 baseline scenarios and seed pack.
15. SB15 UI test preflight.
16. SB16 final red-team and closure.

## Subbundle Dependency Map

```mermaid
flowchart TD
    SB01["SB01 Compile integrity"] --> SB02["SB02 API/tool/OpenAPI parity"]
    SB02 --> SB03["SB03 Template inventory"]
    SB03 --> SB04["SB04 Blazor boundary corrections"]
    SB04 --> SB05["SB05 Tetris WASM PWA readiness"]
    SB05 --> SB06["SB06 Contract normalization checkpoint"]
    SB06 --> SB07["SB07 Project-structure tool policy"]
    SB07 --> SB08["SB08 Non-software template migration"]
    SB08 --> SB09["SB09 Workflow/subprocess mappings"]
    SB09 --> SB10["SB10 Unified artifact validation"]
    SB10 --> SB11["SB11 Runtime validation services checkpoint"]
    SB11 --> SB12["SB12 Block/recovery health"]
    SB12 --> SB13["SB13 Skill and documentation update"]
    SB13 --> SB14["SB14 Baseline scenarios"]
    SB14 --> SB15["SB15 UI test preflight"]
    SB15 --> SB16["SB16 Final red-team closure"]
```

## Critical Subbundles

- SB01 is a critical foundation because downstream proof is meaningless when the solution cannot build.
- SB04 is a critical foundation because Blazor template mutation boundaries gate the planned Tetris process run.
- SB06 is a critical foundation because operation contract normalization is reused by editor save, import/export, template projection, lint, dispatch metadata, and tests.
- SB07 is a critical foundation because project-structure mutation must be governed before writeback templates can be trusted.
- SB10 is a critical foundation because manual/API step completion must not bypass finalizer-grade artifact validation.
- SB15 is a critical foundation for browser/UI readiness because it defines the proof surface for the upcoming Tetris run.
- SB16 is critical final verification and red-team closure.

## Phase Gates

- SB01 gate: build and enum/default proof pass before API/tool parity work starts.
- SB04 gate: manifest-driven Blazor template audit and negative mutation tests pass before Tetris template readiness starts.
- SB06 gate: one authoritative operation-contract normalizer is used by all named surfaces before project-structure policy work starts.
- SB07 gate: project-structure mutation tools are classified and rejected for read-only contracts before non-software template migration starts.
- SB10 gate: API/manual transition tests reject weak required artifacts through the shared validator before runtime service refactoring starts.
- SB15 gate: UI preflight evidence, diagnostics surfaces, and browser-proof expectations are documented before final red-team closure starts.

## Minimum Validation Commands

```powershell
dotnet build CanDoItAll.slnx --no-restore
dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~AgentToolInvocationPolicyTests"
dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ProcessRunAutomationDispatchServiceTests|FullyQualifiedName~ProcessesServiceIntegrationTests|FullyQualifiedName~ProcessDefinitionLinterTests|FullyQualifiedName~ApiIntegrationTests"
dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --no-restore --filter "FullyQualifiedName~ProcessStepEditorFormTests"
rg -n "Sqlite|SQLite|UseSqlite|Migrations.Sqlite" src tests Templates codex -S
```

## Template-Specific Audit Commands

```powershell
rg -n '"AllowedOperations"|"OperationTargetScope"' Templates/Processes/processes -S
rg -n '"MutateProductTarget"|"ExternalProductTargetMutable"' Templates/Processes/processes/blazor-* -S
rg -n 'project_structure_asset_create|project_structure_node_create|project_structure_' src Templates codex -S
```
'@

Write-BundleFile 'reviews/00-bundle-self-review.md' @'
# Bundle Self Review

## QA Review

- Pass condition: every raw finding maps to an owning subbundle, planned proof, and closure status.
- Current decision: ready for execution after prepared-stage validator passes.

## Architecture Review

- Pass condition: runtime logic remains generic, PostgreSQL-only, and separated from Blazor/Tetris template specifics.
- Current decision: ready for execution after prepared-stage validator passes.

## Manager Review

- Pass condition: ordered subbundles, dependency gates, execution report rows, and proof artifacts make progress auditable.
- Current decision: ready for execution after prepared-stage validator passes.
'@

Write-BundleFile 'reviews/01-execution-report.md' @'
# Execution Report

## Status

- Pending implementation.

## Summary

Execution has not started. Prepared-stage bundle repair was required before production code changes because the current validator rejected the original lightweight bundle shape.

## Subbundle Status Table

| Subbundle | Status | Notes |
| --- | --- | --- |
| SB01 | Pending | |
| SB02 | Pending | |
| SB03 | Pending | |
| SB04 | Pending | |
| SB05 | Pending | |
| SB06 | Pending | |
| SB07 | Pending | |
| SB08 | Pending | |
| SB09 | Pending | |
| SB10 | Pending | |
| SB11 | Pending | |
| SB12 | Pending | |
| SB13 | Pending | |
| SB14 | Pending | |
| SB15 | Pending | |
| SB16 | Pending | |

## Subbundle Gate Results

| Subbundle | Entry gate | Closure gate | Downstream dependencies checked | Progression result | Notes |
| --- | --- | --- | --- | --- | --- |

## Browser Validation Analytics

| Subbundle | Route | Viewport | Playwright MCP evidence | Screenshots | Result |
| --- | --- | --- | --- | --- | --- |

## Analytics Review

Pending execution.

## Raw Note Closure

| Raw note | Status | Proof |
| --- | --- | --- |
| F01 | Pending | |
| F02 | Pending | |
| F03 | Pending | |
| F04 | Pending | |
| F05 | Pending | |
| F06 | Pending | |
| F07 | Pending | |
'@

$subbundleDirectories = Get-ChildItem -LiteralPath (Join-Path $BundleRoot 'subbundles') -Directory | Sort-Object Name
foreach ($directory in $subbundleDirectories) {
    $readmePath = Join-Path $directory.FullName 'README.md'
    $content = Get-Content -LiteralPath $readmePath -Raw
    $subbundleKey = Get-SubbundleKey $directory.Name
    $number = Get-SubbundleNumber $directory.Name
    $goal = Get-Section -Content $content -Heading 'Goal'
    $requiredWork = Get-Section -Content $content -Heading 'Required work'
    $requiredProof = Get-Section -Content $content -Heading 'Required proof'
    $closureCriteria = Get-Section -Content $content -Heading 'Closure criteria'
    $coveredInputs = Format-Bullets (Get-CoveredInputs $subbundleKey)
    $sourceReferences = Format-Bullets (Get-SourceReferences $subbundleKey)
    $previous = if ([int]$number -eq 1) { 'None; this is the first execution gate.' } else { "SB$(([int]$number - 1).ToString('00')) closure gate is Completed or honestly Blocked with an explicit follow-up." }
    $browserLogging = if ($subbundleKey -in @('SB05', 'SB15', 'SB16')) {
        '- Record route, viewport, Playwright MCP evidence, screenshot paths, console assertions, and result in `bundle://reviews/01-execution-report.md` when browser-visible proof is produced.'
    } else {
        '- N/A for direct browser rendering unless implementation changes browser-visible behavior; still record the N/A decision in `bundle://reviews/01-execution-report.md`.'
    }

    $newContent = @"
# ${subbundleKey}: $($directory.Name.Substring(3))

## Status

- Ready

## Objective

$goal

## Covered Inputs

$coveredInputs

## Prerequisites

- $previous

## Exact Source References

$sourceReferences

## Scope

$requiredWork

## Dependency Impact

- Downstream subbundles cannot rely on this phase until the closure gate records proof in bundle://reviews/01-execution-report.md.
- Critical-foundation behavior must be reopened if later proof contradicts the stated invariant.

## Validation Depth

- Entry gate with current source references before editing.
- Failing-first or adversarial proof where behavior changes.
- Passing production-path test or build proof.
- Source assertions, changed-file hashes, anti-stub audit, and proof manifest under bundle://proof/$subbundleKey/.

## Implementation Steps

$requiredWork

## Scope Exceptions

- None planned. Any discovered exception must be recorded as a blocker, reopened subbundle, or concrete follow-up before closure.

## Do Not Do

- Do not hardcode Tetris behavior into generic process runtime code.
- Do not introduce SQLite paths or non-PostgreSQL persistence assumptions.
- Do not replace runtime proof with source-text-only assertions for behavior-changing work.
- Do not silently narrow raw notes that say all, every, must, or same flow.

## Acceptance Checklist

- Required work is implemented or explicitly blocked with a follow-up.
- Targeted tests and relevant audit commands pass.
- bundle://proof/$subbundleKey/manifest.md and bundle://proof/$subbundleKey/semantic-invariants.md are updated when this subbundle changes behavior.
- bundle://reviews/01-execution-report.md contains the subbundle gate row and raw-note closure evidence.

## Proof Required

$requiredProof
- Proof manifest: bundle://proof/$subbundleKey/manifest.md.
- Semantic invariant contract: bundle://proof/$subbundleKey/semantic-invariants.md.
- Command transcripts: bundle://proof/$subbundleKey/transcripts/.

## Browser Validation Logging

$browserLogging

## Progression Gate

- Closure gate passes only after proof artifacts exist, referenced paths resolve, and downstream dependency impact is recorded.
- Dependent subbundle may start only after the closure gate is Completed or the blocker is explicit.

## Suggested Agent Prompt

- Execute $subbundleKey exactly as scoped here. Preserve the generic Processes runtime boundary, add minimal production changes and tests, update proof artifacts, and rerun the relevant validation commands before closing.

## Original Closure Criteria

$closureCriteria
"@

    Set-Content -LiteralPath $readmePath -Value $newContent.TrimStart() -Encoding utf8
}
