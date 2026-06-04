# SB07 Semantic Invariants

- Invariant ID: `SB07-INV-001`
- Source raw note: "Keep all original functions and prove no behavior was dropped."
- Expected behavior: Title, slug, token, text-content, and narrative-purpose matching rules move to a pure helper without changing matching order or allowing product source files to satisfy narrative deliverables.
- Disallowed shallow implementation: Moving token helpers while leaving noise-token state duplicated in the dispatcher, or passing only structure tests without slug/content/narrative matcher tests.
- Failing-first test: `bundle://proof/SB07/transcripts/focused-unit-architecture-tests.txt`
- Passing test: `bundle://proof/SB07/transcripts/focused-unit-architecture-tests-rerun.txt` and `bundle://proof/SB07/transcripts/focused-title-text-integration-tests.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactTextMatchRules.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs`, `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- Production assertions: `bundle://proof/SB07/source-assertions/title-text-rule-extraction.md`
- Red-team negative case: Source scan proves the text helper has no file, storage, DbContext, nested dispatcher expectation, Core/driver, TODO, NotImplemented, or return-default tokens; title/text integration tests include unrelated and product-source rejection cases.
- Downstream dependency check: SB08 may start because path and title/text matcher parity slices passed after extraction.

- Raw note owned: Extract title/slug/text-content matching with parity proof.
- Shipped behavior: Matcher behavior remains equivalent; 16 focused integration tests passed.
- Source proof: `bundle://proof/SB07/source-assertions/title-text-rule-extraction.md`
- Test proof: `bundle://proof/SB07/transcripts/focused-title-text-integration-tests.txt`
- Shallow-pass trap: Token helper exists but matching order or noise-token semantics drift.
- Adversarial negative proof: `bundle://proof/SB07/transcripts/focused-title-text-integration-tests.txt`
- Semantic positive proof: `bundle://proof/SB07/transcripts/focused-title-text-integration-tests.txt`
- Anti-stub audit: `bundle://proof/SB07/transcripts/text-rule-source-scans.txt`
