# Codex Implementation Status (Template)

> This file must be copied to `codex/STATUS.md` in the target repo.
> Update it after every milestone. Keep evidence (test names, fixture names, file paths).

## Conventions

- **Done**: implemented + tested + documented.
- **Partial**: implemented but missing tests, missing UI, or known limitations.
- **No**: not started.

Use this format per line:
- [ ] Requirement (Status: No) – Evidence: – Files:
- [x] Requirement (Status: Done) – Evidence: `TestName`, `FixtureName` – Files: `path/...`

---

## Milestone A – Key / Time Signatures + Accidentals

- [ ] Document model: `KeySignature` and `KeySignatureChanges` (Status: No) – Evidence: – Files:
- [ ] Document model: `TimeSignatureChanges` (Status: No) – Evidence: – Files:
- [ ] Context resolution per measure (`ScoreContext`) (Status: No) – Evidence: – Files:
- [ ] Measure capacity + reflow respects time signature changes (Status: No) – Evidence: – Files:
- [ ] Accidental engine (barline reset, key signature default) (Status: No) – Evidence: – Files:
- [ ] Rendering: key signature glyphs at system starts and measure changes (Status: No) – Evidence: – Files:
- [ ] Rendering: accidentals for notes that deviate from current state (Status: No) – Evidence: – Files:
- [ ] Editor: accidental override tools + keyboard shortcuts (#, b, n) (Status: No) – Evidence: – Files:
- [ ] Tests: JSON fixtures + Playwright E2E (Status: No) – Evidence: – Files:

## Milestone B – Ties + Filled Slurs

- [ ] Tie model behavior: `TieStart`/`TieStop` resolved into drawable segments (Status: No) – Evidence: – Files:
- [ ] Tie rendering as **filled** shape (Status: No) – Evidence: – Files:
- [ ] Slur rendering upgraded to **filled ribbon** (Status: No) – Evidence: – Files:
- [ ] Slur anchor algorithm mapped to VexFlow Curve or improved rules (Status: No) – Evidence: – Files:
- [ ] Tests: fixtures + Playwright visual/command assertions (Status: No) – Evidence: – Files:

## Milestone C – Canvas-first HUD Toolbars

- [ ] In-canvas top toolbar (Status: No) – Evidence: – Files:
- [ ] In-canvas floating toolbar near selection/pointer (Status: No) – Evidence: – Files:
- [ ] Optional radial menu (Status: No) – Evidence: – Files:
- [ ] Hit-testing inside canvas (no HTML buttons required) (Status: No) – Evidence: – Files:
- [ ] Keyboard shortcuts mapped + discoverability overlay (Status: No) – Evidence: – Files:
- [ ] Tests: click HUD, verify state changes (Status: No) – Evidence: – Files:

## Milestone D – Key/Time Signature Editing UX

- [ ] Set key signature at measure boundary (Status: No) – Evidence: – Files:
- [ ] Set time signature at measure boundary (Status: No) – Evidence: – Files:
- [ ] Handle repeated changes + revert to previous signatures (Status: No) – Evidence: – Files:
- [ ] UI integration inside canvas HUD (Status: No) – Evidence: – Files:

## Milestone E – Transposition

- [ ] Transpose selection (by semitone, by diatonic step) (Status: No) – Evidence: – Files:
- [ ] Transpose range/measure/system (Status: No) – Evidence: – Files:
- [ ] Transpose respecting target key signature spelling (Status: No) – Evidence: – Files:
- [ ] Tests: transposition + rendering + playback sanity (Status: No) – Evidence: – Files:

## Extended Notation (Backlog)

> Keep expanding this list. Nothing should be removed; if out of scope, mark as Postponed with a reason.

- [ ] Tuplets (Status: No)
- [ ] Grace notes (Status: No)
- [ ] Multiple voices per staff (Status: No)
- [ ] Clef changes (Status: No)
- [ ] Repeats / endings / segno/coda (Status: No)
- [ ] Tempo marks + metronome marks (Status: No)
- [ ] Lyrics (Status: No)
- [ ] Chord diagrams / fretboard / TAB staff (Status: No)
- [ ] Ornaments (trill, mordent, turn) (Status: No)
- [ ] Articulations expansion (marcato, fermata, etc.) (Status: No)
- [ ] Text annotations (Status: No)
- [ ] Page layout controls (page size, system breaks, measure spacing) (Status: No)

