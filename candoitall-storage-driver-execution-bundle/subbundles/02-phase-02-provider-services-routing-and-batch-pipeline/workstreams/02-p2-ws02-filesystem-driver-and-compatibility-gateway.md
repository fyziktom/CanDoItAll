# P2-WS02 Filesystem driver and compatibility gateway

## Objective

Make the local filesystem driver the first real provider behind the new contracts while preserving current safety guarantees.

## Touchpoints From Workbook

| Touchpoint | Surface | Module | Scope | Required change | Proof route |
| --- | --- | --- | --- | --- | --- |
| TP-001 | Baseline storage abstraction | Infrastructure | In scope | Replace with layered storage contracts while retaining a compatibility adapter during migration. | Unit + integration tests + build |

## Exact Source References

- C:\repositories\CanDoItAll/src/CanDoItAll.Infrastructure/Storage/WorkspaceStorage.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructureLocalFileOpener.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Web/Infrastructure/ManagedFilesEndpointRoutes.cs

## Ordered Implementation Tasks

1. Refactor the existing local storage implementation into the new provider contract.
2. Preserve path traversal protections and active-workspace scoping.
3. Expose local-open capability only when the provider points to a trusted local path.
4. Back the compatibility adapter with the new filesystem provider instead of duplicating logic.

## Acceptance Checklist

- Existing local behavior still works through the new driver.
- Traversal protection remains intact.
- Local-open is still safe and explicit.

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
Implement workstream P2-WS02 only.

Objective:
Make the local filesystem driver the first real provider behind the new contracts while preserving current safety guarantees.

Mandatory files to read first:
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/README.md
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/subbundles/02-phase-02-provider-services-routing-and-batch-pipeline/README.md
- C:\repositories\CanDoItAll/src/CanDoItAll.Infrastructure/Storage/WorkspaceStorage.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructureLocalFileOpener.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Web/Infrastructure/ManagedFilesEndpointRoutes.cs

Mandatory execution behavior:
- Keep comments in English.
- Update reviews/01-execution-report.md with the exact commands, screenshots, and findings for this workstream.
- Do not mark the workstream complete if required proof is blocked.
- If this workstream touches UI, run Playwright automation plus manual headed Playwright MCP with screenshots at 1900x1200 and 1366x900.
- If a screenshot shows overlap, clipping, overflow, or broken action gating, fix it before closure.
```

