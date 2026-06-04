# SB06 Proof Manifest

- Subbundle: `SB06`
- Status: `Completed`
- Owned requirements: `RQ-007`
- Raw notes: Provider boundary checkpoint must prevent MAF or Tooling from becoming a new product-tool monolith after project/image migrations.
- Semantic invariant contract: `bundle://proof/SB06/semantic-invariants.md`

## Changed File Hashes

- Representative SHA-256: a49531646b86a3107979bf6a594fa7ef005e2bc0473cb79facf8acd2e0a168f7  repo://tests/CanDoItAll.Tests.Unit/AgentRuntimeToolProviderArchitectureTests.cs
- Hash manifest: `bundle://proof/SB06/source-assertions/changed-file-hashes.txt`

## Command Transcripts

- Static architecture tests: `bundle://proof/SB06/transcripts/static-architecture-tests.txt`
- Forbidden namespace scans: `bundle://proof/SB06/transcripts/forbidden-namespace-scans.txt`
- Provider composition size/responsibility review: `bundle://proof/SB06/transcripts/provider-composition-size-review.txt`
- Tooling project build: `bundle://proof/SB06/transcripts/tooling-build.txt`
- MAF project build: `bundle://proof/SB06/transcripts/maf-build.txt`
- Anti-stub audit: `bundle://proof/SB06/transcripts/anti-stub-audit.txt`
- Adversarial MAF product reference scan: `bundle://proof/SB06/transcripts/adversarial-maf-product-reference-scan.txt`

## Failing-First And Passing Proof

- Adversarial negative proof: `bundle://proof/SB06/transcripts/adversarial-maf-product-reference-scan.txt` records a non-zero scan for direct MAF product-module references.
- Passing: `bundle://proof/SB06/transcripts/static-architecture-tests.txt`, `bundle://proof/SB06/transcripts/forbidden-namespace-scans.txt`, `bundle://proof/SB06/transcripts/tooling-build.txt`, and `bundle://proof/SB06/transcripts/maf-build.txt`.

## Source Assertions

- Source assertions and decision log: `bundle://proof/SB06/source-assertions/provider-boundary-source-assertions.txt`
- Changed-file hashes: `bundle://proof/SB06/source-assertions/changed-file-hashes.txt`

## Anti-Stub Audit

- Anti-stub audit transcript: `bundle://proof/SB06/transcripts/anti-stub-audit.txt`

## Browser And Host Proof

- Browser proof: N/A; SB06 changes architecture tests and proof only.
- Host proof: N/A; no desktop or process-launch behavior changed.

## Downstream Smoke Proof

- `bundle://proof/SB06/transcripts/provider-composition-size-review.txt` records that Process provider internal split remains deferred to SB07, while MAF/Tooling boundaries are clean enough for the next phase.
