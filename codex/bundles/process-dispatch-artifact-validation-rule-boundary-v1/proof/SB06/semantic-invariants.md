# SB06 Semantic Invariants

- Invariant ID: `SB06-INV-001`
- Source raw note: "Keep all original functions and prove no behavior was dropped."
- Expected behavior: Pure managed path normalization, explicit expected-path parsing, scoped path comparison, and shallow shared managed path classification move to a process-module helper while dispatcher file-system orchestration remains in place.
- Disallowed shallow implementation: Moving file reads/copies/storage operations into a helper named as a rule class, or changing exact-path/scoped-path matching behavior while extracting.
- Failing-first test: `bundle://proof/SB06/transcripts/focused-unit-architecture-tests.txt`
- Passing test: `bundle://proof/SB06/transcripts/focused-unit-architecture-tests-rerun.txt` and `bundle://proof/SB06/transcripts/focused-path-integration-tests.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactPathValidationRules.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs`, `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- Production assertions: `bundle://proof/SB06/source-assertions/path-rule-extraction.md`
- Red-team negative case: Source scan proves the new path helper has no `File.`, `Directory.`, storage, DbContext, dispatcher nested expectation, Core/driver, TODO, NotImplemented, or return-default tokens.
- Downstream dependency check: SB07 may start because title/text matching still passed through the path-sensitive integration slice.

- Raw note owned: Extract path and managed-artifact rules without moving file effects.
- Shipped behavior: Path/matcher behavior remains equivalent; 16 focused integration tests passed.
- Source proof: `bundle://proof/SB06/source-assertions/path-rule-extraction.md`
- Test proof: `bundle://proof/SB06/transcripts/focused-path-integration-tests.txt`
- Shallow-pass trap: A helper exists but owns I/O or breaks scoped path equivalence.
- Adversarial negative proof: `bundle://proof/SB06/transcripts/path-rule-source-scans.txt`
- Semantic positive proof: `bundle://proof/SB06/transcripts/focused-path-integration-tests.txt`
- Anti-stub audit: `bundle://proof/SB06/transcripts/path-rule-source-scans.txt`
