# SB14 Semantic Invariants

- Invariant ID: `SB14-INV-001`
- Source raw note: "Final red-team review and next safe dispatcher isolation cutline."
- Expected behavior: Final closure proves artifact validation behavior, expectation matching, required artifact satisfaction, and projection smoke still pass while documenting that the next cutline remains module-local.
- Disallowed shallow implementation: Marking the bundle complete without fresh final tests, source scans, build proof, and a documented next cutline.
- Failing-first test: Final closure would fail if final architecture smoke, integration smoke, build, no-driver scans, helper dependency scans, anti-stub scan, viewport proof scan, or next-cutline documentation were missing or failing.
- Passing test: `bundle://proof/SB14/transcripts/final-unit-architecture-tests.txt`, `bundle://proof/SB14/transcripts/final-validation-projection-integration-tests.txt`, and `bundle://proof/SB14/transcripts/final-solution-build.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.cs`, and `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- Production assertions: `bundle://proof/SB14/source-assertions/final-red-team-and-cutline.md`
- Red-team negative case: No Process Core, driver packs, helper side effects, product-module dependencies, stubs, or prohibited viewport proof artifacts were introduced.
- Downstream dependency check: Final closure may proceed because all subbundle gates are passed.

- Raw note owned: Final red-team and next dispatcher cutline.
- Shipped behavior: Artifact validation rules are isolated into process-module-local helpers with validation/projection behavior preserved.
- Source proof: `bundle://proof/SB14/source-assertions/final-red-team-and-cutline.md`
- Test proof: `bundle://proof/SB14/transcripts/final-unit-architecture-tests.txt` and `bundle://proof/SB14/transcripts/final-validation-projection-integration-tests.txt`
- Shallow-pass trap: Creating driver-pack names or Core surfaces while only proving current tests still pass.
- Adversarial negative proof: `bundle://proof/SB14/transcripts/final-no-core-no-driver-scan.txt`, `bundle://proof/SB14/transcripts/final-rule-helper-side-effect-scan.txt`, `bundle://proof/SB14/transcripts/final-helper-maf-tooling-product-dependency-scan.txt`, and `bundle://proof/SB14/transcripts/final-no-prohibited-viewport-proof-scan.txt`
- Semantic positive proof: `bundle://proof/SB14/transcripts/final-unit-architecture-tests.txt`, `bundle://proof/SB14/transcripts/final-validation-projection-integration-tests.txt`, and `bundle://proof/SB14/transcripts/final-solution-build.txt`
- Anti-stub audit: `bundle://proof/SB14/transcripts/final-anti-stub-scan.txt`
