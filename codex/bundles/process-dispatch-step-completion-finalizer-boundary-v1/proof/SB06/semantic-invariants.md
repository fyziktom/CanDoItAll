# SB06 Artifact Content Reader Extraction Semantic Invariants

- Invariant ID: SB06-INV-001
- Source raw note: Make the step-completion finalizer smaller while preserving original behavior.
- Expected behavior: Type snapshots and content reader implementations compile in module-local partial files and keep workspace/storage read behavior intact.
- Disallowed shallow implementation: Reader extraction that loses storage-backed reads, creates a public driver API, or leaves compile-only placeholder members.
- Failing-first test: bundle://proof/SB06/transcripts/type-reader-split-build.txt
- Passing test: bundle://proof/SB06/transcripts/type-reader-split-build-passing.txt
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.Types.cs; repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.ArtifactContentReaders.cs
- Production assertions: Processes-module behavior is preserved; no Process Core project, driver pack API, or UI file change is introduced.
- Red-team negative case: bundle://proof/SB06/transcripts/anti-stub-audit.txt rejects placeholder exception/TODO implementation markers and boundary drift for this scope.
- Downstream dependency check: Execution report gate row and final red-team scan confirm downstream SBs can proceed or close without expanding the process-driver boundary.
