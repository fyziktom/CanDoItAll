# P3-WS03 Playwright automation and manual Playwright MCP proof contract

## Objective

Make browser-visible changes unskippable through automated tests plus manual MCP screenshot review.

## Touchpoints From Workbook

| Touchpoint | Surface | Module | Scope | Required change | Proof route |
| --- | --- | --- | --- | --- | --- |
| TP-027 | Artifact browser tests | Playwright | In scope | Extend with storage settings, recommendation UI, storage-node flows, and remote-preview/access cases. | Playwright tests |
| TP-028 | App fixture | Playwright | In scope | Reuse fixture, add storage seeding helpers, and save screenshot artifacts into dedicated storage-driver folder. | Playwright tests |

## Exact Source References

- C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Playwright/PlaywrightAppFixture.cs
- C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Playwright/ProjectStructureArtifactBrowserTests.cs
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/shared-prompts/qa-prompt.md

## Ordered Implementation Tasks

1. Add or extend Playwright test classes for settings UI, workbench upload/recommendation flows, preview flows, and prompt factory flows.
2. Save screenshots to a dedicated storage-driver evidence folder.
3. Require a headed Playwright MCP pass with desktop and narrower-width screenshots and written visual findings.
4. For every changed overlay/dialog, require open-state screenshots and review of clipping, overflow, and layering.

## Acceptance Checklist

- Automated Playwright coverage exists for the main changed browser flows.
- Manual MCP proof is explicitly logged with screenshot paths and findings.
- No UI subbundle can close without screenshot review answers.

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
Implement workstream P3-WS03 only.

Objective:
Make browser-visible changes unskippable through automated tests plus manual MCP screenshot review.

Mandatory files to read first:
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/README.md
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/subbundles/03-phase-03-test-coverage-and-proof-harness/README.md
- C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Playwright/PlaywrightAppFixture.cs
- C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Playwright/ProjectStructureArtifactBrowserTests.cs
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/shared-prompts/qa-prompt.md

Mandatory execution behavior:
- Keep comments in English.
- Update reviews/01-execution-report.md with the exact commands, screenshots, and findings for this workstream.
- Do not mark the workstream complete if required proof is blocked.
- If this workstream touches UI, run Playwright automation plus manual headed Playwright MCP with screenshots at 1900x1200 and 1366x900.
- If a screenshot shows overlap, clipping, overflow, or broken action gating, fix it before closure.
```

