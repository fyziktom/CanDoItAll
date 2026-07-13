# SB03 Proof Manifest

- Invariant ID: `SB03-INV-branch-enforcement`
- Changed file hash: `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessRequiredToolReceiptGate.cs` sha256 `da3a5c49e10571f0cb1ccf175a09b03f8b23c41200859303f3cb20350fa03bfe`
- Semantic invariant contract: `bundle://proof/SB03/semantic-invariants.md`
- Failing-first transcript: `bundle://proof/shared/transcripts/failing-first.txt`
- Adversarial negative proof transcript: `bundle://proof/shared/transcripts/failing-first.txt`
- Semantic positive proof passing transcript: `bundle://proof/shared/transcripts/passing-tests.txt`
- Anti-stub audit transcript: `bundle://proof/shared/transcripts/anti-stub-audit.txt`
- Test name: `QualityAccepted_with_full_browser_receipts_accepts_criterion_by_criterion_proof`
- Source proof: `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessRequiredToolReceiptGate.cs`
- Result: Branch-aware receipt enforcement skips acceptance-proof obligations on repair branches and deduplicates product-covered obligations.
