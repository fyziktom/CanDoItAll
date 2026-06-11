# SB06 Semantic Invariants

## Invariant SB06_INV_001
- Invariant ID: `SB06_INV_001`
- Source raw notes: RN-001 through RN-004 require final stabilization classification, live OpenAI proof, exact blocker identification, and no premature runtime extraction.
- Expected behavior: The final decision reconciles fresh build, full unit, focused integration, browser, live OpenAI, and boundary evidence into one explicit classification without counting skipped or failed live proof as a pass.
- Disallowed shallow implementation: Claiming merge-ready while live OpenAI fails, reporting a provider model rejection as a process runtime regression, or relying on report rows without command transcripts and proof artifacts.
- Failing-first test: `bundle://proof/SB06/transcripts/red-team-fake-proof-audit.txt` rejects a merge-ready decision while live-provider-blocked remains true and verifies SB01-SB05 manifests plus final transcripts.
- Passing test: `bundle://proof/SB06/transcripts/final-source-assertions.txt` verifies final build, unit, integration, Playwright, and live classification evidence.
- Changed source files: no SB06 product source edits. Final decision and proof artifacts are recorded in `bundle://proof/SB06/manifest.md`.
- Production assertions: `bundle://proof/SB06/transcripts/final-build.txt`, `bundle://proof/SB06/transcripts/final-unit-tests.txt`, `bundle://proof/SB06/transcripts/final-focused-integration-matrix.txt`, and `bundle://proof/SB06/transcripts/final-playwright-project-structure-completed-run.txt` are green.
- Live-provider assertion: `bundle://proof/SB06/transcripts/final-live-classification.txt` records `live-provider-blocked` for OpenAI HTTP 400 `model_not_found` on `5.4-mini`.
- Final classification: `runtime-stable-live-blocked`.
