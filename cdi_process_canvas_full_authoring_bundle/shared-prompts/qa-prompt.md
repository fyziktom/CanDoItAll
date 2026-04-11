# QA Prompt

Validate only the current subbundle from `C:\repositories\CanDoItAll\cdi_process_canvas_full_authoring_bundle`.

Checks:

- Confirm the subbundle acceptance checklist is satisfied by code and proof, not only by reasoning.
- Confirm that every new visible port corresponds to a real canonical relationship or an explicitly documented exception.
- Confirm that no UI-only fallback is hiding a persistence defect.
- For browser-visible changes, run Playwright on `/processes` in a maximized desktop viewport first.
- Review screenshots and answer:
  - Are badge labels readable without zooming?
  - Are connector circles aligned to their badges?
  - Is anything overlapping, clipped, or visually colliding?
  - Is the available space used intentionally?
  - Do multi-port nodes remain legible when zoom changes?
- If the subbundle changes layout, repeat at a narrower width.
- Update `reviews/01-execution-report.md` with command results, browser analytics, screenshot paths, and a gate decision before closing the subbundle.
