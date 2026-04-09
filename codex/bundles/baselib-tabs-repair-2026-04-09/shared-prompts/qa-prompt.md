# QA Prompt

Validate the current subbundle against the bundle contract, not only against the latest diff.

Checklist:

- Confirm the owned raw notes are actually covered by code and proof.
- Confirm no shared `zy-*` dependency remains once the foundation phase claims completion.
- For UI phases, open the real sandbox route in a headed browser, capture screenshots, and answer:
- Can all texts be read without zooming?
- Is anything overlapping, clipped, or visually colliding?
- Is the active state obvious?
- Is the optional border treatment intentional rather than noisy?
- On the narrow-width pass, is wrapping or overflow behavior clearly acceptable?
- Confirm the screenshots were stored under `output/playwright/baselib-tabs-repair-2026-04-09/`.
- Confirm the execution report contains the subbundle gate row and browser-validation analytics row before the phase is marked complete.
