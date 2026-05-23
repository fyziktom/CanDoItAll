# SB03 Semantic Invariants

## Shallow-Pass Trap

A validation package with screenshot, board cell count, and clean console but no keyboard/state/localStorage proof must fail.

## Adversarial Negative Proof

Use `bundle://evidence/tetris-rerun-independent-snapshot.md` where the rendered app remains `Status Loading` and localStorage is null. Expected result: validation fails.

## Semantic Positive Proof

Launch a corrected Tetris app and use Playwright to prove status is ready/playing, keyboard input changes visible state or score/position, and high score is persisted in localStorage.

## Anti-Stub Audit

Search changed validation files and generated app proof artifacts for fake markers, placeholder data, and fixture-only checks.

## Raw Note Literal Closure

- Closes `N003` and `N007` only after real keyboard/localStorage browser proof exists.

## Production Behavior Artifact Matrix

| Artifact/Signal | Producer | Consumer | Lifecycle | Negative-Test Citation |
| --- | --- | --- | --- | --- |
| Browser semantic assertion record | Playwright/browser validation | QA and process completion | Generated during validation and attached to evidence | Pending |
| Local high-score persistence proof | Browser validation | QA/final verifier | Created after play action and read from localStorage | Pending |
