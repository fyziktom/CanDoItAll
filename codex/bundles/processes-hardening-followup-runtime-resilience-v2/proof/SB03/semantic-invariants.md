# SB03 Semantic Invariants

## SB03-INV-001

- Invariant ID: `SB03-INV-001`
- Source raw note: N002, N006, N007
- Expected behavior: recovered artifacts identify the recovery execution run and the original recovered-for execution run, and finalizer validation accepts that lineage explicitly.
- Disallowed shallow implementation: accepting stale artifacts by title, string matching without recovered-for identity, or bypassing the process-owned finalizer.
- Failing-first test: `bundle://proof/SB03/transcripts/failing-first.txt`
- Passing test: `bundle://proof/SB03/transcripts/passing.txt`
- Changed source files and hashes: `bundle://proof/SB03/transcripts/changed-file-hashes.txt`
- Production assertions: `bundle://proof/SB03/transcripts/source-assertions.txt`
- Red-team negative case: a stale workspace artifact from the wrong execution run remains invalid.
- Downstream dependency check: SB07 storage-backed validation reuses the same finalizer path.

## Production Behavior Artifact Matrix

| Artifact/signal | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| `manager-recovery-artifact` lineage key | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs` | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.CompletionArtifactRecovery.cs` | `bundle://proof/SB03/transcripts/failing-first.txt` |
