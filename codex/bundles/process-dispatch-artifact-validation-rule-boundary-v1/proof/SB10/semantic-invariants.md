# SB10 Semantic Invariants

- Invariant ID: `SB10-INV-001`
- Source raw note: "No change in acceptance/rejection behavior."
- Expected behavior: Placeholder request classification, browser-proof text signals, warning-free validation, and zero-test rejection move to helper rules without changing completion status or reason behavior.
- Disallowed shallow implementation: Moving helper method names without preserving zero-test, Czech no-test, warning, browser-proof, and placeholder rejection coverage.
- Failing-first test: `bundle://proof/SB10/transcripts/focused-unit-architecture-tests.txt`
- Passing test: `bundle://proof/SB10/transcripts/focused-unit-architecture-tests-rerun.txt` and `bundle://proof/SB10/transcripts/focused-quality-placeholder-integration-tests.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactQualityValidationRules.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs`, `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- Production assertions: `bundle://proof/SB10/source-assertions/quality-placeholder-rule-extraction.md`
- Red-team negative case: Source scans prove the helper has no file, storage, DbContext, nested dispatcher expectation, Core/driver, TODO, NotImplemented, or return-default tokens.
- Downstream dependency check: SB11 may start because architecture and quality/placeholder behavior tests passed.

- Raw note owned: Extract placeholder, build/test/browser proof, zero-test, and warning-free rules with parity proof.
- Shipped behavior: Quality and placeholder validation remain equivalent; 7 focused integration tests passed.
- Source proof: `bundle://proof/SB10/source-assertions/quality-placeholder-rule-extraction.md`
- Test proof: `bundle://proof/SB10/transcripts/focused-quality-placeholder-integration-tests.txt`
- Shallow-pass trap: Warning and zero-test helpers exist but completion status no longer blocks.
- Adversarial negative proof: `bundle://proof/SB10/transcripts/quality-rule-source-scans.txt`
- Semantic positive proof: `bundle://proof/SB10/transcripts/focused-quality-placeholder-integration-tests.txt`
- Anti-stub audit: `bundle://proof/SB10/transcripts/quality-rule-source-scans.txt`
