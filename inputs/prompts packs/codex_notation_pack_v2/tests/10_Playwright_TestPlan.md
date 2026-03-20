# 10 — Test plan (C# xUnit + Playwright) for the missing notation features

Goal: every new notation feature should be covered by:
- at least one **unit test** for core math/logic, and
- one **E2E test** for user-visible behavior (and optionally visuals).

---

## 1) Unit tests (MusicTheory.Tests)
Add tests in `tests/MusicTheory.Tests/`:

### 1.1 Key/Time signature context
- `KeySignatureContext_DefaultIsCMajor`
- `KeySignatureContext_ChangeAppliesFromMeasure`
- `TimeSignatureContext_ChangeAppliesFromMeasure`
- `Capacity_UsesPerMeasureTimeSignature`

### 1.2 Accidentals
- `AccidentalEngine_UsesKeySignatureDefaults`
- `AccidentalEngine_ShowsNaturalWhenLeavingSharpKey`
- `AccidentalEngine_ResetsAtBarline`

### 1.3 Transposition
- `Transpose_BySemitones_ChangesMidiNumber`
- `Transpose_TargetKey_PicksBestEnharmonicPreference`

### 1.4 Ties/Slurs geometry
- `TieLayout_AnchorsOnNoteheads`
- `SlurLayout_ProducesNonIntersectingCurve_WhenCollision`

---

## 2) Playwright E2E tests (App.Web.PlaywrightTests)

### 2.1 Strategy options
#### Option A — “DOM state mirror” (recommended)
Expose state in a stable DOM node:
- `<div data-testid="notation-debug-state" hidden>...</div>`
- or keep hidden toolbar buttons for tests

Pros:
- stable assertions
- no pixel matching required

#### Option B — screenshot regression
Use `page.ScreenshotAsync()` and compare with baseline images.

Pros:
- catches engraving regressions
Cons:
- needs a small tolerance; can be flaky if fonts differ.

Recommended:
- use A for logic + tool state,
- use B for key visual primitives (slur/tie/key signature).

### 2.2 New E2E tests to add

1) **Canvas HUD toggles tools**
- Click on overlay canvas at the toolbar region for “Note tool”.
- Insert a note; verify it appears (canvas data URL changed).
- Assert `window.__notationEditorSettings.tool === "Note"`.

2) **Key signature renders in first measure**
- Load a fixture score whose default key signature is G major.
- Screenshot only the left margin of measure 0.
- Compare to baseline.

3) **Key change mid-score**
- Fixture: C major for measures 0-1, D major from measure 2.
- Verify key signature glyphs appear in measure 2 only.

4) **Accidentals show and reset**
- Fixture: G major, insert F natural in measure 0 (shows natural),
  then F# later in the same measure (may omit), and F natural in next measure (shows natural again).
- Verify via screenshot or debug state.

5) **Time signature change reflows**
- Fixture: 4/4 then 3/4 at measure 4.
- Verify capacity: measure 4 only allows 3 quarter notes.
- Verify layout spacing changes.

6) **Tie rendering**
- Fixture: tied note across barline.
- Screenshot the tie region; compare.

7) **Slur rendering (filled)**
- Fixture: slur spanning a 3-note phrase.
- Screenshot; compare.

---

## 3) Fixtures
Place fixtures in `tests/fixtures/`:
- `score_key_sig_change.json`
- `score_time_sig_change.json`
- `score_ties_and_slurs.json`

Provide a deterministic loader route in the app:
- `GET /editor?fixture=score_key_sig_change`
so Playwright can load the exact score.

---

## 4) Image comparison helper (C#)
Use ImageSharp:
- load baseline PNG
- load screenshot PNG
- compute pixel diff with tolerance

Keep tolerance low (e.g., 0.5–1.0% differing pixels).
