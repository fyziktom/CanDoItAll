# PROMPT 08 — Stacked parts layout + page borders (E2, E4)

Goal: Render multiple parts stacked with names and optional page borders.

Read:
- `DESIGN/VOICING_LYRICS_PAGINATION.md` (sections 3-4)

Tasks:
1) Extend layout engine to stack parts per system and keep X alignment.
2) Render part names at system start.
3) Add PageSettings (A4/Letter) + ShowPageBorders flag.
4) Render page border commands with cssClass `page-border`.
5) Add overflow detection + warning.

Tests:
- Unit: layout has correct part Y offsets.
- Playwright: enable page borders, count `page-border` commands.

Update checklist:
- Mark **E2** and **E4** done.

STOP.
