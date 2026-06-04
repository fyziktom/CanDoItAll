# SB09 Semantic Invariants

- Invariant ID: `SB09-INV-001`
- Source raw note: "Keep expected/discovered projection modes unchanged."
- Expected behavior: Provider-native browser path/tool classification and screenshot/visual scoring move to a helper without changing expected-artifact matching or required artifact satisfaction.
- Disallowed shallow implementation: Moving only method names while leaving scoring signals in the dispatcher, or changing expected/discovered projection modes.
- Passing test: `bundle://proof/SB09/transcripts/focused-unit-architecture-tests.txt` and `bundle://proof/SB09/transcripts/focused-provider-native-visual-integration-tests.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProviderNativeVisualValidationRules.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs`, `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- Production assertions: `bundle://proof/SB09/source-assertions/provider-native-visual-rule-extraction.md`
- Red-team negative case: Source scans prove the helper has no file, storage, DbContext, nested dispatcher expectation, Core/driver, TODO, NotImplemented, or return-default tokens.
- Downstream dependency check: SB10 may start because architecture and provider-native browser parity tests passed.

- Raw note owned: Extract provider-native visual scoring and screenshot/visual signal rules.
- Shipped behavior: Provider-native browser matching remains equivalent; 12 focused integration tests passed.
- Source proof: `bundle://proof/SB09/source-assertions/provider-native-visual-rule-extraction.md`
- Test proof: `bundle://proof/SB09/transcripts/focused-provider-native-visual-integration-tests.txt`
- Shallow-pass trap: Browser path helpers moved but screenshot expectation scoring drifts.
- Adversarial negative proof: `bundle://proof/SB09/transcripts/provider-native-visual-rule-source-scans.txt`
- Semantic positive proof: `bundle://proof/SB09/transcripts/focused-provider-native-visual-integration-tests.txt`
- Anti-stub audit: `bundle://proof/SB09/transcripts/provider-native-visual-rule-source-scans.txt`
