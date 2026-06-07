# SB036 Semantic Invariants

## Invariants

- Invariant ID: `SB036-INV-001`
- Source raw note: `Complete execution report, final Core readiness decision, driver readiness decision, and proof index.`
- Expected behavior: Final closure records all 36 subbundle rows as passed, closes raw notes, stores final decisions, indexes proof, preserves no-Core/no-production-driver/no-UI constraints, and passes final validation.
- Disallowed shallow implementation: Marking the bundle complete while rows are pending, raw notes are pending, proof artifacts are missing, final decisions are absent, broad Core extraction is approved, production driver APIs appear, or validation is not run.
- Failing-first test: `N/A - final closure/proof-only subbundle; no production behavior change was intended.`
- Passing test: `bundle://proof/SB036/transcripts/final-source-assertions.txt`
- Changed source files: `repo://codex/bundles/process-core-pre-extraction-consolidation-driver-readiness-v1/architecture/03-final-core-readiness-decision-template.md`, `repo://codex/bundles/process-core-pre-extraction-consolidation-driver-readiness-v1/proof/index.md`, `repo://codex/bundles/process-core-pre-extraction-consolidation-driver-readiness-v1/reviews/01-execution-report.md`, `repo://codex/bundles/process-core-pre-extraction-consolidation-driver-readiness-v1/reviews/02-final-red-team-review.md`, `repo://codex/bundles/process-core-pre-extraction-consolidation-driver-readiness-v1/README.md`, `repo://codex/bundles/process-core-pre-extraction-consolidation-driver-readiness-v1/subbundles/SB036/README.md`
- Production assertions: `bundle://proof/SB036/transcripts/final-source-assertions.txt`
- Red-team negative case: Pending raw notes, missing proof files, collapsed rows, broad Core approval, production driver tokens, UI/media drift, or final validator failure fails SB036 proof.
- Downstream dependency check: Future work may propose a narrow Process Core cutline only; production driver APIs remain proposal-only and out of the next Core implementation.

## Raw Note Closure

- Do not rush Process Core unless clearly justified: `Solved with final decision limiting future Core to a narrow proposal.`
- Preserve existing functionality: `Solved with critical build, full unit tests, focused integration tests, and source proof.`
- Fewer, broader subbundles: `Solved with 36 rows across 12 phases.`
- Move closer to Process Core and drivers: `Solved with candidate map, driver readiness, red-team review, and final decisions.`
- No production driver API: `Solved with final no-driver source scan and proposal-only driver decision.`
- No UI/mobile proof: `Solved with no UI/mobile/media drift scan and N/A browser analytics rows.`
