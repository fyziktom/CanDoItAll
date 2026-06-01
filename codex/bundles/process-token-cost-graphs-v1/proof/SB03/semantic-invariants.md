# SB03 Semantic Invariants

## SB03-I01 Process Graph Tab Does Not Eager Load All Runs

- Raw note: "Those are lots of loading of the data, so it must load them only when that tab is selected. For all process run there might be button 'Show graphs of all runs of process'..."
- Expected behavior: selecting the process `Graphs` tab shows range controls and the explicit load button but does not call the historical graph load until the button is clicked.
- Disallowed shallow implementation: call the all-runs query as soon as the tab is selected.
- Failing-first proof: the component test asserts `processGraphsLoadRequested` remains false and `processGraphsSnapshot` remains null after tab activation.
- Passing proof: `bundle://proof/SB03/transcripts/component-lazy-graph-tests.txt`.
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceGraphsTab.razor`, `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Graphs.cs`.
- Production assertions: `bundle://proof/SB03/transcripts/source-assertions.txt`.
- Red-team negative case: accidental tab click without button click leaves graph snapshot null.
- Downstream dependency check: prevents accidental large history queries in process workspace.

## SB03-I02 Process All-Runs Graphs Are Explicitly Scoped And Range-Bounded

- Raw note: "button 'Show graphs of all runs of process' and with preselected option like last 1 month (with other options like 1day, 1 week, 1 month, 3 months, 1 year, all)."
- Expected behavior: default process graph range is one month; supported ranges are one day, one week, one month, three months, one year, and all; explicit load uses `ProcessDefinitionId`.
- Disallowed shallow implementation: button loads global all-run history or silently uses a stale prior range.
- Failing-first proof: source assertion proves range options and scoped query call; component test proves explicit button load.
- Passing proof: `bundle://proof/SB03/transcripts/component-lazy-graph-tests.txt` and `bundle://proof/SB03/transcripts/source-assertions.txt`.
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor.cs`, `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Graphs.cs`, `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceGraphsTab.razor`.
- Production assertions: `bundle://proof/SB03/transcripts/source-assertions.txt`.
- Red-team negative case: graph state resets when the selected range changes, requiring an explicit load for the new range.
- Downstream dependency check: observation service bounds history windows except explicit `All`.

## SB03-I03 Selected Run Graphs Load Only For The Selected Run

- Raw note: "in specic selected process run we need also own tab for graphs for that specific process run only."
- Expected behavior: selected-run graph data is not loaded until the run `Graphs` tab is selected, and the query passes `ProcessRunId`.
- Disallowed shallow implementation: reuse process all-runs data in the run tab.
- Failing-first proof: component test asserts selected-run graph snapshot is null before tab selection and loaded for the selected run after graph tab activation.
- Passing proof: `bundle://proof/SB03/transcripts/component-lazy-graph-tests.txt`.
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunsTab.razor`, `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Graphs.cs`, `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.RunsPresenter.cs`.
- Production assertions: `bundle://proof/SB03/transcripts/source-assertions.txt`.
- Red-team negative case: changing selected run resets selected-run graph state so stale run data is not displayed as the new run.
- Downstream dependency check: run-level graph queries use SB02 run-scoped observation data.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Process graph load request | `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceGraphsTab.razor` | `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Graphs.cs` | user clicks explicit all-runs load button | `bundle://proof/SB03/transcripts/component-lazy-graph-tests.txt` |
| Process graph range | `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor.cs` | `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Graphs.cs` | range defaults to one month and resets load state on change | `bundle://proof/SB03/transcripts/component-lazy-graph-tests.txt` |
| Selected-run graph activation | `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunsTab.razor` | `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Graphs.cs` | nested run tab activation calls run-scoped graph load | `bundle://proof/SB03/transcripts/component-lazy-graph-tests.txt` |
