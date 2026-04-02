# P2-WS05 Batch transfer and migration pipeline

## Objective

Prepare high-volume migration and folder upload support for snapshots, publishing, and bulk imports.

## Touchpoints From Workbook

| Touchpoint | Surface | Module | Scope | Required change | Proof route |
| --- | --- | --- | --- | --- | --- |
| TP-019 | Database snapshots | Infrastructure | In scope | Refactor onto storage providers and transfer pipeline, preserving snapshot manifest behavior. | Integration tests |

## Exact Source References

- C:\repositories\CanDoItAll/src/CanDoItAll.Infrastructure/ControlPlane/DatabaseSnapshots.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructureImportService.cs

## Ordered Implementation Tasks

1. Create a manifest-driven transfer pipeline with bounded concurrency, cancellation, progress, retry, and verification hooks.
2. Support folder enumeration to provider-specific write operations.
3. Allow the snapshot service and future publishing flows to reuse the same pipeline instead of hand-rolled loops.

## Acceptance Checklist

- Folder upload/migration can be expressed as a manifest and executed with progress.
- At least one existing bulk-copy path (snapshot or import) is refactored onto the shared pipeline.
- The pipeline can short-circuit on unsupported provider capabilities instead of failing deep in UI flows.

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
Implement workstream P2-WS05 only.

Objective:
Prepare high-volume migration and folder upload support for snapshots, publishing, and bulk imports.

Mandatory files to read first:
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/README.md
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/subbundles/02-phase-02-provider-services-routing-and-batch-pipeline/README.md
- C:\repositories\CanDoItAll/src/CanDoItAll.Infrastructure/ControlPlane/DatabaseSnapshots.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructureImportService.cs

Mandatory execution behavior:
- Keep comments in English.
- Update reviews/01-execution-report.md with the exact commands, screenshots, and findings for this workstream.
- Do not mark the workstream complete if required proof is blocked.
- If this workstream touches UI, run Playwright automation plus manual headed Playwright MCP with screenshots at 1900x1200 and 1366x900.
- If a screenshot shows overlap, clipping, overflow, or broken action gating, fix it before closure.
```

