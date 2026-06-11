# SB03 Proof Manifest

- Subbundle: SB03
- Status: Completed
- Source references: `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.RuntimeHostReadback.cs`, `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunsRuntimeHostReadbackSection.razor`
- Semantic invariant contract: `bundle://proof/SB03/semantic-invariants.md`
- SHA-256 hash: `c827d3884759a7ad76ac28e4ee8e9a3084588ea833966dc35d754ded9f8784df`
- Passing transcript: `bundle://proof/SB03/transcripts/closure.txt`
- Anti-stub audit transcript: `bundle://proof/SB03/transcripts/closure.txt`
- Failing-first: N/A - process mutation denial is covered by adversarial negative proof in the passing transcript, and no behavior was added that mutates process state.
- Test name: `Run_execution_tab_exposes_runtime_host_readback_for_selected_run`
