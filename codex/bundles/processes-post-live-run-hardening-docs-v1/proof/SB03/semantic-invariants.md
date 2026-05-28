# SB03 Semantic Invariants

## Invariants

- Invariant ID: SB03-INV-001
- Source raw note: RN03 - Align artifact validation, read-model, health, recovery/API/UI, and invalid artifact semantics.
- Expected behavior: Finalizer validation status, operator read-model satisfaction status, operator read-model validation status, health-audit unsatisfied-required detection, and run-detail loader unsatisfied-required counts all flow through one strongly typed projection service.
- Disallowed shallow implementation: Updating only the read-model mapper while leaving a duplicated status set in health or UI code.
- Failing-first test: bundle://proof/SB03/transcripts/sb03-adversarial-duplicate-mapping-removed.txt proves the old local helper definitions are absent; the transcript intentionally exits 1 because `rg` finds no duplicate helpers.
- Passing test: bundle://proof/SB03/transcripts/sb03-projection-service-tests.txt proves every finalizer status and required-artifact satisfaction state maps as expected.
- Regression test: bundle://proof/SB03/transcripts/sb03-read-model-regression-tests.txt proves the existing operator read-model finalizer projection tests still pass.
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessArtifactStatusProjectionService.cs; repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.Support.cs; repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessHealthInvariantAuditor.cs; repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunDetailsLoader.cs; repo://tests/CanDoItAll.Tests.Integration/ProcessArtifactStatusProjectionServiceTests.cs.
- Production assertions: No stringly status mapping was introduced; all projection paths use `ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus`, `ProcessArtifactExpectationSatisfactionStatus`, and `ProcessArtifactExpectationValidationStatus`.
- Red-team negative case: `Missing`, `InvalidFormat`, `InsufficientEvidence`, `StaleOrWrongRun`, `WrongProducerMode`, `PlaceholderOnly`, `ContentUnavailable`, and `ContentHashMismatch` remain unsatisfied for required artifacts while `Expected`, `Satisfied`, `AutoProjected`, and `NotApplicable` do not fail health checks.
- Downstream dependency check: SB04, SB11, SB13, and SB16 can now consume one shared artifact status contract instead of copying status lists.
