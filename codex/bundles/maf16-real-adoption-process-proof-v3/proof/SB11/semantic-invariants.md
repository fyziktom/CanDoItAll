# SB11 Semantic Invariants

- Invariant ID: `SB11-INV-001`
- Source raw note: `repo://codex/bundles/maf16-real-adoption-process-proof-v3/requirements/01-normalized-requirements.md` RQ08.
- Expected behavior: A required narrative artifact with a managed storage path must report `ContentUnavailable` and remain unsatisfied when content cannot be loaded.
- Disallowed shallow implementation: Treating a recorded artifact row as satisfied without reading required stored content.
- Failing-first test: `bundle://proof/SB11/transcripts/failing-first.txt`
- Passing test: `bundle://proof/SB11/transcripts/passing.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.CompletionArtifactValidator.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`, `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs`, and `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`.
- Production assertions: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` computes when stored content is required, and `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.CompletionArtifactValidator.cs` applies that policy.
- Red-team negative case: `bundle://proof/SB11/transcripts/failing-first.txt` proves the old behavior returned `Satisfied` for missing stored content.
- Downstream dependency check: SB13 projects the same `ContentUnavailable` status into operator obligations and health.
