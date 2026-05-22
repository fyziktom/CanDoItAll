# Shared Implementation Prompt

```text
Implement only the assigned subbundle from repo://codex/bundles/process-browser-evidence-runtime-proof-hardening.

Start by reading:
- README.md
- inputs/00-original-request.md
- inputs/01-source-artifacts.md
- analysis/01-current-state.md
- requirements/01-normalized-requirements.md
- plan/01-phase-plan.md
- the assigned subbundle README

Keep the process core generic. Do not hardcode Tetris, game board dimensions, piece names, Blazor-specific gameplay rules, or any product-specific acceptance logic into process runtime. Runtime code may enforce generic proof categories, artifact existence, artifact content validity, console phase classification, and representative-interaction assertions. Domain facts must come from project structure context, step evidence contracts, skills, or agent instructions.

Use strongly typed code for new classifications and proof states. Do not hide missing evidence behind fallback success. If evidence is required but cannot be imported, mirrored, parsed, or validated, return a repair/blocking outcome and record an actionable conformance observation.

For critical subbundles, produce failing-first and passing proof. Update proof/SBxx/manifest.md and proof/SBxx/semantic-invariants.md with changed-file hashes, command transcripts, source assertions, anti-stub audit, and the production behavior artifact matrix. Do not let downstream subbundles start until the progression gate honestly passes.
```
