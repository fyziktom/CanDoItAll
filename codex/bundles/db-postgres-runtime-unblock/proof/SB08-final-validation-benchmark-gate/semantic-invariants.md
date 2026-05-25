# SB08 Semantic Invariants

## Invariants

### SB08-I1 Final proof is artifact-backed, not prose-only

Raw note: "Final proof must include build, unit tests, targeted component tests, PostgreSQL integration tests, fresh migration baseline proof, residue audit, concurrency tests, and a short before/after bottleneck analysis."

Expected behavior: final closure cites concrete transcript files, screenshot artifacts, source assertions, hashes, and explicit blockers.

Shallow-pass trap: fill an execution report table while missing transcripts or treating failed environment checks as success.

Adversarial negative proof: `bundle://proof/SB08-final-validation-benchmark-gate/fake-proof-red-team.md` records remote fetch and broad integration as blockers, not passes.

Semantic positive proof: SB08 manifest command table and execution report cite all final artifacts.

Production assertions: `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/source-assertions.txt`.

Changed source files: see `bundle://proof/SB08-final-validation-benchmark-gate/changed-file-hashes.tsv`.

Downstream dependency check: completed-stage validator checks required critical manifests, semantic invariant files, transcripts, and browser screenshots.

### SB08-I2 Merge-readiness recommendation preserves validation exceptions

Raw note: "Full validation suite passes or every non-passing item is explicitly quarantined with reason and owner."

Expected behavior: source implementation is recommended for merge only after rerunning remote ancestry and broad integration in a correctly provisioned environment.

Shallow-pass trap: say "tests passed" based only on focused tests.

Adversarial negative proof: broad non-quarantined integration transcript remains cited as blocked and final report names the PostgreSQL auth failure.

Semantic positive proof: focused tests pass for changed high-risk surfaces while remaining environment blockers are isolated.

Production assertions: `bundle://reviews/01-execution-report.md`.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| Final validation closure state | `bundle://proof/SB08-final-validation-benchmark-gate/manifest.md` | `bundle://reviews/01-execution-report.md` | `bundle://scripts/validate_bundle.py` | `bundle://proof/SB08-final-validation-benchmark-gate/fake-proof-red-team.md` |
