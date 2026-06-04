# SB08 Semantic Invariants

- Invariant ID: `SB08-INV-001`
- Source raw note: "Gate B must pass before visual/quality rules."
- Expected behavior: Refactored matcher, path, and text helper boundaries preserve artifact validation, expected-artifact matching, required artifact satisfaction, and MAF/Tooling product-module neutrality.
- Disallowed shallow implementation: Declaring helper extraction complete without running the combined matcher/path/title parity slice, or accepting proof artifacts from prohibited viewport classes.
- Failing-first test: Gate B would fail if matcher/path/title parity tests regressed, helper side-effect scans found moved file/storage behavior, or prohibited viewport proof paths were created.
- Passing test: `bundle://proof/SB08/transcripts/gate-b-unit-architecture-tests.txt` and `bundle://proof/SB08/transcripts/gate-b-matcher-parity-integration-tests.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactValidationSnapshot.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactValidationSnapshotBuilder.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactPathValidationRules.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactTextMatchRules.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs`, `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- Production assertions: `bundle://proof/SB08/source-assertions/gate-b-matcher-parity.md`
- Red-team negative case: Source scans prove no Process Core, driver-pack references, helper side effects, stubs, or prohibited viewport proof paths were introduced.
- Downstream dependency check: SB09 may start because Gate B architecture and matcher parity tests passed.

- Raw note owned: Run Gate B parity and line-count review before visual/quality rule extraction.
- Shipped behavior: Matcher/path/title/text validation remains equivalent; 5 architecture tests and 29 focused integration tests passed.
- Source proof: `bundle://proof/SB08/source-assertions/gate-b-matcher-parity.md`
- Test proof: `bundle://proof/SB08/transcripts/gate-b-matcher-parity-integration-tests.txt`
- Shallow-pass trap: Line count reduction exists but matcher behavior is not proven.
- Adversarial negative proof: `bundle://proof/SB08/transcripts/gate-b-no-core-no-driver-scan.txt`, `bundle://proof/SB08/transcripts/gate-b-helper-side-effect-scan.txt`, and `bundle://proof/SB08/transcripts/gate-b-no-prohibited-viewport-proof-scan.txt`
- Semantic positive proof: `bundle://proof/SB08/transcripts/gate-b-unit-architecture-tests.txt` and `bundle://proof/SB08/transcripts/gate-b-matcher-parity-integration-tests.txt`
- Anti-stub audit: `bundle://proof/SB08/transcripts/gate-b-helper-side-effect-scan.txt`
