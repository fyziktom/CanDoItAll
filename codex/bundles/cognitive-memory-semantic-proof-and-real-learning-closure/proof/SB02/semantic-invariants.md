# SB02 Semantic Invariants

## Invariant SB02-PORTABLE-PROOF-01

- Invariant ID: `SB02-PORTABLE-PROOF-01`
- Source raw note: Completed bundle proof must be portable and active skill installation proof must not depend on user-profile artifact references.
- Expected behavior: Completed-stage validation rejects machine-specific artifact proof paths, accepts portable proof from a copied/moved bundle path, and active bundle skills are synchronized with matching repo and active SHA-256 hashes.
- Disallowed shallow implementation: A manifest can include an absolute user-profile or checkout path that passes only on the original machine, or a skill update can remain only in the repo copy while active Codex still runs stale instructions.
- Failing-first test: `PortableProof.RejectsMachineSpecificArtifactPaths` in `bundle://proof/SB02/transcripts/failing-first.txt`.
- Passing test: `PortableProof.ValidatesMovedPortableFixtureAndActiveSkillSync` in `bundle://proof/SB02/transcripts/passing.txt`.
- Changed source files: `repo://codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py` with hash `E05FAC8476996CAD28EF8071252F9263E0E5439ED6019ABC2AFFAC868DB6172A`.
- Production assertions: `repo://codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py` defines `validate_no_machine_specific_artifact_paths` and applies it to completed reports, manifests, and semantic invariant contracts.
- Red-team negative case: `bundle://proof/SB02/transcripts/failing-first.txt` proves the old machine-specific active-skill path fixture fails completed validation.
- Downstream dependency check: SB03 may start only after `bundle://proof/SB02/transcripts/passing.txt` shows active skill hash synchronization and moved-path completed validation.

