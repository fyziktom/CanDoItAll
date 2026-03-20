# 10 — Tests + performance + observability polish

Tasks:
1) Add tests:
   - novelty guard unit tests
   - pattern module tokenization tests
   - recorder import/export roundtrip
2) Performance:
   - ensure canvas render loop minimizes allocations
   - avoid recreating large arrays each frame (where feasible)
3) Observability:
   - add a debug widget toggle to show:
     - current chord confidence
     - active scale inference
     - module contributions

Acceptance:
- Test suite passes.
- No obvious perf regressions when playing quickly.
