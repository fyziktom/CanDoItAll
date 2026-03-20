# PROMPT 03 — Fix beam/flag level for dotted notes (C2)

Goal: Dotted notes must keep the same beam/flag count as their base duration.

Tasks:
1) Identify all usages of `NotationDurationHelper.BeamLevel(ev.Duration)` and replace with base-duration aware logic.
   - For `NoteEvent`, use `ev.BaseDuration` not total `ev.Duration`.
   - Update the layout model to carry `BaseDuration` and `DotCount` consistently.

2) Add unit tests:
   - dotted eighth => beamlevel 1
   - dotted sixteenth => beamlevel 2

3) Run `dotnet test`.

Update checklist:
- Mark **C2** done.

STOP.
