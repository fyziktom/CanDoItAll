# Codex Result Gap Analysis (Why the previous run stopped early)

The previous Codex run implemented most of **Milestone A** (key/time signatures + accidentals) but skipped several major items from the original request.

## Why it likely happened

- The pack previously contained a single prompt which explicitly said: “Now start with Milestone A.”
- Large changes require **multiple Codex runs**. Without a persistent in-repo progress file and an enforced prompt chain, Codex tends to stop after the first milestone.

## What Milestone A usually includes

When implemented correctly, you should see:

- `KeySignature`, `KeySignatureChange` and `TimeSignatureChange` model types.
- `ScoreDocument` contains:
  - `KeySignature`
  - `KeySignatureChanges`
  - `TimeSignature`
  - `TimeSignatureChanges`
- `ScoreContext.GetKeySignatureForMeasure(...)` and `GetTimeSignatureForMeasure(...)`
- Layout engine emits:
  - key signature glyphs
  - time signature glyphs
  - note accidentals
- Playwright fixtures (e.g. key change / time change) and assertions.

## What was missing / commonly skipped

These are the big-ticket items from the original user request that frequently get skipped if prompts are not split:

1) **Canvas-first toolbars / HUD**
   - Moving editing controls from Blazor HTML into the overlay canvas.
   - HUD hit-testing and rendering.
   - E2E tests that click inside canvas, not HTML buttons.

2) **Engraved slurs and ties**
   - Slurs as filled ribbons (VexFlow Curve algorithm).
   - Ties rendered (VexFlow StaveTie algorithm).

3) **Transposition**
   - Batch transpose with key-signature-aware spelling.

4) **Key/time signature editing UI**
   - Not just rendering changes, but allowing users to insert/remove them at measure boundaries.

## Fix strategy

This pack version introduces:

- A prompt chain (`prompts/INDEX.md`) with one milestone per prompt.
- Persistent progress files (`codex/STATUS.md`, `codex/NEXT_PROMPT.md`).
- Mandatory test gating at each milestone.

