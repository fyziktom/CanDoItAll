# SB17 Proof Manifest

## Scope

SB17 rebuilds the definition canvas over projection DTOs and typed canvas commands. It covers US-018 and US-019: canvas load/selection/route visibility and toolbox/recomposition behavior.

## Implementation Artifacts

- repo://src/CanDoItAll.Processes.Projections/ProcessDefinitionCanvasProjectionContracts.cs
- repo://src/CanDoItAll.Processes.Projections/ProcessWorkspaceShellProjectionContracts.cs
- repo://src/CanDoItAll.Processes.Application/ProcessDefinitionCanvasEditorProjectionService.cs
- repo://src/CanDoItAll.Processes.Application/ProcessDefinitionCanvasEditorProjectionService.Commands.cs
- repo://src/CanDoItAll.Processes.Application/ProcessDefinitionCanvasEditorProjectionService.Projection.cs
- repo://src/CanDoItAll.Processes.Application/ProcessDefinitionCanvasEditorProjectionService.Selection.cs
- repo://src/CanDoItAll.Processes.Application/ProcessDefinitionCanvasEditorProjectionService.State.cs
- repo://src/CanDoItAll.Processes.Application/ProcessWorkspaceShellProjectionService.cs
- repo://src/CanDoItAll.Processes.Templates/ProcessTemplateCanvasSummaries.cs
- repo://src/CanDoItAll.Processes.Templates/ProcessTemplatePackLoader.cs
- repo://src/CanDoItAll.Modules.Processes/Components/ProcessDefinitionCanvasPanel.razor
- repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceShell.razor
- repo://src/CanDoItAll.Modules.Processes/Services/ProcessWorkspaceProjectionClient.cs
- repo://src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs

## Test And Validation Artifacts

- bundle://proof/SB17/build-process-module.txt
- bundle://proof/SB17/build-solution-sb17.txt
- bundle://proof/SB17/test-unit-canvas-sb17.txt
- bundle://proof/SB17/test-components-process-shell-sb17.txt
- bundle://proof/SB17/test-playwright-process-shell-sb17.txt
- bundle://proof/SB17/tailwind-build-sb17.txt
- bundle://proof/SB17/source-assertions.txt
- bundle://proof/SB17/semantic-invariants.md
- bundle://proof/SB17/red-team-semantic-proof.md
- bundle://proof/SB17/story-coverage.md
- bundle://proof/SB17/browser-validation.md
- bundle://proof/SB17/codeanalytics-snapshot-summary.txt
- bundle://proof/SB17/bundle-validator-prepared-sb17.txt
- bundle://proof/SB17/git-diff-check-sb17.txt
- bundle://proof/SB17/performance-scan-summary.json
- bundle://proof/SB17/scans/projection-boundary-scan.txt
- bundle://proof/SB17/scans/old-symbol-scan.txt
- bundle://proof/SB17/scans/anti-stub-scan.txt
- bundle://proof/SB17/scans/performance-antipattern-scan.txt
- bundle://proof/SB17/changed-file-hashes.txt
- bundle://proof/SB17/line-counts.txt

## Browser Artifacts

- bundle://proof/SB17/browser/processes-project-shell.png
- bundle://proof/SB17/browser/processes-global-definition-catalog.png
- bundle://proof/SB17/browser/processes-definition-role-editor.png
- bundle://proof/SB17/browser/processes-definition-canvas.png

## Production Behavior Artifact Matrix

| Signal | Producer | Consumer | Lifecycle | Negative or guard proof |
| --- | --- | --- | --- | --- |
| Definition canvas projection | `ProcessDefinitionCanvasEditorProjectionService.GetCanvasAsync` | `ProcessWorkspaceShellProjectionService` and `ProcessDefinitionCanvasPanel` | Template canvas defaults are converted to typed node, edge, selection, toolbox, command, and viewport projections. | `projection-boundary-scan.txt` has no runtime/persistence/old observation references. |
| Canvas command receipt | `ProcessDefinitionCanvasEditorProjectionService.ExecuteCommandAsync` | `ProcessWorkspaceShell.razor` applies the returned canvas projection and receipt. | Toolbox/recompose commands return accepted/rejected typed receipts and updated projections. | `Canvas_rejects_stale_version_tokens` rejects changed version tokens. |
| Selection state | `ProcessDefinitionCanvasPanel.razor` local state | Selection panel and command payload builder | Selection is explicit UI state, then passed as typed node/edge keys to application commands. | Component tests assert selected artifact text and typed selected node key. |
| Recomposition layout | `RecomposeNodes` in the canvas projection service | Canvas panel absolute node layout and SVG route rendering | Layout is deterministic from projection nodes and edge anchors; it does not parse DOM state. | Unit/component/Playwright tests assert command receipt and stable screenshot. |
| Toolbox actions | `ProcessTemplateCanvasSummaries` and source-generated toolbox JSON read | Canvas panel toolbox buttons | Step templates plus fixed role/artifact/subprocess actions become typed toolbox projections. | Component tests assert `process-step.implementation` command boundary and Playwright adds a step. |

## Result

SB17 closure passes. Builds and focused tests passed, browser proof exists, CodeAnalytics MCP ran on the final layout with no blocking errors, projection boundary scans are clean, and the acceptance checklist is satisfied. Remaining CodeAnalytics complexity warnings are tracked as refactoring watch items for SB18/SB28 hardening, not hidden as correctness proof.
