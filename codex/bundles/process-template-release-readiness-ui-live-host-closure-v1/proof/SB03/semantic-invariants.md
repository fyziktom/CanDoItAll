# SB03 Semantic Invariants

- Invariant ID: `SB03-INV-001`
- Source raw note: REQ-003 runtime-host operator readback.
- Expected behavior: Run detail Execution tab displays read-only runtime-host status, audit metadata, lane, capability, diagnostics, evidence counts, and mutation-denial flags for the selected persisted run and step.
- Disallowed shallow implementation: Static UI text or fabricated run identifiers cannot satisfy the requirement.
- Failing-first test: `Run_execution_tab_exposes_runtime_host_readback_for_selected_run` would fail if the readback surface were absent or not tied to the selected run.
- Passing test: `Run_execution_tab_exposes_runtime_host_readback_for_selected_run`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.RuntimeHostReadback.cs`, `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunsRuntimeHostReadbackSection.razor`
- Production assertions: The workspace calls the existing read-only verification facade and maps facade DTO values into operator-visible view models.
- Red-team negative case: The UI must show denied process, transition, and finalizer write permissions.
- Downstream dependency check: SB04 and SB07 browser proof uses the same run detail area and screenshot route.
