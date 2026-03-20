# 03 — Implement Harmony→Mood→Color mapping helper (shared C#)

Goal: create a reusable mapping that both:
- canvas visualization, and
- route planning
can use consistently.

## Add new files
- `src/MusicTheory.Core/Generation/Realtime/HarmonyVisualMapping.cs` (or similar name)

## 1) Implement mapping per design
Implement Strategy A first (rule-based), and make Strategy B optional behind an enum.

### Types to add
- `public enum HarmonyColorMappingMode { HeuristicRuleBased = 0, CircleOfFifthsStructured = 1 }`
- `public sealed record HarmonyVisualMetrics(
    double Darkness,
    double Energy,
    double WorldY,
    string ColorHex,
    int RootPitchClass,
    string Symbol
  );`

### Public API
- `public static HarmonyVisualMetrics Compute(ChordInstance chord, HarmonyColorMappingMode mode = ..., EnharmonicPreference pref = EnharmonicPreference.Sharps)`
- `public static double ComputeWorldY(ChordInstance chord, HarmonyColorMappingMode mode = ...)`
- `public static string ComputeColorHex(ChordInstance chord, HarmonyColorMappingMode mode = ...)`

### Heuristic rules (must be tunable constants)
Follow `/02_DESIGN/02_harmony-color-mapping.md`:
- parse chord symbol string in a robust way (case-insensitive)
- compute:
  - Darkness (0..1)
  - Energy (0..1)
- WorldY = lerp(0.15, 0.85, Darkness)
- Color = HSL family selection:
  - energy high → red/orange base
  - energy low → blue/green base
  - small hue offset via circle-of-fifths index
  - lightness lowered by Darkness
  - saturation increased by Energy

### Implementation requirements
- No `System.Drawing`.
- Implement HSL→RGB conversion manually.
- Clamp inputs and always return valid `#RRGGBB`.

## 2) Add a minimal unit test file
- `tests/MusicTheory.Tests/HarmonyVisualMappingTests.cs`
Add tests that validate:
- returns stable hex format
- darker chords map to higher WorldY than bright chords
- dominant 7 chord tends to red-ish family vs major triad calmer family (heuristic check: compare hue bucket indirectly by checking which component dominates; keep the test loose to avoid brittleness)

## Acceptance criteria
- Mapping compiles and tests pass.
- Mapping functions are deterministic and do not allocate excessively.

## Self-check
- `dotnet test tests/MusicTheory.Tests/Zyphonote.MusicTheory.Tests.csproj --filter "FullyQualifiedName~HarmonyVisualMappingTests"`
