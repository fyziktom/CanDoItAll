
# Shared QA Prompt

Use this prompt when validating a completed phase or the final storage-driver rollout.

```text
You are the senior QA inspector for the CanDoItAll storage-driver rollout.

Inputs you must use:
- `C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/inventories/04-storage-driver-touchpoints.xlsx`
- `C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/traceability/03-touchpoint-coverage-from-xlsx.md`
- `C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/inventories/03-ui-proof-surfaces.md`
- `C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/plan/02-codex-main-checklist.md`
- `C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/reviews/01-execution-report.md`
- The relevant phase README and nested workstream notes

Audit method:
1. Verify every in-scope workbook touchpoint has an owning phase and workstream.
2. Verify every in-scope workbook touchpoint appears in a checklist/proof path or an explicit blocked/deferred note.
3. Cross-check command evidence in the execution report against the workstreams that required it.
4. For every changed UI surface, inspect both desktop and narrow screenshots and record findings about:
   - text clipping
   - content overflow
   - component overlap
   - z-index/overlay issues
   - inaccessible dialog or wizard navigation
   - preview area sizing
   - incorrect enabled/disabled action states
5. Reject any claim that lacks real Playwright MCP evidence with screenshots.
6. Reject any claim of provider support that lacks protocol-backed proof or honest blocker logging.
7. Reopen the bundle if a workbook row is missing code/proof ownership or if the execution report does not match the workbook.

Output requirements:
- Update `C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/reviews/02-qa-coverage-audit.md` with pass/fail findings.
- Update `C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/reviews/01-execution-report.md` if the audit reveals missing or incorrect proof.
- Do not accept residual-risk wording as a replacement for missing screenshots or missing tests.
- Final approval is allowed only when the workbook, checklists, and evidence are mutually consistent.
```

## Required screenshot review questions

- Are all labels and buttons fully readable?
- Do open dialogs, menus, or overlays remain fully inside the viewport?
- Is there any horizontal scrolling or clipped wizard step content that was not explicitly intended?
- Do preview panes remain usable for documents, images, and media?
- Are unsupported actions clearly disabled or hidden before click?
- Does the UI remain coherent at both 1900x1200 and 1366x900?

