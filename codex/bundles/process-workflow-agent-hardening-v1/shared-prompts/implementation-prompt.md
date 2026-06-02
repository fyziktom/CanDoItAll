# Shared Implementation Prompt

You are executing a prepared CanDoItAll refactoring/hardening bundle. Treat the bundle as the contract.

Before editing code:

1. Read `README.md`, `plan/01-phase-plan.md`, `traceability/`, and the current subbundle README.
2. Read the exact source references named by the subbundle.
3. Run `python scripts/validate_bundle.py --stage prepared`.
4. Run the repository bundle/subbundle validator skills if available.
5. Confirm prerequisite subbundles are completed or honestly blocked.

Implementation rules:

- Implement one subbundle at a time.
- Do not widen scope just because files overlap.
- For critical subbundles, create `proof/SBxx/manifest.md` and `proof/SBxx/semantic-invariants.md` before closure.
- Preserve working Tetris path.
- Do not hard-code scenario-specific application generation.
- Do not accept stale evidence.
- Do not record usage/cost from estimates when provider usage is available.
- Do not mutate external mailboxes outside explicit side-effect tests.
- Keep code comments in English.

After implementation:

1. Run targeted tests.
2. Run solution-level build/test gate appropriate for changed projects.
3. Run browser/host proof where required.
4. Update `reviews/01-execution-report.md`.
5. Update raw requirement closure rows.
6. Run subbundle closure validator.
7. Stop before starting dependent work if the progression gate fails.
