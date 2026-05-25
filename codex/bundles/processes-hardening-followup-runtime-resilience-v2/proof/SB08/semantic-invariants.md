# SB08 Semantic Invariants

## SB08-INV-001

- Invariant ID: `SB08-INV-001`
- Source raw note: N004, N005, N007
- Expected behavior: repeated invalid attempts with the same semantic failure are compressed, wrong-root writes do not count as progress, and active executions are not finalized prematurely.
- Disallowed shallow implementation: counting any write receipt as progress, finalizing an observed running execution, or fingerprinting only response text.
- Failing-first test: `bundle://proof/SB08/transcripts/failing-first.txt`
- Passing test: `bundle://proof/SB08/transcripts/passing.txt`
- Changed source files and hashes: `bundle://proof/SB08/transcripts/changed-file-hashes.txt`
- Production assertions: `bundle://proof/SB08/transcripts/source-assertions.txt`
- Red-team negative case: repeated wrong-root product writes do not create another retry.
- Downstream dependency check: SB10 includes software and non-software retry/adoption red-team coverage.

## Production Behavior Artifact Matrix

| Artifact/signal | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| `NoProgressRetryCompressed` | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RecoveryPackets.cs` | `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeEventTypes.cs` | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs` | `bundle://proof/SB08/transcripts/failing-first.txt` |
| Active execution `InProgress` outcome | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs` | Dispatch finalizer caller in `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs` | `bundle://proof/SB08/transcripts/source-assertions.txt` | `bundle://proof/SB08/transcripts/failing-first.txt` |
