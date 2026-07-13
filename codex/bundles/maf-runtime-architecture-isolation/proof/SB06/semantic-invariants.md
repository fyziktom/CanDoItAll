# SB06 Semantic Invariants

- Direct tests must exercise production collaborators, not test-only wrappers.
- Reflection tests for moved behavior should be removed when internal collaborators are directly available.
- Full-runtime smoke tests must remain; direct collaborator tests complement them rather than replacing them.
