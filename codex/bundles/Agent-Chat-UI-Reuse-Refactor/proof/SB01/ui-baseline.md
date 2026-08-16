# SB01 large-desktop UI baseline

- Viewport: 1920x1080.
- Route: `/agents?tab=agents` and `/agents?tab=chat` on `http://127.0.0.1:5032`.
- Fixture: local development profile `candoitall_development`; 28 agents and 6 providers were available.
- Console: 0 errors, 0 warnings.

Inspected states:

1. Agent catalog normal: primary card grid visible; team rail and card grid preserve their existing independent scroll regions; no clipping.
2. Main chat normal: selected `.NET Application Developer`; thread rail, empty transcript, and composer visible in the first viewport; no overlay obstruction.
3. Floating catalog open: draggable overlay appears above the page at the upper right; list scroll and visible actions are usable; no clipping or layering failure.
4. Floating settings normal: lifecycle and preparation settings render in two columns with save action visible.
5. Agent settings open: wide dialog shows Identity fields; body scroll and fixed actions remain within the viewport.

The Playwright browser's workspace-help popover initially intercepted clicks; closing its own scrim restored interaction. This is development-shell state, not an Agent Chat failure.

Artifacts are under `proof/SB01/browser/`. The early startup-dialog snapshot `sb01-agents-catalog.snapshot.md` is retained for audit but is not used as catalog parity evidence; the inspected catalog evidence is `sb01-agent-catalog-normal.png`.

