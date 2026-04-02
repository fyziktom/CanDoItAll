# P4-WS03 Prompt Factory and artifact export adoption

## Objective

Adopt the new storage routing for prompt attachments and exports while keeping previews usable.

## Touchpoints From Workbook

| Touchpoint | Surface | Module | Scope | Required change | Proof route |
| --- | --- | --- | --- | --- | --- |
| TP-016 | Attachment preparation | Factory | In scope | Route attachments through placement service, capture storage object reference, and compute preview/download access route. | Unit + Playwright + manual MCP |
| TP-017 | Prompt export | Factory | In scope | Adopt routing for export purpose with sensible defaults and preserved downloadability. | Unit + integration |
| TP-018 | Attachment preview nodes | Factory | In scope | Update to unified access route and capability metadata. | Playwright + manual MCP |

## Exact Source References

- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Factory/PromptFactoryService.Pack.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Factory/PromptFactoryService.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Factory/CanvasAdapters/PromptSessionAttachmentNode.cs

## Ordered Implementation Tasks

1. Route prompt attachments through the new placement service and store object references/access descriptors.
2. Route prompt exports through storage defaults for export purpose.
3. Keep prompt canvas previews and download/open actions working through access descriptors.

## Acceptance Checklist

- Prompt Factory no longer depends on direct MediaRoute assumptions from local filesystem writes.
- Generated exports remain discoverable and downloadable.
- Playwright/browser proof covers attachment flows.

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
Implement workstream P4-WS03 only.

Objective:
Adopt the new storage routing for prompt attachments and exports while keeping previews usable.

Mandatory files to read first:
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/README.md
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/subbundles/04-phase-04-cross-project-adoption-ui-and-validation/README.md
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Factory/PromptFactoryService.Pack.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Factory/PromptFactoryService.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Factory/CanvasAdapters/PromptSessionAttachmentNode.cs

Mandatory execution behavior:
- Keep comments in English.
- Update reviews/01-execution-report.md with the exact commands, screenshots, and findings for this workstream.
- Do not mark the workstream complete if required proof is blocked.
- If this workstream touches UI, run Playwright automation plus manual headed Playwright MCP with screenshots at 1900x1200 and 1366x900.
- If a screenshot shows overlap, clipping, overflow, or broken action gating, fix it before closure.
```

