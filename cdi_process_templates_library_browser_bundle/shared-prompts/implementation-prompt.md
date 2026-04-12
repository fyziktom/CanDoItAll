# Implementation Prompt

Implement the bundle phase by phase.

Rules for execution:

- Do not skip subbundle gates.
- Reuse the current Processes module seams unless a new helper is clearly justified.
- Keep UI work aligned with BaseLib components and Process workspace patterns.
- Do not invent a standalone persisted roles or artifacts library because the current domain model does not have one.
- Treat artifact import as a step-targeted authoring action.
- Capture proof after each subbundle before continuing.
