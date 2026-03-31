# QA Prompt

Validate the active subbundle for `material-icons-migration-bundle-v1`.

Checklist:

- Confirm the active workbook rows moved from `Not started` to the correct state and that any unresolved rows remain visible.
- Confirm the active files no longer rely on remote icon assets or Font Awesome rendering where the subbundle says they should be migrated.
- Confirm icon sizing, alignment, affordance clarity, and icon-only accessibility are preserved.
- Confirm any CSS hooks now point at the new shared Material icon class contract rather than old Font Awesome classes.

Browser review questions:

- Are all icons present instead of rendering as empty squares, fallback text, or clipped glyphs?
- Are toolbar, tab, treeview, button, and chip icons centered and aligned with adjacent text?
- Do narrow-width views still avoid overlap, clipping, and awkward spacing around icons?
- On Workbench and Prompt Factory surfaces, do interactive affordances still read clearly after the icon swap?

Closure rule:

- Record commands, screenshots, and browser analytics in `reviews/01-execution-report.md`.
- If any route still shows a remote icon request, Font Awesome class output, or a mismatched raw glyph, fail the subbundle and reopen the earlier foundation phase as needed.
