# SB01 Proof Manifest

- Invariant ID: `SB01-INV-extracted-gate`
- Changed file hash: `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessCompletionGateEvaluator.cs` sha256 `ff729a6dd9e253d98ce81bf5ee6e6f7e7460cb338ccbe08a597c6a589b23b6c1`
- Semantic invariant contract: `bundle://proof/SB01/semantic-invariants.md`
- Failing-first transcript: `bundle://proof/shared/transcripts/failing-first.txt`
- Adversarial negative proof transcript: `bundle://proof/shared/transcripts/failing-first.txt`
- Semantic positive proof passing transcript: `bundle://proof/shared/transcripts/passing-tests.txt`
- Anti-stub audit transcript: `bundle://proof/shared/transcripts/anti-stub-audit.txt`
- Test name: `ProcessCompletionGateEvaluator_orders_and_deduplicates_issues_without_adapter_runtime`
- Source proof: `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessCompletionGateEvaluator.cs`
- Result: Completion issue aggregation, deduplication, and priority ordering are tested without constructing the old adapter runtime.
