# SB01 Semantic Invariants

## Invariant SB01-CLAIM-TO-CODE-01

- Invariant ID: `SB01-CLAIM-TO-CODE-01`
- Source raw note: Execution proof must not claim `Czech/diacritic`, `embedding-backed`, `provider-backed`, `automatic`, `scheduled`, `claim-specific`, `line-level`, `domain synthesis`, or `portable proof` behavior unless source behavior, tests, and negative fixtures prove the literal claim.
- Expected behavior: Completed-stage validation requires a proof claim-to-code matrix when critical proof uses semantic capability labels, resolves cited source artifacts, and applies label-specific source-token checks.
- Disallowed shallow implementation: A completed proof can pass by naming a class `EmbeddingBacked` or claiming Czech support while the cited source only contains lexical matching or English keywords.
- Failing-first test: `CapabilityProof.RejectsFakeCapabilityClaims` in `bundle://proof/SB01/transcripts/failing-first.txt`.
- Passing test: `CapabilityProof.ValidatesSourceBackedCapabilityClaims` in `bundle://proof/SB01/transcripts/passing.txt`.
- Changed source files: `repo://codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py` with hash `E05FAC8476996CAD28EF8071252F9263E0E5439ED6019ABC2AFFAC868DB6172A`.
- Production assertions: `repo://codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py` defines `PROOF_CLAIM_TO_CODE_MATRIX_HEADING`, `CAPABILITY_SOURCE_REQUIREMENTS`, `validate_proof_claim_to_code_matrix`, and `validate_capability_source_requirements`.
- Red-team negative case: `bundle://proof/SB01/transcripts/failing-first.txt` proves English-only Czech claims and lexical-only embedding claims fail completed validation.
- Downstream dependency check: SB02 and SB03 may proceed only after active skill synchronization and prepared validation pass with the updated validator.

