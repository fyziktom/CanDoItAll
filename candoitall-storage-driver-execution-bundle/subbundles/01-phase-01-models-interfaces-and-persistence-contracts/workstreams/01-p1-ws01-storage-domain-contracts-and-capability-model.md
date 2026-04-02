# P1-WS01 Storage domain contracts and capability model

## Objective

Define the shared provider abstractions, object references, capability flags, usage purposes, recommendation context, and compatibility-facing contracts.

## Touchpoints From Workbook

| Touchpoint | Surface | Module | Scope | Required change | Proof route |
| --- | --- | --- | --- | --- | --- |
| TP-001 | Baseline storage abstraction | Infrastructure | In scope | Replace with layered storage contracts while retaining a compatibility adapter during migration. | Unit + integration tests + build |

## Exact Source References

- C:\repositories\CanDoItAll/src/CanDoItAll.Infrastructure/Storage/WorkspaceStorage.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.SharedKernel/ProjectObjectContracts.cs

## Ordered Implementation Tasks

1. Create storage enums/records/interfaces for provider kind, capability flags, usage purpose, placement context, object reference, access descriptor, and connection-test result.
2. Keep comments and XML docs in English only.
3. Introduce compatibility-facing interfaces so existing modules can migrate incrementally instead of flipping all call sites at once.
4. Define capability gating for preview, download, delete, mutable update, local open, public/direct URL, and batch folder upload.

## Acceptance Checklist

- A future provider can be added without changing module call sites or expanding provider-specific switch statements outside the registry.
- The object reference model can represent local paths, IPFS CIDs, FTP remote paths, and future provider locators.
- Local-open is modeled as an optional capability, not assumed for every storage.

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
Implement workstream P1-WS01 only.

Objective:
Define the shared provider abstractions, object references, capability flags, usage purposes, recommendation context, and compatibility-facing contracts.

Mandatory files to read first:
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/README.md
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/subbundles/01-phase-01-models-interfaces-and-persistence-contracts/README.md
- C:\repositories\CanDoItAll/src/CanDoItAll.Infrastructure/Storage/WorkspaceStorage.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.SharedKernel/ProjectObjectContracts.cs

Mandatory execution behavior:
- Keep comments in English.
- Update reviews/01-execution-report.md with the exact commands, screenshots, and findings for this workstream.
- Do not mark the workstream complete if required proof is blocked.
- If this workstream touches UI, run Playwright automation plus manual headed Playwright MCP with screenshots at 1900x1200 and 1366x900.
- If a screenshot shows overlap, clipping, overflow, or broken action gating, fix it before closure.
```

