# P1-WS04 Legacy adapter and migration seam

## Objective

Define the compatibility seam that lets existing IFileStore/IManagedArtifactStore callers survive the multi-phase migration.

## Touchpoints From Workbook

| Touchpoint | Surface | Module | Scope | Required change | Proof route |
| --- | --- | --- | --- | --- | --- |
| TP-001 | Baseline storage abstraction | Infrastructure | In scope | Replace with layered storage contracts while retaining a compatibility adapter during migration. | Unit + integration tests + build |

## Exact Source References

- C:\repositories\CanDoItAll/src/CanDoItAll.Infrastructure/Storage/WorkspaceStorage.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Factory/PromptFactoryService.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs

## Ordered Implementation Tasks

1. Decide whether to keep IFileStore/IManagedArtifactStore as adapters over the new storage gateway during the rollout.
2. Document which old fields remain temporarily (for example MediaRelativePath) and which new fields become the source of truth.
3. Prevent a big-bang rewrite by preserving compatibility until all high-value touchpoints are adopted.

## Acceptance Checklist

- Phase 02 and 04 can migrate call sites gradually without losing buildability.
- Legacy relative-path fields are clearly marked as compatibility-only where applicable.
- The bundle makes the cutover rules explicit so Codex does not half-migrate call sites.

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
Implement workstream P1-WS04 only.

Objective:
Define the compatibility seam that lets existing IFileStore/IManagedArtifactStore callers survive the multi-phase migration.

Mandatory files to read first:
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/README.md
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/subbundles/01-phase-01-models-interfaces-and-persistence-contracts/README.md
- C:\repositories\CanDoItAll/src/CanDoItAll.Infrastructure/Storage/WorkspaceStorage.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Factory/PromptFactoryService.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs

Mandatory execution behavior:
- Keep comments in English.
- Update reviews/01-execution-report.md with the exact commands, screenshots, and findings for this workstream.
- Do not mark the workstream complete if required proof is blocked.
- If this workstream touches UI, run Playwright automation plus manual headed Playwright MCP with screenshots at 1900x1200 and 1366x900.
- If a screenshot shows overlap, clipping, overflow, or broken action gating, fix it before closure.
```

