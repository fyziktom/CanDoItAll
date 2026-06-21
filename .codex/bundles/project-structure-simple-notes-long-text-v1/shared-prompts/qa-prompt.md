# QA Prompt

Validate the shipped behavior against the raw notes, not only against implementation intent.

Required checks:

- Long quick-note create persists the complete body in `Notes` and `InlineText`.
- Long inline-note edit persists the complete body and derives a bounded first-line title.
- Browser runtime state and persisted/service state agree after create/edit and after a reload or surface refresh.
- Rendered inline note cards use measured width rather than the old fixed narrow width when note text warrants it.
- Large desktop screenshot and narrower screenshot are reviewed for readability, wrapping, overlap, and use of available space.
- Package proof confirms stale CanvasLib assets are not being used.

Do not close the bundle from screenshots alone. Do not close it from tests that only assert a node exists, a title is non-empty, or a text fragment appears somewhere on the page.
