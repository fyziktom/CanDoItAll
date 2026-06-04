# SB02 Inventory Closure Assertions

- `bundle://inventories/02-agentframework-usage-in-processes.md` replaces the initial inventory task stub with a source-backed table of Process module AgentFramework usages.
- `bundle://proof/SB02/transcripts/direct-execution-call-scan.txt` identifies dispatcher direct calls to `ExecuteRunAsync`, `GetExecutionRunDetailAsync`, and `ListExecutionRunsAsync`.
- Dispatcher execution/readback calls that should move behind the process execution facade are concentrated in `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs`, `Concurrency.cs`, `Grounding.cs`, `Costing.cs`, `CompletionArtifactRecovery.cs`, and `Dispatch.cs`.
- Non-dispatcher direct calls in UI, observation, manager chat, and recovery worker files are documented as out of scope for SB06 unless a later gate explicitly reopens that boundary.
- No production code changed in SB02.
