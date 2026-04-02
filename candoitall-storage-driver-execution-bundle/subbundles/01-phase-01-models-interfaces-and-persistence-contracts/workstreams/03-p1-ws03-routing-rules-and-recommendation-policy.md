# P1-WS03 Routing rules and recommendation policy

## Objective

Define how the system chooses default storage by file subtype, MIME, size, usage purpose, edit intent, and project/node scope.

## Touchpoints From Workbook

| Touchpoint | Surface | Module | Scope | Required change | Proof route |
| --- | --- | --- | --- | --- | --- |
| TP-007 | Project workbench file subtype policy | Workbench | In scope | Use subtype inference as routing recommendation input and formalize storage-reference metadata. | Unit tests |

## Exact Source References

- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructureCanvasCatalog.cs
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/requirements/03-default-routing-policy.md

## Ordered Implementation Tasks

1. Use ProjectFileSubtype inference as a first-class routing signal.
2. Add explicit usage-purpose taxonomy for prompt attachments, exports, evidence, recordings, deployment mirrors, and snapshot packages.
3. Model per-workspace defaults plus project/node-specific overrides.
4. Represent recommendations as suggestions with explainable reasons and alternatives rather than hard-coded forced writes.

## Acceptance Checklist

- The recommendation matrix distinguishes editable content from immutable/shareable content and publish/deploy targets.
- Project/node overrides can win over workspace defaults without code branching in upload pages.
- The policy is testable without UI.

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
Implement workstream P1-WS03 only.

Objective:
Define how the system chooses default storage by file subtype, MIME, size, usage purpose, edit intent, and project/node scope.

Mandatory files to read first:
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/README.md
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/subbundles/01-phase-01-models-interfaces-and-persistence-contracts/README.md
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructureCanvasCatalog.cs
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/requirements/03-default-routing-policy.md

Mandatory execution behavior:
- Keep comments in English.
- Update reviews/01-execution-report.md with the exact commands, screenshots, and findings for this workstream.
- Do not mark the workstream complete if required proof is blocked.
- If this workstream touches UI, run Playwright automation plus manual headed Playwright MCP with screenshots at 1900x1200 and 1366x900.
- If a screenshot shows overlap, clipping, overflow, or broken action gating, fix it before closure.
```

