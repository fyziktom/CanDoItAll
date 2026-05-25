# SB02 Semantic Invariants

- Invariant ID: `WEB-SB02-001`
- Source raw note: `REQ-PROC-001`.
- Expected behavior: Processes workspace initial render loads only the selected process/editor data and defers hidden runtime, party, workflow-option, analytics, and improvement data until the user opens the dependent tab or dialog.
- Disallowed shallow implementation: Delaying UI rendering while still calling the hidden-section services during initial load, or preloading everything behind a cache that preserves the same startup cost.
- Failing-first test: N/A process because the reported failure was an observed latency regression; `bundle://proof/SB02/transcripts/negative-probe.md` guards against the old eager-call sequence returning.
- Passing test: `Process_workspace_defers_hidden_runtime_and_analytics_data_until_tabs_need_it` proves hidden data is unloaded on initial render, workflow options load when Steps needs them, runtime options load when Runs needs them, and analytics load when Analytics is selected.
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Loading.cs`, `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor.cs`, and `repo://tests/CanDoItAll.Tests.Components/ProcessWorkspaceTests.cs`.
- Production assertions: The visible definition form remains usable, manager-agent options still load because that form needs them, and hidden tabs call explicit ensure methods only when selected.
- Red-team negative case: The old contiguous eager-call block for executor, workflow, party, analytics, and improvements is absent from `LoadWorkspaceAsync`.
- Downstream dependency check: SB05 targeted component tests and web startup include the Processes changes with no startup failure.
