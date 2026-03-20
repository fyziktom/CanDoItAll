# 08 — Pattern matcher module + UI

Goal: detect when user history matches known patterns and bias suggestions.

Tasks:
1) Implement `PatternMatcherModule`:
   - normalize user chord history → tokens
   - match suffix windows against corpus tokens
   - if match found, boost next token chord candidates
2) Add widget controls:
   - enable/disable
   - bias strength slider
   - sensitivity dropdown (strict/loose)
3) Add UI annotations:
   - when a suggestion is pattern-derived, add tooltip meta: `patternName`, `source`.

Acceptance:
- When user plays ii–V, assistant suggests I and labels it as pattern-derived.
- Module is toggleable and adjustable in canvas widget.
