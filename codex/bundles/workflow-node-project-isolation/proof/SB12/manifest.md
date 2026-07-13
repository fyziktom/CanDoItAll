# SB12 Proof Manifest

## Status

- Subbundle: `SB12 - API UI Workbench Adoption`
- Result: `Passed`
- Closure date: `2026-06-29`
- Next gate: `SB13 - Adoption Refactoring Hardening Checkpoint`

## Owned Requirements And Raw Notes

- R10, R11, R12, R13, R14, R15, and R17.
- Raw note scope: adopt the workflow isolation through API, Blazor UI, Workbench workflow nodes, and plugin-related user-visible failure paths without falling back to MAF internals or string-only diagnostics.
- Large-screen-only UI scope: small and medium viewport tests are intentionally skipped per the user instruction for this execution request.

## Implementation Summary

- Extended `WorkflowFailureDisplayFormatter` to resolve typed `WorkflowFailureDiagnosticEnvelope` payloads from `WorkflowEventRecord.PayloadJson`, return user-safe diagnostic messages, expose redacted technical detail, and redact legacy message fallback text.
- Updated the workflow page event list, run summary, run detail, and event detail surfaces to render formatter output instead of raw event messages.
- Updated workflow canvas editor exception display and Workbench workflow-node UI exception paths to use the shared formatter.
- Updated Workbench workflow-node status mapping to prefer typed failure diagnostics from runtime events before falling back to redacted legacy summary text.
- Added payload JSON to Workbench workflow run event summaries so workflow-node status paths can consume the same runtime event diagnostics as the Blazor workflow page.
- Adjusted the Workbench Playwright proof to large-screen-only scope by removing the small/mobile viewport segment.
- Updated the XLSX mapping workbook with SB12 adoption rows and rendered all workbook sheets.

## Verification

| Proof | Result | Transcript |
| --- | --- | --- |
| Entry gate | Passed | `bundle://proof/SB12/transcripts/entry-gate.txt` |
| Validation command manifest | Passed | `bundle://proof/SB12/transcripts/validation-command-manifest.txt` |
| Unit diagnostic tests | Passed, 13/13 | `bundle://proof/SB12/transcripts/unit-diagnostics-tests.txt` |
| Workflow UI component tests | Passed, 21/21 | `bundle://proof/SB12/transcripts/component-workflows-page-tests.txt` |
| Workflow API and Workbench integration tests | Passed, 14/14 | `bundle://proof/SB12/transcripts/api-workbench-integration-tests.txt` |
| Workflow shell large-screen Playwright proof | Passed, 1/1 | `bundle://proof/SB12/transcripts/playwright-workflow-shell-large.txt` |
| Workbench workflow-node large-screen Playwright proof | Passed, 1/1 | `bundle://proof/SB12/transcripts/playwright-workbench-workflow-node-large.txt` |
| Static adoption/no-fallback check | Passed | `bundle://proof/SB12/transcripts/static-adoption-check.txt` |
| Semantic source assertions | Passed | `bundle://proof/SB12/transcripts/semantic-source-assertions.txt` |
| Adversarial raw-message negative check | Passed | `bundle://proof/SB12/transcripts/adversarial-negative-check.txt` |
| Anti-stub audit | Passed | `bundle://proof/SB12/transcripts/anti-stub-audit.txt` |
| Workbook update and render | Passed; formula-error scan matched 0 entries | `bundle://proof/SB12/transcripts/workbook-update-and-render.txt` |
| Output isolation caveat | Documented | `bundle://proof/SB12/transcripts/output-isolation-caveat.txt` |
| Browser viewport scope | Large-screen-only scope documented | `bundle://proof/SB12/transcripts/browser-large-screen-scope.txt` |
| Prepared-stage validator after SB12 sync | Passed | `bundle://proof/SB12/transcripts/prepared-validator.txt` |
| Closure audit | Passed | `bundle://proof/SB12/transcripts/closure-audit.txt` |

## Browser And Workbook Artifacts

