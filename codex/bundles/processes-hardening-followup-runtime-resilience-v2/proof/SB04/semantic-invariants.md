# SB04 Semantic Invariants

## SB04-INV-001

- Invariant ID: `SB04-INV-001`
- Source raw note: N002, N004, N007
- Expected behavior: workflow and subprocess outputs are projected with typed process expectation/provenance data before finalizer validation.
- Disallowed shallow implementation: accepting any output by title, projecting stale child artifacts, or letting workflow status bypass process artifact validation.
- Failing-first test: `bundle://proof/SB04/transcripts/failing-first.txt`
- Passing test: `bundle://proof/SB04/transcripts/passing.txt`
- Changed source files and hashes: `bundle://proof/SB04/transcripts/changed-file-hashes.txt`
- Production assertions: `bundle://proof/SB04/transcripts/source-assertions.txt`
- Red-team negative case: subprocess artifact validation requires current child lineage.
- Downstream dependency check: SB05 unblock behavior depends on accurate artifact materialization.

## Production Behavior Artifact Matrix

| Artifact/signal | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Workflow/subprocess artifact projection lineage | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessWorkflowRunCoordinator.cs` | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` | `bundle://proof/SB04/transcripts/failing-first.txt` |
