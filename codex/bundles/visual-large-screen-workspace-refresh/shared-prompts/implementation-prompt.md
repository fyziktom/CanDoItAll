# Implementation Prompt

```text
Implement the selected subbundle from C:\repositories\CanDoItAll\codex\bundles\visual-large-screen-workspace-refresh.

Rules:
- Work only on the selected subbundle scope.
- Treat large desktop screens as the only tuning target for this bundle.
- Do not add new page-local custom CSS or new .razor.css files for this refresh.
- Use BaseLib/CanDoItAll components, typed component parameters, enums, shared Tailwind sources, and Class parameters first.
- Do not introduce Radzen; this app does not currently use it.
- Keep UI behavior strongly typed and explicit.
- Do not silently hide database or shell errors.
- Preserve existing user changes and unrelated repo changes.

Before editing:
- Read README.md, inputs/02-structured-input.md, requirements/01-normalized-requirements.md, plan/01-phase-plan.md, traceability/01-requirement-traceability.md, and this subbundle README.
- Read the relevant inputs/page-inputs file(s) and accepted proposal image review before changing any page, tab, or dialog.
- If the subbundle depends on SB00-02 or SB00-03, verify the reusable component pattern exists before adding page-specific layout.
- Verify prerequisites and the previous subbundle gate result in reviews/01-execution-report.md.
- If the subbundle touches UI, capture or reuse the required large-screen baseline screenshot.

After editing:
- Run the validation commands required by the subbundle.
- Capture Playwright large-screen screenshots and open-state overlay proof.
- For tab/dialog subbundles, capture each changed tab body and each open dialog state separately.
- Update reviews/01-execution-report.md with gate rows, browser analytics, screenshot paths, and notes.
- Stop and mark blocked if the progression gate cannot honestly pass.
```
