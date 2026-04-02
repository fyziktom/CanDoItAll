# Phase 04 Cross Project Adoption UI And Validation

## Status

- `Ready for implementation after Phase 03 gate`

## Objective

Adopt the storage platform across the in-scope modules and UI surfaces, then close the XLSX inventory through a QA-style audit.

## Covered Inputs

- N003
- N004
- N005
- N006
- N008
- N009
- N010
- N011
- N012
- N013
- N014
- RQ-009
- RQ-010
- RQ-011
- RQ-012
- RQ-016

## Prerequisites

- `subbundles/01-phase-01-models-interfaces-and-persistence-contracts` completed.
- `subbundles/02-phase-02-provider-services-routing-and-batch-pipeline` completed.
- `subbundles/03-phase-03-test-coverage-and-proof-harness` completed.

## Exact Source References

- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workspace/Pages/Components/DatabaseSourcesSettingsPanel.razor
- C:\repositories\CanDoItAll/src/CanDoItAll.Components.BaseLib
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructureCreateRequestComposer.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/Pages/ProjectStructureSelectionPanel.razor
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructureCanvasCatalog.RichDefinitions.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Factory/PromptFactoryService.Pack.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Factory/PromptFactoryService.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Factory/CanvasAdapters/PromptSessionAttachmentNode.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Infrastructure/ControlPlane/DatabaseSnapshots.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Web/Program.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Web/Components/Layout/MainLayout.razor
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/inventories/04-storage-driver-touchpoints.xlsx
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/traceability/03-touchpoint-coverage-from-xlsx.md
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/reviews/01-execution-report.md
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/reviews/02-qa-coverage-audit.md
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/inventories/04-storage-driver-touchpoints.xlsx
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/reviews/02-qa-coverage-audit.md
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/shared-prompts/implementation-prompt.md

## Deliverables

- Storage settings tab, wizard, and reusable shared components.
- Workbench adoption for uploads, previews, exports, and storage-system nodes.
- Prompt Factory adoption for attachments and exports.
- Snapshot/seed/adjacent-surface closure decisions recorded against the inventory.
- Senior-QA-style coverage audit showing that every XLSX row has an owner and proof route.
- Nested workstream files listed below:
- `P4-WS01` - Settings UI, wizard, and reusable storage components (`workstreams/01-p4-ws01-settings-ui-wizard-and-reusable-storage-components.md`)
- `P4-WS02` - Workbench adoption, storage nodes, and upload/preview flows (`workstreams/02-p4-ws02-workbench-adoption-storage-nodes-and-upload-preview-flows.md`)
- `P4-WS03` - Prompt Factory and artifact export adoption (`workstreams/03-p4-ws03-prompt-factory-and-artifact-export-adoption.md`)
- `P4-WS04` - Snapshots, seeds, adjacent surfaces, and closure hygiene (`workstreams/04-p4-ws04-snapshots-seeds-adjacent-surfaces-and-closure-hygiene.md`)
- `P4-WS05` - Final QA audit and closure package (`workstreams/05-p4-ws05-final-qa-audit-and-closure-package.md`)

## Dependency Impact

- This phase is the only phase allowed to claim the user-visible feature is complete.
- If inventory rows are missed here, the whole bundle fails the user requirement about mapping all file-use situations.

## Validation Depth

- `End-to-end regression and closure`

## Implementation Steps

1. Implement settings UI and shared components.
2. Adopt the storage platform in Workbench flows, including storage nodes and capability-driven previews/actions.
3. Adopt the storage platform in Prompt Factory flows.
4. Refactor snapshots/seeds/adjacent surfaces or record explicit defer/block decisions.
5. Run final browser and QA audit against the workbook and execution report.

## Scope Exceptions

- Out-of-immediate-scope internal repo-local file I/O stays documented as unchanged; it must not be quietly omitted from the audit.

## Do Not Do

- Do not close the phase while any in-scope workbook row lacks an owner or proof.
- Do not ship a storage settings UI without connection-test and defaults management.
- Do not leave visual issues like overflow/clipping as unreviewed assumptions.

## Acceptance Checklist

- All in-scope workbook touchpoints are implemented or explicitly blocked/deferred with evidence.
- Settings/storage UI and module adoption surfaces have both automated and manual browser proof.
- QA coverage audit passes against the workbook.

## Proof Required

- Targeted build + unit + integration + Playwright test commands.
- Manual Playwright MCP screenshots and findings for each changed UI surface.
- Updated execution report + QA coverage audit.

## Browser Validation Logging

- Routes: all rows from `inventories/03-ui-proof-surfaces.md`.
- Viewports: desktop `1900x1200` plus narrower `1366x900` or similar for layout-affected screens.
- Required questions: no overlay clipping, no text/image overflow, correct action gating, readable wizard steps, sane preview sizing.

## Progression Gate

- The phase closes only when the QA audit says every in-scope workbook row has subbundle ownership, checklist coverage, and proof.
- If a screenshot reveals overflow/clipping/overlap, reopen the relevant workstream instead of accepting the defect as a residual risk.

## Suggested Agent Prompt

```text
Implement Phase 04 only.

Adopt the storage platform across settings, workbench, factory, and snapshot-related surfaces.
Run real Playwright MCP validation with screenshots.
Close the workbook and QA audit honestly; do not skip adjacent surfaces silently.

Read this phase README, the nested workstream notes, the workbook inventories, and the execution checklist before changing code.
Update reviews/01-execution-report.md as you go.
Do not skip Playwright MCP proof when a browser-visible surface is touched.
```

