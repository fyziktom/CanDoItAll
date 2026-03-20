# Performance checklist

## Targets
- Median update latency (stable chord -> UI update): < 250ms
- p95 update latency: < 350ms (reference device/browser)
- Canvas render per update: ideally < 10ms for typical node counts

## Checks
- [ ] Canvas renderer avoids large allocations each render:
  - reuse arrays/maps where possible
  - avoid creating many gradients in tight loops if not needed (cache per edge or per color pair if possible)
- [ ] Scoring tracker stays bounded (MaxTrackedNotes enforced).
- [ ] Chord recognition loop is bounded (max K).
- [ ] Beam search remains bounded (width/horizon clamps).
- [ ] Debug logging is throttled (does not spam).

## Debug tools
- Use the debug panel (if implemented) to inspect:
  - pitch class scores
  - inferred scale context
  - top candidates and confidence
  - suggestion reasons
