# SB01 Semantic Invariants

## Invariant SB01-PORTABILITY-01

- Invariant ID: `SB01-PORTABILITY-01`
- Source raw note: Completed proof must be portable across Windows, WSL, Linux, CI, and relocated repo roots.
- Expected behavior: The validator resolves `repo://` references against an explicit or discovered repository root, resolves `bundle://` references against the bundle root, recognizes Windows and POSIX absolute path syntax without relying on the current OS, and validates a relocated fixture with the same portable proof.
- Disallowed shallow implementation: Treating only the current machine path as valid, or accepting a manifest whose only durable source and artifact references are absolute machine-specific paths.
- Failing-first test: `ArtifactProof.RejectsMachineSpecificOnlyPaths` in `bundle://proof/SB01/transcripts/fake-proof-fixtures.txt`.
- Passing test: `ArtifactProof.ValidatesRelocatedPortableFixture` in `bundle://proof/SB01/transcripts/positive-portable-fixture.txt`.
- Changed source files: `repo://codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py`, skill instruction files, and validator fixtures listed in `bundle://proof/SB01/manifest.md`.
- Production assertions: Source assertions transcript proves `--repo-root`, `--bundle-root`, `repo://`, `bundle://`, portable artifact extraction, and OS-independent absolute path recognition exist in the validator.
- Red-team negative case: `artifact-proof-machine-specific-paths` must fail completed-stage validation because it lacks a portable durable reference.
- Downstream dependency check: `bundle://proof/SB01/transcripts/prepared-validator-after-sb01.txt` proves the follow-up bundle validates at prepared stage after source references were converted to portable form.

## Invariant SB01-INVARIANT-02

- Invariant ID: `SB01-INVARIANT-02`
- Source raw note: Critical closure must tie behavior to semantic invariants, not report prose alone.
- Expected behavior: Every completed critical subbundle must include a semantic invariant contract, the execution report must cite it, the manifest or subbundle README must cite it, and invariant IDs must appear in cited transcripts.
- Disallowed shallow implementation: Filling a semantic proof table while omitting the invariant contract or using transcript output that is not tied to an invariant ID.
- Failing-first test: `ArtifactProof.RejectsMissingOrUncitedInvariantContracts` in `bundle://proof/SB01/transcripts/fake-proof-fixtures.txt`.
- Passing test: `ArtifactProof.ValidatesCompleteFixture` in `bundle://proof/SB01/transcripts/positive-portable-fixture.txt`.
- Changed source files: `repo://codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py`, `repo://codex/skills/bundles/candoitall-bundle-execution/references/artifact-backed-proof-manifest.md`, and the active skill-root copies listed in `bundle://proof/SB01/manifest.md`.
- Production assertions: The validator contains semantic invariant contract discovery, required-label validation, invariant-id extraction, and transcript invariant-id matching.
- Red-team negative case: `artifact-proof-missing-semantic-invariants` and `artifact-proof-invariant-id-not-cited` must fail completed-stage validation.
- Downstream dependency check: Active skill sync proof shows Codex will use the invariant-aware workflow before SB02 starts.
