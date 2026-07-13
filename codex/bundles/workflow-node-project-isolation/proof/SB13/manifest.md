# SB13 Proof Manifest

## Status

- Subbundle: `SB13 - Adoption Refactoring Hardening Checkpoint`
- Result: `Passed`
- Closure date: `2026-06-29`
- Next gate: `SB14 - Regression Proof Cleanup And Docs`

## Owned Requirements And Raw Notes

- R11, R12, R13, R14, R15, R17, and R18.
- Raw note scope: forced hardening checkpoint after API/UI/Workbench adoption to prove the new isolated architecture is live and no hidden fallback path remains before final cleanup.
- Large-screen-only UI scope: small and medium viewport tests are intentionally skipped per the user instruction for this execution request.

## Implementation Summary

- Added `WorkflowAdoptionHardeningCheckpointTests` to guard API/UI/Workbench adoption against direct MAF workflow internals, old executor aliases, raw event-message display, duplicated typed diagnostic deserialization, stub markers, and generic failure phrases.
- Updated stale SB09 executor hardening expectation so the old `AgentFramework.Maf/Runtime/Workflows` folder is expected to be empty after SB11 adapter isolation.
- Ran focused architecture/no-fallback, no-generic-error, anti-stub, file-size/responsibility, and .NET performance audits.
- Re-ran unit, component, integration, and large-screen Playwright proof after hardening.
- Updated the XLSX mapping workbook and rendered all sheets.

## Verification

| Proof | Result | Transcript |
| --- | --- | --- |
| Entry gate | Passed | `bundle://proof/SB13/transcripts/entry-gate.txt` |
| Validation command manifest | Passed | `bundle://proof/SB13/transcripts/validation-command-manifest.txt` |
| Unit build | Passed, 0 warnings/errors | `bundle://proof/SB13/transcripts/unit-build.txt` |
| Focused adoption hardening tests | Passed, 5/5 | `bundle://proof/SB13/transcripts/focused-adoption-hardening-tests.txt` |
| Unit build after stale guard fix | Passed, 0 warnings/errors | `bundle://proof/SB13/transcripts/unit-build-after-stale-guard-fix.txt` |
| Combined hardening unit tests | Passed, 37/37 | `bundle://proof/SB13/transcripts/combined-hardening-unit-tests.txt` |
| Workflow UI component tests | Passed, 21/21 | `bundle://proof/SB13/transcripts/component-workflows-page-tests.txt` |
| Integration adoption smoke tests | Passed, 46/46 | `bundle://proof/SB13/transcripts/integration-adoption-smoke-tests.txt` |
| Architecture/no-fallback check | Passed | `bundle://proof/SB13/transcripts/architecture-no-fallback-check.txt` |
| No-generic-error audit | Passed | `bundle://proof/SB13/transcripts/no-generic-error-audit.txt` |
| File-size/responsibility review | Passed with approved exception for pre-existing large UI files | `bundle://proof/SB13/transcripts/file-size-responsibility-review.txt` |
| Focused performance scan | Passed; 0 critical findings | `bundle://proof/SB13/transcripts/performance-scan.txt` |
| Workflow shell large-screen Playwright proof | Passed, 1/1 | `bundle://proof/SB13/transcripts/playwright-workflow-shell-large.txt` |
| Workbench workflow-node large-screen Playwright proof | Passed, 1/1 | `bundle://proof/SB13/transcripts/playwright-workbench-workflow-node-large.txt` |
| Anti-stub audit | Passed | `bundle://proof/SB13/transcripts/anti-stub-audit.txt` |
| Workbook update and render | Passed; formula-error scan matched 0 entries | `bundle://proof/SB13/transcripts/workbook-update-and-render.txt` |
| Prepared-stage validator after SB13 sync | Passed | `bundle://proof/SB13/transcripts/prepared-validator.txt` |
| Closure audit | Passed | `bundle://proof/SB13/transcripts/closure-audit.txt` |

## Browser And Workbook Artifacts

