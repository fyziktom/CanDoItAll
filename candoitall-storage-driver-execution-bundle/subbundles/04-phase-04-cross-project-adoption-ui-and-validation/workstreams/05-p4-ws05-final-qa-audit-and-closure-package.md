# P4-WS05 Final QA audit and closure package

## Objective

Use the XLSX and execution report as inputs for a final senior-QA-style audit before the bundle is declared complete.

## Touchpoints From Workbook

- No direct touchpoint row is owned exclusively here; this workstream is a governance/closure slice that still blocks phase completion.

## Exact Source References

- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/traceability/03-touchpoint-coverage-from-xlsx.md
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/reviews/01-execution-report.md
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/reviews/02-qa-coverage-audit.md

## Ordered Implementation Tasks

1. Cross-check every XLSX touchpoint row against an owning workstream, shipped proof, and main checklist reference.
2. Reopen any phase whose proof is weaker than the dependency gate requires.
3. Do not hide missing Playwright MCP proof inside residual-risk prose.

## Acceptance Checklist

- QA audit names every missing or blocked item explicitly.
- The bundle cannot be called complete if a touchpoint lacks an owner or proof route.
- Screenshots were reviewed, not merely captured.

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
Implement workstream P4-WS05 only.

Objective:
Use the XLSX and execution report as inputs for a final senior-QA-style audit before the bundle is declared complete.

Mandatory files to read first:
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/README.md
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/subbundles/04-phase-04-cross-project-adoption-ui-and-validation/README.md
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/traceability/03-touchpoint-coverage-from-xlsx.md
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/reviews/01-execution-report.md
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/reviews/02-qa-coverage-audit.md

Mandatory execution behavior:
- Keep comments in English.
- Update reviews/01-execution-report.md with the exact commands, screenshots, and findings for this workstream.
- Do not mark the workstream complete if required proof is blocked.
- If this workstream touches UI, run Playwright automation plus manual headed Playwright MCP with screenshots at 1900x1200 and 1366x900.
- If a screenshot shows overlap, clipping, overflow, or broken action gating, fix it before closure.
```

