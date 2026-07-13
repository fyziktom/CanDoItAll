# SB13 Proof Manifest

- Status: `Completed`
- Owned requirement: R13
- Semantic invariant contract: `bundle://proof/SB13/semantic-invariants.md`

## Required Artifacts

- `bundle://proof/SB13/changed-file-hashes.txt`
- `bundle://proof/SB13/transcripts/failing-first.txt`
- `bundle://proof/SB13/transcripts/passing-tests.txt`
- `bundle://proof/SB13/transcripts/source-assertions.txt`
- `bundle://proof/SB13/transcripts/anti-stub-audit.txt`
- `bundle://proof/SB13/transcripts/codeanalytics.txt`

## Production Behavior Artifact Matrix

No new persisted production signal is planned; policy contributors affect existing completion decisions.

## Closure Evidence

- Generic receipt handling depends on `IProcessToolReceiptPolicyContribution` through `ProcessToolReceiptPolicyCatalog`.
- Generic subprocess contract resolution depends on `IProcessSubprocessContractProvider`.
- .NET/software-delivery implementations are isolated under `RuntimeIntegration/Drivers/DotNet`.
- Ambiguous tool ownership fails explicitly; unrelated tools and generic-only subprocess resolution have negative tests.
- The strengthened forbidden-domain scan is green.
