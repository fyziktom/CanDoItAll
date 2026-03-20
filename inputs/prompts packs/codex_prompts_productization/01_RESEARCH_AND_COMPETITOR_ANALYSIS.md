# 01_RESEARCH_AND_COMPETITOR_ANALYSIS.md

## Goal
Create a detailed competitor research document and extract actionable product/UX patterns that will drive our redesign and backlog.

## Output files (create/overwrite)
- `docs/product/competitor-analysis.md`
- `docs/product/ux-patterns-from-competition.md`

## Scope categories (MUST cover all)
1) Learning to read sheet music / sight-reading
2) Piano training with MIDI input + realtime feedback + statistics
3) Chord progression / harmony generation (“chord trainer”)
4) Notation editors (web + desktop)
5) Accompaniment tools (metronome, drums, backing tracks, adaptive tempo)

## Competitor set (minimum list)
You MUST include at least these competitors (you can add more):
- Simply Piano
- Flowkey
- Yousician
- Skoove
- Piano Marvel (incl. SASR / sight-reading)
- Sight Reading Factory
- ABRSM Piano Sight-Reading Trainer
- Complete Music Reading Trainer
- musictheory.net exercises / Tenuto
- MuseScore
- Dorico
- Sibelius (and/or Finale context)
- Noteflight
- Flat.io
- iReal Pro
- Band-in-a-Box
- Chordify
- Hooktheory / Hookpad

## Required structure in `docs/product/competitor-analysis.md`

### A) Summary table
Create a table with rows = competitors and columns:
- Category
- Core value proposition
- Key features (bullet list)
- UX patterns (navigation, onboarding, lesson flow, gamification)
- MIDI/audio approach (if relevant)
- Monetization model
- What we should copy
- How we can beat it with PWA/WASM + our “killer feature”

### B) UX patterns library
Create a section with reusable patterns:
- Onboarding patterns (goal selection, skill level, device setup)
- Daily practice / streaks / reminders
- Lesson map / curriculum gating
- Micro-feedback loops (timing, accuracy, dynamic difficulty)
- Sound design (metronome/drums/backing)
- Progress dashboards (weekly charts, mistakes, heatmaps)
- “Library” experience (search, tags, favorites, downloads)
- Premium upsell patterns that do not annoy free users

### C) “What we will NOT copy”
Explicitly list patterns we should avoid (e.g., aggressive paywalls before user feels value, forced account creation too early, noisy gamification).

## Input (use this as baseline; verify and expand)
Below is a curated baseline. You may refine the text, but do not omit items:

### Sight-reading & note-reading apps (patterns to copy)
- Gamified levels / chapters + short drills (Complete Music Reading Trainer style).
- Timed sessions + review mistakes + daily stats (Music Tutor / similar).
- Optional MIDI input as a faster answer method (some reading trainers support MIDI).

### Piano training apps (patterns to copy)
- “Connect your piano” first-time setup wizard.
- Realtime feedback: wrong note highlighting, timing bars, repetition on mistakes.
- Adaptive difficulty and practice loops.

### Notation editors (patterns to copy)
- Library-first home: recent scores + templates.
- Clear import/export (MusicXML, MIDI, PDF), share links, collaborative editing (web editors).
- Desktop-level keyboard shortcuts, selection affordances, undo/redo reliability.

### Harmony / chords (patterns to copy)
- Chord dictionaries with inversions and voicings.
- Progression suggestions and “why this works” explanations.
- Backing tracks / accompaniment generation for practicing.

### Accompaniment tools (patterns to copy)
- Accurate scheduling, swing/humanize, and loop practice.
- Tempo tracking / adaptive tempo for practice scenarios.

## Acceptance criteria
- Document includes all required categories and competitors.
- Each competitor row includes monetization and actionable “copy / beat” notes.
- There is a dedicated section describing how our PWA/WASM approach is an advantage and where the web is weaker (Web MIDI support differences, audio latency, storage).
- The “killer feature” is articulated as a differentiator: realtime harmonic assistant with beautiful visualization and meaningful suggestions.

## Verification steps
- Ensure the created markdown renders correctly (tables readable).
- Ensure every required competitor appears in the table.
