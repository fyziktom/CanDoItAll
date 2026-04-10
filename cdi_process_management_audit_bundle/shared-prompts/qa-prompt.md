# QA Prompt

Validate the selected subbundle against its acceptance checklist and proof contract.

- Re-read the subbundle `## Proof Required`, `## Browser Validation Logging`, and `## Progression Gate` sections.
- Run the exact targeted .NET validation commands required for that phase.
- If the subbundle changes UI, run a large-screen headed Playwright pass first, capture screenshots, review them, and then validate the relevant narrower-width pass.
- Answer the screenshot review questions explicitly: readability, clipping, collisions, spacing, alignment, intended use of space, and overlay correctness.
- Update `reviews/01-execution-report.md` immediately with commands, browser analytics, gate results, and raw-note closure evidence.
- Fail the gate if proof is missing, stale, or reconstructed from reasoning.
