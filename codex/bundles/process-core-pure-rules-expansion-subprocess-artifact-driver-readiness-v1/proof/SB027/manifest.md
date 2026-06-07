# SB027 Proof Manifest
## Summary
- Subbundle: SB027 - Gate I Core docs and scorecard.
- Status: Completed.
- Invariant ID: SB027-INV-001
- Hash reference: repo://codex/bundles/process-core-pure-rules-expansion-subprocess-artifact-driver-readiness-v1/architecture/04-core-extraction-scorecard.md SHA-256 e03b8e4e105e6f2e237d8684e64eda998fd52ed32b9d7d49240f34da2ddd2197
- Semantic invariant contract: bundle://proof/SB027/semantic-invariants.md
- Changed file: repo://codex/bundles/process-core-pure-rules-expansion-subprocess-artifact-driver-readiness-v1/architecture/04-core-extraction-scorecard.md
- Changed file: repo://codex/bundles/process-core-pure-rules-expansion-subprocess-artifact-driver-readiness-v1/architecture/05-core-public-contract-map.md
## Evidence
- Source assertion transcript: bundle://proof/shared/transcripts/source-assertions.txt
- Source proof artifact: bundle://architecture/04-core-extraction-scorecard.md
- Passing transcript: bundle://proof/shared/transcripts/source-assertions.txt
- Failing-first transcript: bundle://proof/SB027/transcripts/failing-first.txt
- Anti-stub audit transcript: bundle://proof/shared/transcripts/anti-stub-scan.txt
- Dependency scan transcript: bundle://proof/shared/transcripts/core-forbidden-scan.txt
- Build transcript: bundle://proof/shared/transcripts/build.txt
- Driver token scan transcript: bundle://proof/shared/transcripts/driver-token-scan.txt
- No UI/media transcript: bundle://proof/shared/transcripts/no-ui-media-drift-scan.txt
## Closure
- Expected behavior: Documentation states the accepted Core surface and the remaining non-Core responsibilities.
- Disallowed shallow implementation: Publishing docs that claim a broader Core extraction than the code and tests support.
- Downstream dependency check: bundle://proof/shared/transcripts/core-forbidden-scan.txt
