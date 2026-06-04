# SB13 Semantic Invariants

- Invariant ID: `SB13-INV-001`
- Source raw note: "Browser proof remains N/A unless UI changed."
- Expected behavior: Runtime smoke proves artifact validation/projection behavior and build health without creating prohibited viewport proof artifacts.
- Disallowed shallow implementation: Treating Gate C as enough without running the smoke projection/contract validation slice and large-screen policy scan.
- Passing test: `bundle://proof/SB13/transcripts/runtime-smoke-unit-architecture-tests.txt`, `bundle://proof/SB13/transcripts/runtime-smoke-validation-projection-integration-tests.txt`, and `bundle://proof/SB13/transcripts/runtime-smoke-solution-build.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs`, `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- Production assertions: `bundle://proof/SB13/source-assertions/runtime-smoke-and-policy.md`
- Red-team negative case: No prohibited viewport proof paths, Process Core, driver-pack references, or helper side effects were introduced.
- Downstream dependency check: SB14 may start because runtime smoke, build, and policy scans passed.

- Raw note owned: Run runtime smoke and large-screen policy check.
- Shipped behavior: Artifact validation/projection smoke remains green; 29 architecture tests, 26 integration tests, and build passed.
- Source proof: `bundle://proof/SB13/source-assertions/runtime-smoke-and-policy.md`
- Test proof: `bundle://proof/SB13/transcripts/runtime-smoke-validation-projection-integration-tests.txt`
- Shallow-pass trap: No UI changed but prohibited viewport proof artifacts are still created.
- Adversarial negative proof: `bundle://proof/SB13/transcripts/runtime-smoke-no-prohibited-viewport-proof-scan.txt`
- Semantic positive proof: `bundle://proof/SB13/transcripts/runtime-smoke-unit-architecture-tests.txt`, `bundle://proof/SB13/transcripts/runtime-smoke-validation-projection-integration-tests.txt`, and `bundle://proof/SB13/transcripts/runtime-smoke-solution-build.txt`
- Anti-stub audit: `bundle://proof/SB13/transcripts/runtime-smoke-helper-side-effect-scan.txt`
