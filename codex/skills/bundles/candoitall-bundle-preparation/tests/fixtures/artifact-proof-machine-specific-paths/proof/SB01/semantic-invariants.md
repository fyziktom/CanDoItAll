# SB01 Semantic Invariants

## Invariant PROOF-PORTABILITY-01

- Invariant ID: `PROOF-PORTABILITY-01`
- Source raw note: Critical closure must include portable artifact-backed proof.
- Expected behavior: Completed-stage validation accepts a completed critical subbundle only when its manifest, transcript, source assertion, and invariant contract can be resolved through portable bundle or repo references.
- Disallowed shallow implementation: Accepting completed table rows or prose-only semantic labels without checking the cited artifacts.
- Failing-first test: `ArtifactProof.ValidatesCompleteFixture` in `proof/SB01/transcripts/failing-first.txt`.
- Passing test: `ArtifactProof.ValidatesCompleteFixture` in `proof/SB01/transcripts/passing.txt`.
- Changed source files: `C:/repositories/CanDoItAll/codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py` with hash `079561F5460E68FAE447ECA8CD1D5072EEDB7CCD9A7FF09035131EAA92012D9A`.
- Production assertions: The validator checks manifest paths, transcript command and exit fields, changed-file hashes, cited test names, and semantic invariant contracts.
- Red-team negative case: The shallow fixture lacks artifact-backed proof and must fail completed-stage validation.
- Downstream dependency check: A downstream final closure can trust this fixture only when the invariant id appears in a cited transcript.
