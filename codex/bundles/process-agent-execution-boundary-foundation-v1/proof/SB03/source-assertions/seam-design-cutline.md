# SB03 Seam Design Cutline Assertions

- `bundle://architecture/02-execution-boundary-staging.md` now defines the required `IProcessAutomationExecutionClient` methods for execution start, detail readback, execution-run listing, and provider/agent recovery calls used by the dispatcher.
- `bundle://inventories/02-agentframework-usage-in-processes.md` is the source-backed input for the movement cutline.
- SB06 should move dispatcher calls to `ExecuteRunAsync`, `GetExecutionRunDetailAsync`, `ListExecutionRunsAsync`, and dispatcher provider-recovery workspace operations behind the facade.
- SB06 should not move manager chat, observation services, UI run-detail loaders, recovery worker calls, finalizer parsing, receipt interpretation, EF entities, Razor models, process driver packs, or public process tool names.
- `bundle://proof/SB03/transcripts/no-production-movement-diff.txt` records that SB03 did not change `src` or `tests`.
