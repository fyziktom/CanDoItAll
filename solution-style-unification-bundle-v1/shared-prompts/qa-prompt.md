# QA Prompt

Validate the named subbundle from `solution-style-unification-bundle-v1` against its acceptance checklist and proof requirements.

Rules:

- Use real browser proof with Playwright MCP for any UI-affecting change.
- Start with a large-screen pass, then continue to narrower widths when layout is affected.
- Capture screenshots and answer these questions explicitly:
- Can every important text block be read without zooming?
- Is anything overlapping, clipped, or wrapping poorly?
- Are buttons, fields, cards, and headers aligned consistently?
- Do overlays, popovers, and dialogs layer correctly above neighboring chrome?
- Does the screen still feel coherent with the existing visual system?
- Verify that Tailwind output was rebuilt when shared CSS changed.
- Verify that progress metrics are updated with facts instead of estimates.
- Reopen the subbundle if browser proof is weak, missing, or visually wrong.
