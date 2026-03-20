# TODO Fixture — Multi-part + Lyrics

Create this fixture after implementing Parts + Lyrics (E1, E3).

Required contents:
- 2 parts:
  1) "Voice" (TrebleOnly) with 4 quarter notes and lyrics "Hel- lo"
  2) "Piano" (Grand) with simple chords
- PageSettings: A4 Portrait + ShowPageBorders=true
- Enough measures to force at least 2 systems

Playwright assertions:
- part-name render commands exist for both parts
- lyric render commands exist under part 1
- page-border commands exist
