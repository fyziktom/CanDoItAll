# PROMPT 09 — Lyrics model + entry UX + rendering (E3)

Goal: Add note-aligned lyrics entry mode + rendering.

Read:
- `DESIGN/VOICING_LYRICS_PAGINATION.md` (section 4-5)

Tasks:
1) Add Lyrics model to ScoreDocument.
2) Render lyrics under staff with cssClass `lyric`.
3) Add Lyrics tool and entry workflow:
   - click note to set cursor
   - typing creates syllables
   - Space advances; hyphen creates syllabic begin/middle; underscore toggles extender
4) Playwright: enter “Hel- lo” and assert lyric commands exist.

Update checklist:
- Mark **E3** done.

STOP.