- `bundle://proof/SB12/browser/workflow-shell-runtime-large.png`
- `bundle://proof/SB12/browser/project-structure-add-workflow-desktop.png`
- `bundle://proof/SB12/browser/project-structure-start-workflow-confirmation.png`
- `bundle://proof/SB12/browser/project-structure-workflow-selection-status.png`
- `bundle://proof/SB12/browser/project-structure-workflow-result-child-desktop.png`
- `bundle://proof/SB12/workbook-previews/summary.png`
- `bundle://proof/SB12/workbook-previews/source-map.png`
- `bundle://proof/SB12/workbook-previews/project-targets.png`
- `bundle://proof/SB12/workbook-previews/subbundles.png`
- `bundle://proof/SB12/workbook-previews/executor-categories.png`
- `bundle://proof/SB12/workbook-previews/plugin-consequences.png`
- `bundle://proof/SB12/workbook-previews/validation-matrix.png`
- `bundle://proof/SB12/workbook-previews/error-states.png`
- `bundle://proof/SB12/workbook-previews/performance-signals.png`

## Changed File Hashes

- `bundle://proof/SB12/changed-file-hashes.txt`

## Semantic Invariant Contract

- `bundle://proof/SB12/semantic-invariants.md`

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| `WorkflowEventRecord.PayloadJson` diagnostic payload | `repo://src/CanDoItAll.AgentFramework.Workflows.Core/WorkflowFailureDisplayFormatter.cs`; `bundle://proof/SB12/transcripts/unit-diagnostics-tests.txt` | `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor.cs`; `repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureWorkflowNodeService.cs`; `bundle://proof/SB12/transcripts/component-workflows-page-tests.txt` | `bundle://proof/SB12/transcripts/api-workbench-integration-tests.txt`; `bundle://proof/SB12/transcripts/playwright-workflow-shell-large.txt`; `bundle://proof/SB12/transcripts/playwright-workbench-workflow-node-large.txt` | `bundle://proof/SB12/transcripts/adversarial-negative-check.txt`; `bundle://proof/SB12/transcripts/anti-stub-audit.txt` |
| Shared workflow failure display formatter | `repo://src/CanDoItAll.AgentFramework.Workflows.Core/WorkflowFailureDisplayFormatter.cs`; `bundle://proof/SB12/transcripts/semantic-source-assertions.txt` | `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor`; `repo://src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.WorkflowNodes.cs`; `bundle://proof/SB12/transcripts/static-adoption-check.txt` | `bundle://proof/SB12/transcripts/component-workflows-page-tests.txt`; `bundle://proof/SB12/transcripts/api-workbench-integration-tests.txt` | `bundle://proof/SB12/transcripts/adversarial-negative-check.txt` |

## Commands

```powershell
dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-build --filter "WorkflowCoreExtractionTests|ProjectStructureWorkflowPreviewSimulationSupportTests" -m:1 -v:minimal -p:OutputPath=C:\repositories\CanDoItAll\artifacts\sb12-unit-output\
dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --no-build --filter "WorkflowsPageTests" -m:1 -v:minimal -p:OutputPath=C:\repositories\CanDoItAll\artifacts\sb12-components-output\
dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-build --filter "FullyQualifiedName~WorkflowApiIntegrationTests|Name=Email_workflow_uses_switch_and_creates_project_structure_task_nodes" -m:1 -v:minimal -p:OutputPath=C:\repositories\CanDoItAll\artifacts\sb12-integration-output\
$env:CANDOITALL_TEST_CONFIGURATION='sb12-playwright'; dotnet test tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj -c sb12-playwright --no-build --filter "FullyQualifiedName~WorkflowShellSmokeTests" -m:1 -v:minimal
$env:CANDOITALL_TEST_CONFIGURATION='sb12-playwright'; dotnet test tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj -c sb12-playwright --no-build --filter "FullyQualifiedName~Project_structure_workflow_nodes_can_be_added_started_and_inspected_in_browser" -m:1 -v:minimal
```

## Caveats

- Existing `CanDoItAll.Web` Debug output remains locked by a running app process. SB12 proof used isolated output folders and the `sb12-playwright` configuration instead of stopping that process.
- Browser proof is large-screen-only. Small and medium viewport tests were skipped because the user explicitly scoped the app to large screens for this initiative.

