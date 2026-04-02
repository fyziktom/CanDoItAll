# P2-WS04 Unified storage access endpoint and capability-driven actions

## Objective

Replace hard dependency on /managed-files relative paths with unified access descriptors for preview, download, new-tab, and local-open actions.

## Touchpoints From Workbook

| Touchpoint | Surface | Module | Scope | Required change | Proof route |
| --- | --- | --- | --- | --- | --- |
| TP-004 | Managed files endpoint | Web | In scope | Add unified storage access endpoint/service and keep legacy route as compatibility or redirect path. | Integration tests + browser proof |
| TP-014 | Local file opener | Workbench | In scope | Capability-gate to local filesystem providers only; do not fabricate temp-download-and-open in initial pass. | Unit tests + manual host proof where possible |
| TP-015 | Runtime launcher path trust | Workbench | Adjacent/in scope for safety | Review and keep workspace-path safety intact; do not overextend to remote storage. | Unit tests + safety review |

## Exact Source References

- C:\repositories\CanDoItAll/src/CanDoItAll.Web/Infrastructure/ManagedFilesEndpointRoutes.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructureLocalFileOpener.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/Pages/ProjectStructureSelectionPanel.razor
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Factory/CanvasAdapters/PromptSessionAttachmentNode.cs

## Ordered Implementation Tasks

1. Add a storage-object access service that yields preview/download URLs and local-open availability.
2. Introduce a new app route or route family for storage object access and keep /managed-files compatibility only where still needed.
3. Drive UI action availability from access descriptor capabilities.
4. Do not auto-download remote files to temp and open them locally in the first pass without an explicit security design.

## Acceptance Checklist

- UI actions no longer assume MediaRelativePath or /managed-files is always valid.
- Remote providers can still preview/download through a consistent service.
- Unsupported actions are hidden or disabled with an explainable reason.

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
Implement workstream P2-WS04 only.

Objective:
Replace hard dependency on /managed-files relative paths with unified access descriptors for preview, download, new-tab, and local-open actions.

Mandatory files to read first:
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/README.md
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/subbundles/02-phase-02-provider-services-routing-and-batch-pipeline/README.md
- C:\repositories\CanDoItAll/src/CanDoItAll.Web/Infrastructure/ManagedFilesEndpointRoutes.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructureLocalFileOpener.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/Pages/ProjectStructureSelectionPanel.razor
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Factory/CanvasAdapters/PromptSessionAttachmentNode.cs

Mandatory execution behavior:
- Keep comments in English.
- Update reviews/01-execution-report.md with the exact commands, screenshots, and findings for this workstream.
- Do not mark the workstream complete if required proof is blocked.
- If this workstream touches UI, run Playwright automation plus manual headed Playwright MCP with screenshots at 1900x1200 and 1366x900.
- If a screenshot shows overlap, clipping, overflow, or broken action gating, fix it before closure.
```

