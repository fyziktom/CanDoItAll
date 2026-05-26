# SB05 Semantic Invariants

- Invariant ID: SB05-INV-001
- Expected behavior: The reusable Tetris WASM PWA baseline scenario configures the generic `blazor-app-delivery` template with gameplay, PWA/offline, browser, console, build/test, and project-structure writeback acceptance criteria while preserving template step ownership boundaries.
- Disallowed shallow implementation: docs-only, prompt-only, fixture-only, or test-only changes that do not exercise production code paths.
- Required proof: failing-first/adversarial proof, passing production-path template projection test, source assertions, anti-stub audit, changed-file hashes.
