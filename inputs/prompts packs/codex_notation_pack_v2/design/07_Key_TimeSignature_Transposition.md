# 07 — Key signature, time signature changes, and transposition (implementation design)

This design is written to fit the existing Zyphonote model/layout/render architecture.

---

## 1) Data model extensions

### 1.1 KeySignature definition
Add a model type:

```csharp
public enum KeyMode
{
    Major,
    NaturalMinor
    // Optional: Dorian, etc. (key signatures still map via relative major)
}

public readonly record struct KeySignature(
    NoteName Tonic,
    KeyMode Mode,
    EnharmonicPreference Preference,
    int Fifths // -7..+7 (negative = flats)
);
```

Store changes as a list (measure-boundary changes are enough for 99% of scores):

```csharp
public sealed record KeySignatureChange(int MeasureIndex, KeySignature Key);
```

### 1.2 TimeSignature changes
Likewise:

```csharp
public sealed record TimeSignatureChange(int MeasureIndex, TimeSignature TimeSignature);
```

### 1.3 ScoreDocument updates
```csharp
public sealed class ScoreDocument
{
    public TimeSignature TimeSignature { get; set; } // keep as default/fallback
    public KeySignature KeySignature { get; set; }   // new default key

    public List<TimeSignatureChange> TimeSignatureChanges { get; } = new();
    public List<KeySignatureChange> KeySignatureChanges { get; } = new();
}
```

Migration strategy:
- If `KeySignature` is missing in JSON, default to C major.
- If `TimeSignatureChanges` is missing, keep old behavior.

---

## 2) Effective “context” resolution (per measure)

Add helpers:

```csharp
public static class ScoreContext
{
    public static TimeSignature GetTimeSignatureAt(ScoreDocument score, int measureIndex) { ... }
    public static KeySignature GetKeySignatureAt(ScoreDocument score, int measureIndex) { ... }
}
```

Rules:
- Start with global defaults.
- Apply the last change whose `MeasureIndex <= current`.

---

## 3) Measure capacity and editing commands

### 3.1 Capacity per measure
Replace all uses of:
- `score.TimeSignature.Capacity`

with:
- `ScoreContext.GetTimeSignatureAt(score, measureIndex).Capacity`

This affects:
- insert/shift logic,
- auto-rest fill,
- quantization grids,
- beaming grouping by meter.

### 3.2 Beaming grouping
Beaming engine should accept the effective time signature for the measure.  
If you already pass `TimeSignature` into beaming logic, route the per-measure value.

---

## 4) Engraving key signatures and time signatures

### 4.1 Where to draw
- First measure of the score: draw clef + key + time.
- Measure where a change occurs: draw the new key/time after clef (or after barline).

### 4.2 Layout reservation
Add a left-side reservation zone in each measure for:
- clef (first system or clef change)
- key signature (first measure or key change)
- time signature (first measure or time change)

This modifies:
- `MeasureLayout.ContentLeft`

### 4.3 Glyphs required
Key signature:
- `accidentalSharp`, `accidentalFlat`, `accidentalNatural` (for cancellation)
Time signature:
- `timeSig0..9`, plus `timeSigCommon`, `timeSigCutCommon` (optional)

Use either:
- font glyphs (SMuFL codepoints), OR
- SVG/path glyphs from `assets/svg/vexflow-bravura`.

---

## 5) Accidentals on notes (per-measure state)

### 5.1 Core rule
Accidentals are shown when the note’s spelled accidental differs from:
- the key signature default for that letter, OR
- the last accidental used for the same letter+octave earlier in the same measure.

Accidental state resets at each barline.

### 5.2 Implementation plan
Introduce an `AccidentalEngine` in layout:

```csharp
public sealed class AccidentalEngine
{
    public IReadOnlyList<AccidentalLayout> Compute(
        ScoreDocument score,
        MeasureLayout measure,
        KeySignature key);
}
```

- For each staff separately, and (optionally) per voice (depending on desired correctness):
  - keep a dictionary `{ (letter, octave) -> Accidental }`
  - initialize from key signature defaults
  - process events left-to-right (by Start)
  - decide if accidental glyph is needed
  - output `AccidentalLayout` with glyph id and x/y

### 5.3 Placement in chords
- Sort chord noteheads by y.
- For each accidental, place to the left of the notehead.
- If multiple accidentals collide, add extra left offset (stacking columns).

A simple heuristic is acceptable initially.

---

## 6) Transposition

### 6.1 Feature definitions
Provide two operations:
1) **Transpose score** to a target key.
2) **Transpose selection** by an interval.

### 6.2 Written vs sounding pitch (optional)
If you want transposing instruments:
- keep `NoteEvent.Pitch` as written pitch,
- store an `InstrumentTransposition` per staff/part,
- playback uses sounding pitch = written + transposition.

### 6.3 Practical algorithm (score transpose)
Inputs:
- interval in semitones (e.g., +2), OR
- target key (e.g., from C major to D major)

For each `NoteEvent`:
- compute midi number `m`
- new midi = `m + interval`
- choose spelling using `NotePitch.FromMidiNumber(newMidi, preference)` BUT:
  - preference should be derived from target key: if key has flats, prefer flats.

For chord symbols:
- optional: transpose chord root and bass slash note.

For key signature:
- update global key signature, or add a key change.

### 6.4 Selecting the “best” key signature
Given target tonic pitch class and mode:
- compute both sharp and flat representations,
- choose the one with fewer accidentals (abs(fifths) minimal),
- clamp to -7..+7.

---

## 7) Tests

### Unit tests (MusicTheory.Tests)
Add tests:
- `KeySignatureContext_LastChangeWins`
- `TimeSignatureContext_LastChangeWins`
- `MeasureCapacity_UsesTimeSigPerMeasure`
- `AccidentalEngine_ShowsNaturalWhenLeavingKeySigAlteration`
- `TransposeScore_ChangesPitchAndKey`

### Playwright tests (App.Web.PlaywrightTests)
Add screenshot tests:
- key signature rendered in measure 0,
- key change rendered at later measure,
- accidentals appear/disappear correctly across barlines,
- time signature change reflows measure spacing.
