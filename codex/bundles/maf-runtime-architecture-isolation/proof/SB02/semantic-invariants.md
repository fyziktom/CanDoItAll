# SB02 Semantic Invariants

- Runtime collaborators must be injectable but still have explicit fallback behavior for legacy construction paths.
- Fallback construction must be centralized in resolver services rather than scattered across runtime partials.
- Composition metrics must be no-op by default and not change runtime behavior when no collector is registered.
