# SB036 Semantic Invariants

## SB036-BUILD-001
- Final solution build must pass with 0 warnings and 0 errors.

## SB036-TEST-001
- Full unit tests and focused process integration matrix must pass.

## SB036-CORE-001
- Core must remain dependency-clean and free of side-effect tokens.

## SB036-DRIVER-001
- Production source must remain free of process-driver runtime APIs.

## SB036-UI-001
- This bundle must not change UI or media files.

## SB036-STUB-001
- Changed source, test, and non-proof documentation lines must not add TODO, stub, placeholder, fake, dummy, or NotImplemented markers.

## Proof Mapping
- Failing-first proof: `bundle://proof/SB036/transcripts/failing-first-broad-smoke-gap.txt`.
- Passing build proof: `bundle://proof/SB034/transcripts/final-solution-build.txt`.
- Passing unit proof: `bundle://proof/SB034/transcripts/full-unit-tests.txt`.
- Passing integration proof: `bundle://proof/SB035/transcripts/focused-integration-matrix.txt`.
- Source and scan proof: `bundle://proof/SB036/transcripts/source-assertions.txt`, `bundle://proof/SB036/transcripts/final-forbidden-core-source-scan.txt`, `bundle://proof/SB036/transcripts/final-core-project-reference-scan.txt`, `bundle://proof/SB036/transcripts/final-production-driver-token-scan.txt`, `bundle://proof/SB036/transcripts/final-ui-media-drift-scan.txt`, `bundle://proof/SB036/transcripts/final-anti-stub-audit.txt`.
