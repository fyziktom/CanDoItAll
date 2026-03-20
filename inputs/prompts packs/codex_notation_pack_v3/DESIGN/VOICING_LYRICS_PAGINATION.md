# DESIGN — Voicing (Multi-Part), Lyrics, Page Layout / Print

This document defines the next major milestone after rhythm + spacing are fixed.

---

## 1) What “voicing” means here
We interpret “voicing” as **stacked parts/instruments** (like SATB, piano+voice, quartet):
- Each part appears as its own staff (treble or bass) or grand staff.
- Parts are stacked vertically within a system.
- Each part can have a **name** rendered at the start of each system.
- Each part can also contain multiple rhythmic voices (Voice 0..N) on the same staff.

This is different from “multiple voices in one staff” (which we also keep via `ScoreEvent.Voice`).

---

## 2) Data model changes

### 2.1 ScorePart
Add:
```csharp
public sealed class ScorePart
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Part";
    public string Abbrev { get; set; } = "";
    public ScoreStaffMode StaffMode { get; set; } = ScoreStaffMode.TrebleOnly; // or reuse existing enum
    public int Order { get; set; }
}
```

### 2.2 ScoreDocument
Add:
- `List<ScorePart> Parts`
- Backward compatibility:
  - if `Parts` is empty on load, create one default part
  - migrate all events to that part

### 2.3 ScoreEvent
Add:
- `Guid PartId` (or `int PartIndex`)

> Prefer `Guid PartId` to keep stable identity if parts are re-ordered.

### 2.4 Lyrics model
Implement note-aligned lyrics first (standard, familiar to users):
```csharp
public enum Syllabic { Single, Begin, Middle, End }

public sealed class LyricSyllable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PartId { get; set; }
    public Guid NoteId { get; set; } // anchor note
    public int Verse { get; set; } = 1;
    public string Text { get; set; } = "";
    public Syllabic Syllabic { get; set; } = Syllabic.Single;
    public bool Extender { get; set; } // underscore line to next lyric
}
```

Add to ScoreDocument:
- `List<LyricSyllable> Lyrics`

### 2.5 Optional: “measure cell lyrics”
To satisfy “cell under each measure”:
- add `Dictionary<Guid /*PartId*/, string> LyricCellText` to `ScoreMeasure`
- rendered centered under measure
This is optional and can be added after note-aligned lyrics.

---

## 3) Layout changes

### 3.1 Systems now contain parts
Current `ScoreLayout` contains `Systems -> Measures`.
Extend:
- `SystemLayout.Parts[]` each with:
  - `PartId`
  - vertical origin Y
  - per-measure staff Y positions (treble/bass)
  - part label position
- Measures remain aligned horizontally across parts.

### 3.2 Vertical stacking algorithm
For each system:
1) start at `systemTopY`
2) for each part in order:
   - compute part height:
     - TrebleOnly/BassOnly: 1 staff height + spacing for lyrics lane (optional)
     - Grand: treble staff + gap + bass staff + optional dynamics lane/lyrics
   - assign partTopY
   - add `interPartGap` (configurable)
3) systemHeight = sum(partHeights) + gaps

### 3.3 Page layout (A4 / Letter)
Add `PageSettings`:
- Paper: A4, Letter, (optional B4)
- Orientation: Portrait/Landscape
- Margins: L/R/T/B
- Show page borders: bool

Layout engine responsibilities:
- compute page rectangles in score coordinates
- assign systems to pages by cumulative height
- compute warnings if a single system cannot fit in a page

Render:
- if `ShowPageBorders` draw border rectangles for each page
- if overflow warning, draw a visible badge or add to diagnostics overlay

---

## 4) Rendering changes

### 4.1 Staff names
At the start of each system:
- draw part name (full) aligned to the left margin for the first system
- draw abbreviation for following systems (optional)

### 4.2 Lyrics
Render lyrics below the relevant staff:
- baseline Y = staffBottomY + lyricsPadding
- x anchored to the notehead X (slot X)

Hyphenation:
- If Syllabic is Begin/Middle, draw a hyphen between this note and the next syllable note in the same verse.

Extenders:
- If Extender is true, draw an underline from this note to the next lyric note.

---

## 5) Editing UX (what users expect)

### 5.1 Part selection
- A part picker in the ribbon / HUD:
  - “Active Part”
- Clicking within a staff automatically sets Active Part.

### 5.2 Lyric entry mode (standard workflow)
When tool = Lyrics:
- click a note to set lyric cursor
- type text:
  - letters append to current syllable
  - Space commits syllable and moves to next note
  - Hyphen commits syllable as Begin/Middle and moves to next note
  - Underscore toggles Extender (melisma)
- Escape exits lyric mode

### 5.3 Printing / page boundaries
- A toggle “Show page borders”
- Paper size dropdown (A4 / Letter)
- If vertical overflow (too many parts), show:
  - a warning in the UI (“This system does not fit on A4”)
  - optional suggestion: switch to Letter/Landscape or reduce staff size.

---

## 6) Tests
### Unit tests
- Migration: old scores load with one default part and all events assigned.
- Layout: multiple parts produce increasing Y offsets and stable X alignment.

### Playwright
- Load a multi-part fixture:
  - assert part labels are rendered (render commands with cssClass e.g. `part-name`)
  - assert page border commands exist when enabled
- Lyrics entry test:
  - click note, type “Hel- lo”
  - assert lyric render commands exist with cssClass `lyric`.

---

## 7) Implementation sequencing (recommended)
1) Add Parts model + migration (E1)
2) Update editing APIs to include PartId (minimal wiring)
3) Update layout to stack parts (E2)
4) Add page borders + sizing (E4)
5) Add lyrics data model + rendering (E3)
6) Add lyric entry UX + tests (E3)
