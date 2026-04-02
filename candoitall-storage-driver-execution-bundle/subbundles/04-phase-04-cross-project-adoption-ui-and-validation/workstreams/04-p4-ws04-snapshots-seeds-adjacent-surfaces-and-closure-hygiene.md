# P4-WS04 Snapshots, seeds, adjacent surfaces, and closure hygiene

## Objective

Finish adoption for snapshots/seeds and close the inventory honestly, including adjacent surfaces that remain intentionally unchanged.

## Touchpoints From Workbook

| Touchpoint | Surface | Module | Scope | Required change | Proof route |
| --- | --- | --- | --- | --- | --- |
| TP-005 | Program bootstrap/dev seed endpoint | Web | In scope | Preserve seed endpoint behavior through compatibility store and update route wiring. | Integration smoke |
| TP-015 | Runtime launcher path trust | Workbench | Adjacent/in scope for safety | Review and keep workspace-path safety intact; do not overextend to remote storage. | Unit tests + safety review |
| TP-019 | Database snapshots | Infrastructure | In scope | Refactor onto storage providers and transfer pipeline, preserving snapshot manifest behavior. | Integration tests |
| TP-035 | Tuning attachments in MainLayout | Web UI | Adjacent / document only | Inventory as intentionally out of initial storage-driver adoption unless product decides to unify transient uploads later. | Coverage audit |

## Exact Source References

- C:\repositories\CanDoItAll/src/CanDoItAll.Infrastructure/ControlPlane/DatabaseSnapshots.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Web/Program.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Web/Components/Layout/MainLayout.razor
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/inventories/04-storage-driver-touchpoints.xlsx

## Ordered Implementation Tasks

1. Refactor snapshot IPFS/local transport onto the new providers or explicitly document any retained legacy seam.
2. Confirm seed/dev flows still work through the compatibility layer.
3. Close or explicitly defer adjacent upload/file surfaces such as transient tuning attachments.
4. Update the XLSX-derived coverage audit and traceability after implementation decisions are final.

## Acceptance Checklist

- Every inventory row is either implemented, intentionally deferred, or blocked with proof.
- No hidden file surface remains unowned at closure.
- Execution report and raw-note closure table align with the inventory.

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
Implement workstream P4-WS04 only.

Objective:
Finish adoption for snapshots/seeds and close the inventory honestly, including adjacent surfaces that remain intentionally unchanged.

Mandatory files to read first:
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/README.md
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/subbundles/04-phase-04-cross-project-adoption-ui-and-validation/README.md
- C:\repositories\CanDoItAll/src/CanDoItAll.Infrastructure/ControlPlane/DatabaseSnapshots.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Web/Program.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Web/Components/Layout/MainLayout.razor
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/inventories/04-storage-driver-touchpoints.xlsx

Mandatory execution behavior:
- Keep comments in English.
- Update reviews/01-execution-report.md with the exact commands, screenshots, and findings for this workstream.
- Do not mark the workstream complete if required proof is blocked.
- If this workstream touches UI, run Playwright automation plus manual headed Playwright MCP with screenshots at 1900x1200 and 1366x900.
- If a screenshot shows overlap, clipping, overflow, or broken action gating, fix it before closure.
```

