# Manual QA checklist — Harmonic Assistant “WOW” upgrade

Use this checklist after Codex finishes implementation.

## A) Smoke tests
- [ ] App builds and runs.
- [ ] `/harmony` page loads without MIDI permissions.
- [ ] Manual chord input works (enter `Cmaj7`, click Apply).
- [ ] Reset clears history and suggestions.

## B) Canvas visualization (core requirements)
- [ ] Canvas graph is displayed and updates on chord changes.
- [ ] The graph is a single horizontal timeline; suggestions do not wrap under history.
- [ ] Current chord node is visually emphasized and sits on the centerline (mid-height).
- [ ] Branches go right; “brighter/happier” appear above; “darker” below.
- [ ] History shows up/down travel with curved connectors.
- [ ] Background tint shifts when moving from bright to dark.
- [ ] Nodes are larger than baseline; chord labels are readable.
- [ ] Text size can be changed via in-canvas A− / A+ buttons.
- [ ] Auto-zoom keeps content in one line without clipping.

## C) MIDI scoring + detection
If you have a MIDI keyboard:
- [ ] Play arpeggiated C7 (C-E-G-Bb sequentially). Detection remains C7.
- [ ] Add melody notes (Eb, F, F#). Detection remains C7.
- [ ] Scale context panel shows blues-like scale suggestion.
- [ ] Decay works: stop playing; after the configured window, detection stabilizes to empty/none.

## D) Route planning quality
- [ ] With C7 + blues notes, suggestions include blues-friendly next chords (e.g., F7, G7, turnarounds).
- [ ] In major ii-V-I context, the top path feels musically coherent.
- [ ] Switching style pack changes suggestions meaningfully.

## E) Responsiveness
- [ ] Resize window: canvas remains crisp, no blur.
- [ ] Mobile rotation: canvas re-lays out and stays readable.
