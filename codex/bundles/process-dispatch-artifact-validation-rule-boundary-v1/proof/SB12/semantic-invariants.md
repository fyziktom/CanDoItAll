# SB12 Semantic Invariants

- Invariant ID: `SB12-INV-001`
- Source raw note: "Gate C must pass before final smoke."
- Expected behavior: All extracted artifact validation helpers preserve artifact validation, expected-artifact matching, required-artifact satisfaction, MAF/Tooling product-module neutrality, and the no-Process-Core/no-driver-pack boundary.
- Disallowed shallow implementation: Running only narrow helper tests without full Gate C regression, full solution build, driver-readiness map update, and no-driver scans.
- Failing-first test: Gate C would fail if architecture or integration regressions appeared, if the full solution build failed, or if scans found driver/Core, side-effect, product dependency, stub, or prohibited viewport proof drift.
- Passing test: `bundle://proof/SB12/transcripts/gate-c-unit-architecture-tests.txt`, `bundle://proof/SB12/transcripts/gate-c-artifact-validation-integration-regression.txt`, and `bundle://proof/SB12/transcripts/gate-c-full-solution-build.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactValidationSnapshot.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactValidationSnapshotBuilder.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactPathValidationRules.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactTextMatchRules.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProviderNativeVisualValidationRules.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactQualityValidationRules.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectStructureRequirementValidationRules.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs`, `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- Production assertions: `bundle://proof/SB12/source-assertions/gate-c-validation-regression.md`
- Red-team negative case: Source scans prove no Process Core, driver-pack references, helper side effects, MAF/Tooling/product dependencies, stubs, or prohibited viewport proof paths were introduced.
- Downstream dependency check: SB13 may start because Gate C tests, build, scans, and driver-readiness review passed.

- Raw note owned: Run validation regression and driver-readiness review before final smoke.
- Shipped behavior: Artifact validation remains equivalent; 8 architecture tests, 46 integration tests, and full solution build passed.
- Source proof: `bundle://proof/SB12/source-assertions/gate-c-validation-regression.md`
- Test proof: `bundle://proof/SB12/transcripts/gate-c-artifact-validation-integration-regression.txt`
- Shallow-pass trap: Helper extraction compiles but no full regression or no-driver proof is run.
- Adversarial negative proof: `bundle://proof/SB12/transcripts/gate-c-no-core-no-driver-scan.txt`, `bundle://proof/SB12/transcripts/gate-c-helper-side-effect-scan.txt`, `bundle://proof/SB12/transcripts/gate-c-helper-maf-tooling-product-dependency-scan.txt`, and `bundle://proof/SB12/transcripts/gate-c-no-prohibited-viewport-proof-scan.txt`
- Semantic positive proof: `bundle://proof/SB12/transcripts/gate-c-unit-architecture-tests.txt`, `bundle://proof/SB12/transcripts/gate-c-artifact-validation-integration-regression.txt`, and `bundle://proof/SB12/transcripts/gate-c-full-solution-build.txt`
- Anti-stub audit: `bundle://proof/SB12/transcripts/gate-c-helper-side-effect-scan.txt`
