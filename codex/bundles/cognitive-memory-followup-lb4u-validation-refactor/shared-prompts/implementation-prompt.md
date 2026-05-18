# Implementation Prompt

Use `candoitall-bundle-workflow` and `candoitall-api-cognitive-memory`. Start by reading this bundle README, the active subbundle README, `inputs/00-original-request.md`, `analysis/01-current-state.md`, and `reviews/01-execution-report.md`.

Rules:

- Make the smallest correct implementation change for the active subbundle.
- Preserve original cognitive-memory v2 invariants.
- Do not read or ingest `C:\Users\lucys\OneDrive - TechnicInsider\Brano\LB4U\routery hesla`.
- Keep LB4U sources read-only.
- Add or update tests before high-risk refactors.
- Do not introduce silent provider fallback, silent truncation, or generated truth without review.
- Update the workbook and execution report after proof is captured.
- If API or skill behavior changes, update `candoitall-api-cognitive-memory` and docs in subbundle 10.

Before coding, state the subbundle entry gate and the exact files you expect to touch. After coding, record validation commands and evidence in the execution report.