- `bundle://proof/SB13/browser/workflow-shell-runtime-large.png`
- `bundle://proof/SB13/browser/project-structure-add-workflow-desktop.png`
- `bundle://proof/SB13/browser/project-structure-start-workflow-confirmation.png`
- `bundle://proof/SB13/browser/project-structure-workflow-selection-status.png`
- `bundle://proof/SB13/browser/project-structure-workflow-result-child-desktop.png`
- `bundle://proof/SB13/workbook-previews/summary.png`
- `bundle://proof/SB13/workbook-previews/source-map.png`
- `bundle://proof/SB13/workbook-previews/project-targets.png`
- `bundle://proof/SB13/workbook-previews/subbundles.png`
- `bundle://proof/SB13/workbook-previews/executor-categories.png`
- `bundle://proof/SB13/workbook-previews/plugin-consequences.png`
- `bundle://proof/SB13/workbook-previews/validation-matrix.png`
- `bundle://proof/SB13/workbook-previews/error-states.png`
- `bundle://proof/SB13/workbook-previews/performance-signals.png`

## Changed File Hashes

- `bundle://proof/SB13/changed-file-hashes.txt`

## Semantic Invariant Contract

- `bundle://proof/SB13/semantic-invariants.md`

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Adoption no-fallback guard | `repo://tests/CanDoItAll.Tests.Unit/WorkflowAdoptionHardeningCheckpointTests.cs`; `bundle://proof/SB13/transcripts/focused-adoption-hardening-tests.txt` | `repo://src/CanDoItAll.Web/Api/WorkflowsApi.cs`; `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor.cs`; `repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureWorkflowNodeService.cs` | `bundle://proof/SB13/transcripts/combined-hardening-unit-tests.txt`; `bundle://proof/SB13/transcripts/integration-adoption-smoke-tests.txt`; `bundle://proof/SB13/transcripts/playwright-workbench-workflow-node-large.txt` | `bundle://proof/SB13/transcripts/architecture-no-fallback-check.txt`; `bundle://proof/SB13/transcripts/anti-stub-audit.txt` |
| Typed diagnostic display boundary | `repo://src/CanDoItAll.AgentFramework.Workflows.Core/WorkflowFailureDisplayFormatter.cs`; `repo://tests/CanDoItAll.Tests.Unit/WorkflowAdoptionHardeningCheckpointTests.cs` | `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor`; `repo://src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.WorkflowNodes.cs` | `bundle://proof/SB13/transcripts/component-workflows-page-tests.txt`; `bundle://proof/SB13/transcripts/playwright-workflow-shell-large.txt` | `bundle://proof/SB13/transcripts/no-generic-error-audit.txt` |

## Caveats

- Large existing UI files remain as approved exceptions for SB13. Non-trivial typed diagnostic parsing is centralized in `WorkflowFailureDisplayFormatter`, and SB14 must document the exception in final closure.
- Browser proof is large-screen-only. Small and medium viewport tests were skipped because the user explicitly scoped the app to large screens for this initiative.

## Completed Validator Metadata Addendum

- Portable proof reference: bundle://proof/SB13/manifest.md
- Semantic invariant contract: bundle://proof/SB13/semantic-invariants.md
- Command transcript path: bundle://proof/SB13/transcripts/anti-stub-audit.txt
- Passing transcript: bundle://proof/SB13/transcripts/anti-stub-audit.txt
- Anti-stub audit transcript: bundle://proof/SB13/transcripts/anti-stub-audit.txt
- Failing-first test: N/A - process/no production behavior metadata addendum for completed-stage validator compatibility.
- SHA-256 changed-file hash: 5253EB38DE8FB54ED339E0775C3EC746532DA1D2FF9299DDA84C66CB224B8043 bundle://proof/SB13/manifest.md
- Invariant ID: SB13-final-closure

Moved checkout copy validation: portable bundle references can be copied to a moved checkout without machine-specific paths.

## Proof Claim To Code Matrix

| Capability claim | Required production source proof | Required test proof | Required negative fixture | Result |
| --- | --- | --- | --- | --- |
| portable proof | bundle://proof/SB13/manifest.md | bundle://proof/SB13/transcripts/metadata-compliance.txt | bundle://proof/SB13/transcripts/metadata-compliance.txt negative metadata proof | Verified pass: portable proof references are closed for SB13. |



