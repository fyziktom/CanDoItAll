# 12 — Final integration checklist + docs update + performance sanity

Goal: ensure all non-negotiable requirements are met, update docs, and stabilize.

## Files to review
- docs/harmonic-assistant/*
- `src/App.Blazor/Pages/Harmony.razor`
- `src/App.Web/wwwroot/harmonicAssistantCanvas.js`
- `src/MusicTheory.Core/Recognition/*` (scoring + detection)
- `src/MusicTheory.Core/Generation/Realtime/RealtimeHarmonicAssistantEngine.cs`

## Checklist (must be true)
### Canvas visualization
- [ ] uses canvas for graph rendering
- [ ] single horizontal flow, no wrapping
- [ ] branches from current go right
- [ ] happy/brighter go up, darker go down
- [ ] centerline is mid-height and visually explicit
- [ ] background tint shifts with current chord mood/color
- [ ] bigger markers than baseline
- [ ] canvas UI control changes text size (mouse + touch)
- [ ] auto zoom-to-fit prevents clipping
- [ ] history step-count is configurable

### MIDI scoring + detection
- [ ] scored pitch class selection loop implemented
- [ ] decay and hold weighting configurable
- [ ] confidence gating configurable
- [ ] melody noise suppression works
- [ ] low-scored tones preserved for scale inference
- [ ] blues/pentatonic scales supported and inferable

### Route planning
- [ ] uses inferred scale context signal
- [ ] uses style pack device weights + constraints
- [ ] uses tonal distance (circle-of-fifths) feature
- [ ] remains deterministic and bounded

### Tests + observability
- [ ] unit tests cover scoring + inference + planning improvements
- [ ] debug hooks exist (panel or logging)

## Update docs
- Add a new doc file:
  - `docs/harmonic-assistant/wow-canvas-and-scoring-design.md`
Include:
- screenshots description (text)
- parameter list
- tuning notes

## Performance sanity
- Ensure no per-frame allocations spikes in canvas draw loop:
  - reuse arrays/maps where possible
- Ensure chord evaluation loop remains bounded and fast.

## Final self-check
- `dotnet test` (full run)
- Manual QA using `/05_VALIDATION/qa-checklist.md` in this bundle.
