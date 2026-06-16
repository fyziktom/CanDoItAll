# SB18 Proof Manifest

## Scope

SB18 rebuilds step authoring over projection DTOs and typed commands. It covers US-011 through US-017: basic step information, execution strategy inputs, operation contracts, input/output contract summaries, branch routing with loop budgets, role assignments, artifact expectations, and subprocess mapping.

## Implementation Artifacts

- repo://src/CanDoItAll.Processes.Projections/ProcessDefinitionStepEditorProjectionContracts.cs
- repo://src/CanDoItAll.Processes.Projections/ProcessWorkspaceShellProjectionContracts.cs
- repo://src/CanDoItAll.Processes.Application/ProcessDefinitionStepEditorProjectionService.cs
- repo://src/CanDoItAll.Processes.Application/ProcessWorkspaceShellProjectionService.cs
- repo://src/CanDoItAll.Processes.Templates/ProcessTemplateStepSummaries.cs
- repo://src/CanDoItAll.Processes.Templates/ProcessTemplatePackLoader.cs
- repo://src/CanDoItAll.Modules.Processes/Components/ProcessDefinitionStepEditorPanel.razor
- repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceShell.razor
- repo://src/CanDoItAll.Modules.Processes/Services/ProcessWorkspaceProjectionClient.cs
- repo://src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs

## Test And Validation Artifacts

- bundle://proof/SB18/build-process-module.txt
- bundle://proof/SB18/build-solution-sb18.txt
- bundle://proof/SB18/build-playwright-project-sb18.txt
- bundle://proof/SB18/test-unit-step-editor-sb18.txt
- bundle://proof/SB18/test-components-process-shell-sb18.txt
- bundle://proof/SB18/test-playwright-process-shell-sb18.txt
- bundle://proof/SB18/tailwind-build-sb18.txt
- bundle://proof/SB18/source-assertions.txt
- bundle://proof/SB18/semantic-invariants.md
- bundle://proof/SB18/red-team-semantic-proof.md
- bundle://proof/SB18/story-coverage.md
- bundle://proof/SB18/browser-validation.md
- bundle://proof/SB18/codeanalytics-snapshot-summary.txt
- bundle://proof/SB18/bundle-validator-prepared-sb18.txt
- bundle://proof/SB18/git-diff-check-sb18.txt
- bundle://proof/SB18/performance-scan-summary.json
- bundle://proof/SB18/scans/projection-boundary-scan.txt
- bundle://proof/SB18/scans/old-symbol-scan.txt
- bundle://proof/SB18/scans/anti-stub-scan.txt
- bundle://proof/SB18/scans/performance-antipattern-scan.txt
- bundle://proof/SB18/changed-file-hashes.txt
- bundle://proof/SB18/line-counts.txt

## Browser Artifacts

- bundle://proof/SB18/browser/browser-validation-summary.txt
- bundle://proof/SB18/browser/processes-definition-step-editor.png
- bundle://proof/SB18/browser/processes-definition-canvas.png
- bundle://proof/SB18/browser/processes-definition-role-editor.png
- bundle://proof/SB18/browser/processes-global-definition-catalog.png
- bundle://proof/SB18/browser/processes-project-shell.png

## Production Behavior Artifact Matrix

| Signal | Producer | Consumer | Lifecycle | Negative or guard proof |
| --- | --- | --- | --- | --- |
| Step editor projection | `ProcessDefinitionStepEditorProjectionService.GetStepEditorAsync` | `ProcessWorkspaceShellProjectionService`, `ProcessWorkspaceShell.razor`, and `ProcessDefinitionStepEditorPanel.razor` | Template step summaries are converted to typed step, operation, route, artifact, role-binding, subprocess, command, lint, and version projections. | `projection-boundary-scan.txt` has no runtime, persistence, old observation, or DOM parsing references. |
| Step draft snapshot | `ProcessDefinitionStepEditorProjectionService` in-memory snapshot store | Step editor panel form state and save/add/map commands | A selected step draft carries typed operation target scope, allowed operations, route targets, loop budget, artifact expectations, and subprocess mapping. | `Step_editor_rejects_stale_version_tokens` rejects unsafe overwrites. |
| Step editor command | `ProcessDefinitionStepEditorPanel.razor` | Projection client and application service command boundary | Save, add-branch, add-artifact, and map-subprocess actions are emitted as `ProcessDefinitionStepEditorCommand` with typed keys and expected version token. | Component tests assert command kind, route target, artifact expectation, and subprocess mapping payloads. |
| Step command receipt | `ProcessDefinitionStepEditorProjectionService.ExecuteCommandAsync` | Shell state and step editor receipt UI | Accepted/rejected command status is returned with an updated projection and rendered in the panel. | Unit tests assert backward route and stale-token rejection; Playwright asserts saved/added/mapped receipts. |
| Template step authoring defaults | `ProcessTemplateStepSummaryBuilder.Build` and `ProcessTemplatePackLoader` | Step editor projection service | Canonical template JSON step fields become projection-friendly typed summaries without UI direct file reads. | Unit tests load a realistic temp template and assert operation, route, artifact, role, and subprocess fields. |

## Result

SB18 closure passes. Builds, focused tests, Tailwind, Playwright browser proof, static scans, and CodeAnalytics MCP all passed. The remaining CodeAnalytics findings are complexity warnings for large projection/editor services and existing generated/DI collector diagnostics; they are recorded for SB28 hardening rather than hidden as correctness proof.
