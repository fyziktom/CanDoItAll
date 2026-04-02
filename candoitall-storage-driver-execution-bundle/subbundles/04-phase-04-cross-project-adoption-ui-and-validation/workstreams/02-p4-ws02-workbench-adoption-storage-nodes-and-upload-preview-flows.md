# P4-WS02 Workbench adoption, storage nodes, and upload/preview flows

## Objective

Adopt the storage platform throughout project structure creation, import, preview, selection, and storage-node linking flows.

## Touchpoints From Workbook

| Touchpoint | Surface | Module | Scope | Required change | Proof route |
| --- | --- | --- | --- | --- | --- |
| TP-006 | Project node media save | Workbench | In scope | Adopt storage placement service, capture storage object reference, and stop assuming local relative route only. | Unit + Playwright + manual MCP |
| TP-007 | Project workbench file subtype policy | Workbench | In scope | Use subtype inference as routing recommendation input and formalize storage-reference metadata. | Unit tests |
| TP-008 | Project structure create request composer | Workbench | In scope | Add storage selection override inputs, bind recommendation result, and support storage-node creation/linking. | Playwright + manual MCP |
| TP-009 | Project structure import service | Workbench | In scope | Route imported assets through new storage placement service and preserve metadata for preview/download. | Integration + Playwright |
| TP-010 | Project workbench export/capture workflows | Workbench | In scope | Use routing rules for exports/evidence/image capture and store resulting storage references. | Playwright + manual MCP |
| TP-011 | Selection panel previews | Workbench | In scope | Drive actions from capabilities and unified access descriptors instead of relative local paths only. | Playwright + manual MCP |
| TP-012 | Inline document preview | Workbench | In scope | Update preview URL resolution and capability-driven visibility. | Playwright + screenshot review |
| TP-013 | Preview dialog overlay | Workbench | In scope | Validate overlay with remote-provider previews and ensure no clipping/overflow. | Playwright MCP screenshots |
| TP-014 | Local file opener | Workbench | In scope | Capability-gate to local filesystem providers only; do not fabricate temp-download-and-open in initial pass. | Unit tests + manual host proof where possible |
| TP-025 | Project object types | Shared Model | In scope | Add storage-system subtype strategy (prefer Infrastructure subtype or justified new type) and document why. | Design review + Playwright |
| TP-026 | Infrastructure catalog definitions | Workbench | In scope | Add storage-system node definition and wizard/quick-create path. | Playwright + manual MCP |

## Exact Source References

- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructureCreateRequestComposer.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/Pages/ProjectStructureSelectionPanel.razor
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructureCanvasCatalog.RichDefinitions.cs

## Ordered Implementation Tasks

1. Route file/image/video uploads and generated assets through the placement service.
2. Add recommendation/override UI into create/edit flows.
3. Add a storage-system project node type/subtype strategy and link it to storage records.
4. Show storage facts and capability-driven actions in selection/detail surfaces.

## Acceptance Checklist

- Workbench uploads no longer hard-code managed-files as the destination.
- Users can create or attach storage nodes and reuse them in upload defaults.
- Preview/open/download actions match provider capabilities and remain visually correct.

## Proof Required

- Update `reviews/01-execution-report.md` with this workstream's command output or browser evidence.
- Add or update automated tests if the task changes executable behavior.
- If the task affects a UI surface, attach both desktop and narrow screenshot paths plus written findings.
- If anything is blocked, record the blocker explicitly instead of downgrading the requirement silently.

## Reopen Triggers

- A workbook touchpoint owned by this workstream has no implementation note, proof route, or linked evidence.
- Any required test command fails or is skipped.
- Any screenshot reveals clipping, overlap, overflow, inaccessible wizard navigation, or incorrect enabled/disabled actions.
- A provider is marked supported without a real protocol-backed validation path.

## Suggested Codex Prompt

```text
Implement workstream P4-WS02 only.

Objective:
Adopt the storage platform throughout project structure creation, import, preview, selection, and storage-node linking flows.

Mandatory files to read first:
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/README.md
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/subbundles/04-phase-04-cross-project-adoption-ui-and-validation/README.md
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructureCreateRequestComposer.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/Pages/ProjectStructureSelectionPanel.razor
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructureCanvasCatalog.RichDefinitions.cs

Mandatory execution behavior:
- Keep comments in English.
- Update reviews/01-execution-report.md with the exact commands, screenshots, and findings for this workstream.
- Do not mark the workstream complete if required proof is blocked.
- If this workstream touches UI, run Playwright automation plus manual headed Playwright MCP with screenshots at 1900x1200 and 1366x900.
- If a screenshot shows overlap, clipping, overflow, or broken action gating, fix it before closure.
```

