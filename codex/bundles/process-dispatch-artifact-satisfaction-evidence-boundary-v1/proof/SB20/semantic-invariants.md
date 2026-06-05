# SB20 Semantic Invariants

- Invariant ID: $sb-INV-001
- Source raw note: Continue smaller dispatcher isolation steps while preserving all original functionality and deferring Process Core.
- Expected behavior: response text and external-target parity remains protected by module-local helper boundaries and focused regression proof.
- Disallowed shallow implementation: helper names without dispatcher call sites, changed required-artifact branch ordering, moved file/storage/DbContext side effects, or production driver/Core API drift.
- Failing-first test: N/A - process refactor with no intended behavior change; negative regression cases in bundle://proof/shared/transcripts/integration-artifact-contract.txt remain the proof path.
- Passing test: bundle://proof/shared/transcripts/unit-boundary-test.txt, bundle://proof/shared/transcripts/integration-artifact-contract.txt, bundle://proof/shared/transcripts/integration-recovery-routing.txt, and bundle://proof/shared/transcripts/solution-build.txt.
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs and helper files listed in bundle://proof/shared/changed-file-hashes.md.
- Production assertions: dispatcher source calls ProcessArtifactSatisfactionSnapshotBuilder.From, ProcessRequiredArtifactAutoSatisfactionRules.CanAutoSatisfyRequiredArtifact, ProcessQualityValidationEvidenceAggregator.ResolveEvidenceTexts, and ProcessIncompleteImplementationSignalRules.ResolveIncompleteImplementationSummary as asserted by bundle://proof/shared/transcripts/source-assertions.txt.
- Red-team negative case: no-core/no-driver, anti-stub, and no prohibited viewport scans passed in bundle://proof/shared/transcripts/no-core-no-driver-scan.txt, bundle://proof/shared/transcripts/anti-stub-scan.txt, and bundle://proof/shared/transcripts/no-prohibited-viewport-proof-scan.txt.
- Downstream dependency check: downstream gates may depend on this invariant because SB20 is marked completed and the source assertions include $sb-INV-001.