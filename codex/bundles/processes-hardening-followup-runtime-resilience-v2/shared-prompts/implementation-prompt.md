# Implementation Prompt For Codex

You are implementing `processes-hardening-followup-runtime-resilience-v2` in the CanDoItAll repository.

Read the bundle first:

- `README.md`
- `analysis/02-verified-findings.md`
- `requirements/01-normalized-requirements.md`
- `architecture/01-target-runtime-architecture.md`
- `plan/01-phase-plan.md`

Rules:

- Work one subbundle at a time.
- Do not implement from memory; re-open the current source files before changing them.
- Keep the process core generic.
- Do not add SQLite work.
- Do not solve runtime boundaries only by adding prompt text.
- Prefer typed contracts, explicit provenance, and production lifecycle events.
- After each subbundle, update `proof/SBxx/manifest.md`, transcripts, source assertions, changed-file hashes, and `reviews/01-execution-report.md`.
- If a later subbundle reveals a flaw in an earlier foundation, reopen the earlier subbundle and repair it before continuing.

Focus especially on these failure modes:

- architecture/planning step mutates product
- workflow role completes without process artifact projection
- manager recovery creates artifact but finalizer rejects it as stale
- blocked downstream step stays blocked after upstream artifact appears
- negative branch hides missing artifact production
- malformed JSON passes validation
- active running execution is finalized prematurely
