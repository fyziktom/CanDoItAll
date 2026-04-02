
# Shared Implementation Prompt

Use this prompt as the base system/task prompt for Codex when executing any storage-driver phase.

```text
You are implementing the CanDoItAll storage-driver bundle. Work only inside the current repository.

Mandatory preparation before changing code:
1. Read `C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/README.md`.
2. Read `C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/inputs/00-original-request.md` and `C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/inputs/02-structured-input.md`.
3. Read `C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/plan/01-phase-plan.md`, `C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/plan/02-codex-main-checklist.md`, and `C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/plan/03-command-sequence.md`.
4. Read `C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/inventories/04-storage-driver-touchpoints.xlsx` and `C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/traceability/03-touchpoint-coverage-from-xlsx.md`.
5. Read the target phase README and every nested workstream note before editing code.

Global execution rules:
- Keep code comments in English.
- Never skip a required command, screenshot, or checklist item.
- Do not fake provider support, connection tests, Playwright proof, or migration proof.
- Do not silently narrow scope; if blocked, mark the workstream/phase Blocked with concrete reasons and evidence.
- Update `C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/reviews/01-execution-report.md` continuously as work happens.
- Keep the workbook, execution report, and QA coverage audit aligned with the actual implementation.

Implementation discipline:
- Preserve `IFileStore` and `IManagedArtifactStore` as compatibility seams until the relevant touchpoints are migrated.
- Do not store provider secrets in plain text configuration or logs.
- Do not leave remote providers masquerading as local relative paths.
- Implement capability-driven action gating for preview, download, and local-open.
- Only filesystem-backed trusted roots may expose local-open initially.
- Any UI change requires both automated Playwright coverage and manual Playwright MCP screenshot review.
- For layout-sensitive surfaces, capture screenshots at 1900x1200 and 1366x900 and explicitly review overlap, overflow, clipping, hidden controls, and preview sizing.

Execution closure rule:
- A phase is complete only when its README acceptance checklist is satisfied, the required proof exists, and downstream progression gates are still valid.
- The whole bundle is complete only when every in-scope row in the workbook has an owner, code/result status, proof route, and matching evidence in the execution report.
```

## How to use it

- Prepend the relevant phase README and workstream file paths to the working context.
- Re-run the command sequence from `plan/03-command-sequence.md` whenever a touched workstream requires it.
- Re-open `reviews/02-qa-coverage-audit.md` before claiming final completion.

