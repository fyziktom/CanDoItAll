# SB01 Semantic Invariants

- Invariant ID: SB01-INV-001
- Source raw note: F01 claimed `ProcessRuntimeViewModels.cs` referenced `ProcessStepRecoveryOption.None` while `ProcessDefinitionEnums.cs` did not define `None`.
- Expected behavior: `ProcessStepRecoveryOption.None` exists as the zero-valued non-action option, and runtime health read models default recovery actions to that value.
- Disallowed shallow implementation: Adding a compile-only reference, editing bundle prose, or hardcoding tests without verifying the runtime read-model defaults.
- Failing-first test: N/A - no production behavior change was needed; process validation found the suspected compile breaker already absent before implementation.
- Passing test: `bundle://proof/SB01/transcripts/passing.txt`
- Changed source files: `repo://tests/CanDoItAll.Tests.Integration/ProcessStepRecoveryOptionContractTests.cs`
- Production assertions: `repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionEnums.cs` defines `ProcessStepRecoveryOption.None`, and `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs` defaults health recovery actions to `ProcessStepRecoveryOption.None`.
- Red-team negative case: A future removal or renumbering of `ProcessStepRecoveryOption.None` fails `ProcessStepRecoveryOptionContractTests`.
- Downstream dependency check: SB02 can rely on a stable recovery option default for API/read-model parity checks.
