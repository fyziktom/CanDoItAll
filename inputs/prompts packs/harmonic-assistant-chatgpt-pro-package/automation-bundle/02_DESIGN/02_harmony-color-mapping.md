# Harmony → Color → Vertical Position mapping

This section defines how to compute:
1) **WorldYNormalized** (0..1) for vertical mood position, and
2) **Color** (hex) for nodes/paths/background.

Two viable strategies are defined.

---

## Strategy A: Rule-based heuristic mapping (fast, predictable, implementable)

### A1) Extract chord features
From `ChordInstance`:
- root pitch class
- symbol string (e.g., `maj`, `min`, `7`, `maj7`, `m7b5`, `dim`, `sus`, `alt` ...)
- pitch class set (interval content)
- optionally: whether chord contains tritone, b9/#9, #11, etc (if accessible)

### A2) Compute two orthogonal metrics
#### 1) Darkness (0..1)
A proxy for “dark harmony”:
- base by quality:
  - major/maj7: 0.20
  - sus2/sus4: 0.25
  - dominant 7 / 9 / 13: 0.35
  - minor/min7: 0.65
  - half-diminished m7b5: 0.78
  - diminished: 0.85
  - altered dominant (#9/b9 etc): 0.65..0.80 (dark + tense)
- add small increments:
  - if contains b9/#9/b5/#5: +0.08
  - if contains #11: +0.05
- clamp to [0..1]

#### 2) Energy (0..1)
A proxy for “sharp/vivid vs calm”:
- major triad: 0.35
- maj7: 0.32
- sus: 0.28
- dominant 7: 0.75
- minor 7: 0.45
- diminished/half-dim: 0.70
- altered dominant: 0.90
- extensions (9/11/13): +0.05..+0.10 (depending on symbol)

### A3) Map to WorldYNormalized
WorldYNormalized encodes darkness:
- `WorldY = lerp(0.15, 0.85, Darkness)`
This means:
- bright/happy chords cluster near 15% from top
- dark chords cluster near 85% from top

Example calibration:
- C7: Darkness ~0.35 => WorldY ~0.40 (tunable toward 0.30 per product expectation)
- C# minor: Darkness ~0.65 => WorldY ~0.63 (tunable toward 0.70)

### A4) Map to Color (HSL → hex)
We need the product mapping:
- red hues = sharper/vivid
- blue & green = calmer
- dark shades = dark harmonies (across families)

We implement this by:
1) pick color family by Energy:
   - Energy ≥ 0.66 → **red/orange family**
   - Energy < 0.66 → **blue/green family**
2) within family, pick hue influenced by circle-of-fifths:
   - compute `fifthsIndex` 0..11
   - apply small hue offset within family: `offset = (fifthsIndex - 6) * 3°` (±18°)
3) map Darkness to lightness:
   - `lightness = 68% - Darkness * 38%` (dark → lower L)
4) map Energy to saturation:
   - `saturation = 40% + Energy * 40%`

Family hue ranges:
- red/orange: base 18°
- green: base 150°
- blue: base 210°
Choose green vs blue based on chord quality:
- major/sus → green-ish base
- maj7/add9 → blue-ish base
(or alternate by root parity for variety)

Return final hex.

Pros:
- deterministic, easy to tune
- does not require heavy music theory inference

Cons:
- not “tonal-space aware” beyond small fifths hue offset

---

## Strategy B: Structured mapping using circle-of-fifths + tension metrics (still implementable)

This strategy produces a more coherent multi-dimensional structure.

### B1) Compute a tonal-space embedding (2D + scalar)
For each chord:
- **FifthsAngle** (0..2π):
  - map root pitch class to circle-of-fifths index:
    circle = [C, G, D, A, E, B, F#, C#, G#, D#, A#, F]
  - index distance gives tonal relatedness
  - angle = index * (2π / 12)

- **Darkness** (0..1): same as Strategy A
- **Tension** (0..1): emphasize alterations, diminished content, dominant function

### B2) Use embedding for both visualization and planning
Visualization:
- WorldYNormalized uses Darkness (as above)
- Hue uses FifthsAngle but is *constrained* into families:
  - calm family (blue/green) for low Tension
  - vivid family (red/orange) for high Tension
  - within family: hue = baseFamilyHue + small angle-derived offset
- Edge gradients encode movement in the embedding:
  - fromColor -> toColor (or pathColor)

Planning:
- Add scoring terms that prefer small fifths-distance when appropriate
- Allow big jumps only when style pack encourages modulation devices

Pros:
- aligns better with how musicians perceive harmonic distance
- supports “harmony is not flat” multi-dimensional thinking

Cons:
- requires careful tuning to avoid overly “mechanical” movement

---

## Recommended implementation approach
- Implement Strategy A first (quick win, controllable).
- Add Strategy B as an option (`HarmonyColorMappingMode`) and migrate gradually.
- Keep mapping code in C# (shared by canvas snapshot + route planning).
