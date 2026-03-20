# Public-domain Pattern Corpus Module (v2.1)

Goal:
- Use known chord progression patterns (e.g., jazz standards vocabulary) to bias suggestions.
- Must be optional and toggleable.
- Must not require shipping copyrighted chord charts; use only public-domain sources or generic patterns.

## Corpus format (JSON)
Store in `assets/harmonic-assistant/patterns/*.json`:
- Each file describes a set of patterns:
  - `name`, `source`, `isPublicDomain`, `tokens`
- Tokens are normalized:
  - chord root as pitch class relative to key center OR roman numeral
  - chord quality normalized (maj, min, dom7, halfdim, dim, sus)
  - optional secondary dominant marker

We support two tokenizations:
1) Function tokens (preferred): `ii-7`, `V7`, `Imaj7`, `bVII7`, etc.
2) Absolute chord tokens: `C7`, `Fmaj7`, etc. (fallback)

## Matching
- Match against the user’s recent history window (N=8..32).
- Use:
  - suffix matching with edit tolerance (optional later)
  - or rolling hash of tokens for fast detection

## Output
- If a pattern match is found:
  - boost the next token(s) chords in planning
  - show “Pattern hint” on suggestion nodes tooltip and/or in a widget.

Widget controls:
- Toggle module on/off
- Select corpus set
- Match sensitivity (strict vs loose)
- Bias strength

Acceptance:
- When user plays a partial ii–V–I, assistant highlights likely continuation and marks it as pattern-derived.
