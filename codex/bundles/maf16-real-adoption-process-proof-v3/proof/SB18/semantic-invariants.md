# SB18 Semantic Invariants

- Invariant ID: `SB18-INV-001`
- Source raw note: `repo://codex/bundles/maf16-real-adoption-process-proof-v3/requirements/01-normalized-requirements.md` RQ10.
- Expected behavior: Final closure requires focused runtime tests, prepared/completed bundle validation, and a clear next live-run gate.
- Disallowed shallow implementation: Status-only closure, pending proof manifests, or a full live run attempted before deterministic artifact proof.
- Failing-first test: `bundle://proof/SB18/transcripts/failing-first.txt`
- Passing test: `bundle://proof/SB18/transcripts/passing.txt`
- Changed source files: `repo://codex/bundles/maf16-real-adoption-process-proof-v3/reviews/01-execution-report.md`, `repo://codex/bundles/maf16-real-adoption-process-proof-v3/plan/01-phase-plan.md`, and critical source/test files cited in `bundle://proof/SB18/transcripts/changed-file-hashes.txt`.
- Production assertions: Runtime changes are proven by SB11 and SB13 tests; final bundle closure is proven by the completed validator.
- Red-team negative case: `bundle://proof/SB18/transcripts/failing-first.txt` records the initially invalid bundle gate.
- Downstream dependency check: The next live run is gated on the runbook and abort criteria instead of being executed from a partially closed bundle.
