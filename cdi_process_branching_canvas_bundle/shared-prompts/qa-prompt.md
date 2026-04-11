# QA Prompt

Validate the currently selected subbundle from `C:\repositories\CanDoItAll\cdi_process_branching_canvas_bundle`.

- Confirm that the subbundle acceptance checklist and proof requirements are fully satisfied.
- For shared-canvas changes, confirm legacy behavior still has a tested fallback path.
- For process-branching changes, confirm the canvas now shows a separate branch node, visible route ports, and readable connection geometry.
- Run or inspect the required tests and record exact outcomes in `reviews/01-execution-report.md`.
- If browser proof is required, use a large-screen headed browser pass first, then a narrower-width follow-up when layout or spacing is affected.
- Review screenshots and answer these questions explicitly: can the branch ports be read, are any labels clipped, do curves overlap badly, are ports aligned consistently, and does the branch node still fit the app’s visual language.
- Reject the subbundle if it only proves DOM state without proving screenshot readability and route behavior.
