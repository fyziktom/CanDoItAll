# 07 — Progression corpus: format + loader

Goal: create a safe corpus of generic/public-domain patterns and load them.

Tasks:
1) Add `assets/harmonic-assistant/patterns/` with JSON pattern packs:
   - include generic ii–V–I, rhythm changes skeleton, blues I7–IV7–V7 patterns
   - mark source as public-domain/generic vocabulary
2) Implement loader service in C#:
   - `PatternCorpusLoader` reads embedded resources or files.
3) Add config for selecting active corpus set.

Acceptance:
- Corpus loads without exceptions.
- A unit test ensures at least one pattern pack is loaded.
