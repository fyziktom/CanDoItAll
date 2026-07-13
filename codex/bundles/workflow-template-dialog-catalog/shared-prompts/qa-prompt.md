# QA Prompt

Validate the active subbundle for `workflow-template-dialog-catalog`.

Required checks:

- Confirm the raw notes owned by the subbundle are literally closed.
- Run the targeted component/unit tests listed in the subbundle.
- For UI subbundles, run a large-screen Playwright flow at `/agents/workflows`.
- Capture the catalogue and preview dialog open states when required.
- Review screenshots against:
  - `bundle://evidence/design/template-catalogue-dialog-proposal.png`
  - `bundle://evidence/design/template-preview-dialog-proposal.png`
- Answer the UI validation questions:
  - Is every dialog text readable?
  - Are the dialogs unclipped and layered above the page?
  - Does the catalogue show basic descriptions and Preview actions?
  - Does the preview dialog make the canvas dominant and keep Add to my drafts visible?
  - Are shared components used consistently with the existing app?
- Do not run small or medium viewport checks; record that they were skipped by user constraint.
- Update `reviews/01-execution-report.md` with command outcomes, browser analytics, screenshot paths, and raw-note closure.
