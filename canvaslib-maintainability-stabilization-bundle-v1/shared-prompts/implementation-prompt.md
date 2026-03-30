# Implementation Prompt

Implement the current subbundle only.

Rules:

- Preserve CanvasLib behavior and public asset URLs.
- Prefer moving and splitting existing files over inventing new abstraction layers.
- Keep namespaces stable.
- Treat duplicate cleanup as a real retirement task, not as a cosmetic hide-the-folder workaround.
- Update the execution report after each subbundle with commands, browser analytics, and gate results.
- If `CanDoItAll.ComponentKit` proves to have an active consumer, stop the retirement step and record the evidence as a scope exception instead of guessing.
