# P4-WS01 Settings UI, wizard, and reusable storage components

## Objective

Ship the browser-visible management surface for storage catalog entries and the shared components that power it.

## Touchpoints From Workbook

| Touchpoint | Surface | Module | Scope | Required change | Proof route |
| --- | --- | --- | --- | --- | --- |
| TP-020 | Settings shell | Workspace UI | In scope | Add storage tab and route-state handling. | Playwright + manual MCP |
| TP-021 | Database source settings patterns | Workspace UI | In scope | Use as reference for storage list/detail/test flow; do not duplicate pattern inconsistently. | Visual review |
| TP-022 | FTP resource metadata | Resources UI/Domain | Adjacent/in scope | Reuse editor-field ideas only; keep storage catalog separate from project resources or add an explicit bridge. | Design review |
| TP-023 | Resources page FTP editor | Resources UI | Adjacent | Reference visual/input patterns only; do not silently merge modules without explicit design. | Visual consistency review |

## Exact Source References

- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workspace/Pages/Components/DatabaseSourcesSettingsPanel.razor
- C:\repositories\CanDoItAll/src/CanDoItAll.Components.BaseLib

## Ordered Implementation Tasks

1. Add the Storage settings tab and route-state handling.
2. Implement storage list/detail/picker/health/recommendation/wizard components, separating reusable presentation from module orchestration.
3. Support create/edit/disable/delete/test/default assignment flows.
4. Keep layout consistency with existing settings patterns.

## Acceptance Checklist

- The same shared storage components are reused by more than one page/surface.
- Wizard steps guide type -> connection -> auth/secrets -> test -> defaults -> review/save.
- UI screenshots show no overflow, clipped text, or overlapping actions.

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
Implement workstream P4-WS01 only.

Objective:
Ship the browser-visible management surface for storage catalog entries and the shared components that power it.

Mandatory files to read first:
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/README.md
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/subbundles/04-phase-04-cross-project-adoption-ui-and-validation/README.md
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workspace/Pages/Components/DatabaseSourcesSettingsPanel.razor
- C:\repositories\CanDoItAll/src/CanDoItAll.Components.BaseLib

Mandatory execution behavior:
- Keep comments in English.
- Update reviews/01-execution-report.md with the exact commands, screenshots, and findings for this workstream.
- Do not mark the workstream complete if required proof is blocked.
- If this workstream touches UI, run Playwright automation plus manual headed Playwright MCP with screenshots at 1900x1200 and 1366x900.
- If a screenshot shows overlap, clipping, overflow, or broken action gating, fix it before closure.
```

